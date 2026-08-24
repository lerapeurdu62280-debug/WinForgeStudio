using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using WinForge.Exceptions;
using WinForge.Models;
using WinForge.Services;
using WinForge.Services.Models;

namespace WinForge.Views
{
    public sealed partial class ExportISOPage : Page
    {
        public ExportISOPage()
        {
            InitializeComponent();
            RestoreFromState();
        }

        private void RestoreFromState()
        {
            if (App.MainWindow is not MainWindow mw)
                return;

            TxtOutputName.Text = mw.State.OutputIsoName;
            ChkBootable.IsChecked = mw.State.BuildBootable;
            ChkInjectAutounattend.IsChecked = mw.State.InjectAutounattend;
        }

        private void TxtOutputName_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (App.MainWindow is MainWindow mw)
                mw.State.OutputIsoName = string.IsNullOrWhiteSpace(TxtOutputName.Text) ? "WinForge_Custom.iso" : TxtOutputName.Text.Trim();
        }

        private void ChkBootable_Changed(object sender, RoutedEventArgs e)
        {
            if (App.MainWindow is MainWindow mw)
                mw.State.BuildBootable = ChkBootable.IsChecked == true;
        }

        private void ChkInjectAutounattend_Changed(object sender, RoutedEventArgs e)
        {
            if (App.MainWindow is MainWindow mw)
                mw.State.InjectAutounattend = ChkInjectAutounattend.IsChecked == true;
        }

        private async void BtnBuild_Click(object sender, RoutedEventArgs e)
        {
            if (App.MainWindow is not MainWindow mw)
                return;

            var state = mw.State;

            string sourceIsoPath = mw.GetCurrentIsoPath();
            if (string.IsNullOrWhiteSpace(sourceIsoPath))
            {
                BuildStatusText.Text = "Erreur : aucune ISO source sélectionnée.";
                mw.AppendLog("[Export] Aucune ISO source sélectionnée.");
                return;
            }

            bool buildBootable = state.BuildBootable;
            bool injectAutounattend = state.InjectAutounattend;

            if (buildBootable && !mw.IsAdkAvailable)
            {
                BuildStatusText.Text = "Erreur : Windows ADK introuvable, build bootable impossible.";
                mw.AppendLog($"[Export] Windows ADK introuvable. Téléchargement : {AdkDetectionService.DownloadUrl}");
                return;
            }

            BtnBuild.IsEnabled = false;
            BuildProgressBar.Value = 0;
            BuildStatusText.Text = "Préparation du job...";

            var workspace = new WorkspaceConfig();
            var job = new JobConfig
            {
                SourceIsoPath = sourceIsoPath,
                OutputIsoPath = Path.Combine(workspace.Root, state.OutputIsoName),
                EditionIndex = state.EditionIndex,
                Workspace = workspace
            };

            Directory.CreateDirectory(workspace.Root);
            string jobFile = Path.Combine(workspace.Root, "job-last.json");
            job.SaveToFile(jobFile);
            mw.AppendLog("[Export] Job JSON créé : " + jobFile);

            var reporter = new UiProgressReporter(mw, "Export", v => DispatcherQueue.TryEnqueue(() => BuildProgressBar.Value = v));

            var selectedApps = state.SelectedApps;
            var driverPaths = state.DriverPaths;
            var updatePaths = state.UpdatePaths;

            var autounattendOptions = new AutounattendOptions
            {
                Username = state.Username,
                Password = string.IsNullOrEmpty(state.Password) ? null : state.Password,
                AutoLogon = state.AutoLogon,
                SkipOobe = state.SkipOobe,
                BypassSystemRequirements = state.BypassSystemRequirements
            };

            var optimisationOptions = new OptimisationOptions
            {
                DisableTelemetry = state.DisableTelemetry,
                DisableCortana = state.DisableCortana,
                OptimizeServices = state.OptimizeServices,
                PerformanceMode = state.PerformanceMode
            };

            bool isoMounted = false;
            bool imageMounted = false;

            try
            {
                await Task.Run(async () =>
                {
                    reporter.SetStatus("Montage de l'ISO source...");
                    var mountResult = await mw.IsoService.MountIsoAsync(sourceIsoPath, reporter);
                    isoMounted = true;

                    reporter.SetStatus("Copie du contenu de l'ISO...");
                    await mw.IsoService.CopyIsoContentsAsync(mountResult.DriveLetter, workspace.IsoExtractDir, reporter);

                    reporter.SetStatus("Démontage de l'ISO source...");
                    await mw.IsoService.DismountIsoAsync(sourceIsoPath, reporter);
                    isoMounted = false;

                    string wimCandidate = Path.Combine(workspace.IsoExtractDir, "sources", "install.wim");
                    string esdCandidate = Path.Combine(workspace.IsoExtractDir, "sources", "install.esd");
                    string installImage = File.Exists(wimCandidate)
                        ? wimCandidate
                        : File.Exists(esdCandidate)
                            ? esdCandidate
                            : throw new WinForgeBuildException("LocateInstallImage", $"Aucun install.wim ni install.esd trouvé sous {Path.Combine(workspace.IsoExtractDir, "sources")}.");

                    reporter.SetStatus("Montage de l'image Windows...");
                    var mountImageResult = await mw.DismService.MountImageAsync(installImage, job.EditionIndex, workspace.MountDir, reporter);
                    if (!mountImageResult.Success)
                        throw new WinForgeBuildException("MountImage", "Échec du montage de l'image WIM.");
                    imageMounted = true;

                    if (selectedApps.Count > 0)
                    {
                        reporter.SetStatus("Suppression des applications sélectionnées...");
                        var provisioned = await mw.DismService.GetProvisionedAppxPackagesAsync(workspace.MountDir, reporter);

                        int done = 0;
                        foreach (var app in selectedApps)
                        {
                            string? fullName = provisioned.Find(p => p.StartsWith(app.PackageName, StringComparison.OrdinalIgnoreCase));
                            if (fullName == null)
                            {
                                reporter.Log($"Application non trouvée dans l'édition, ignorée : {app.PackageName}");
                            }
                            else
                            {
                                var removeResult = await mw.DismService.RemoveProvisionedAppxPackageAsync(workspace.MountDir, fullName, reporter);
                                if (!removeResult.Success)
                                    reporter.Log($"Échec de la suppression de {app.PackageName} (ignoré, poursuite du job).");
                            }

                            done++;
                            DispatcherQueue.TryEnqueue(() => mw.UpdateDebloatProgress(done * 100.0 / selectedApps.Count));
                        }
                    }

                    if (driverPaths.Count > 0)
                    {
                        reporter.SetStatus("Injection des pilotes...");
                        int done = 0;
                        foreach (var driver in driverPaths)
                        {
                            var result = await mw.DismService.AddDriverAsync(workspace.MountDir, driver, reporter);
                            if (!result.Success)
                                reporter.Log($"Échec de l'injection du pilote {driver} (ignoré, poursuite du job).");
                            done++;
                            DispatcherQueue.TryEnqueue(() => mw.UpdateInjectProgress(done * 100.0 / driverPaths.Count));
                        }
                    }

                    if (updatePaths.Count > 0)
                    {
                        reporter.SetStatus("Injection des mises à jour...");
                        int done = 0;
                        foreach (var update in updatePaths)
                        {
                            var result = await mw.DismService.AddPackageAsync(workspace.MountDir, update, reporter);
                            if (!result.Success)
                                reporter.Log($"Échec de l'injection de la mise à jour {update} (ignoré, poursuite du job).");
                            done++;
                            DispatcherQueue.TryEnqueue(() => mw.UpdateUpdateProgress(done * 100.0 / updatePaths.Count));
                        }
                    }

                    if (optimisationOptions.AnyEnabled)
                    {
                        reporter.SetStatus("Application des optimisations...");
                        await mw.OptimisationService.ApplyAsync(workspace.MountDir, optimisationOptions, reporter);
                    }

                    reporter.SetStatus("Validation de l'image (commit)...");
                    var unmountResult = await mw.DismService.UnmountImageAsync(workspace.MountDir, commit: true, reporter);
                    imageMounted = false;
                    if (!unmountResult.Success)
                    {
                        bool stillMounted = await mw.DismService.IsMountedAsync(workspace.MountDir);
                        if (stillMounted)
                        {
                            reporter.Log("Échec du commit, tentative de discard pour libérer le point de montage...");
                            await mw.DismService.UnmountImageAsync(workspace.MountDir, commit: false, reporter);
                            throw new WinForgeBuildException("Commit", "Échec de la validation de l'image modifiée. Le job doit être relancé.");
                        }

                        reporter.Log("Avertissement DISM non fatal pendant le commit (image bien démontée, poursuite du job).");
                    }

                    if (injectAutounattend)
                    {
                        reporter.SetStatus("Génération de l'autounattend.xml...");
                        // Réutilise le XML figé au clic "Générer" (page Autounattend) s'il est encore
                        // valide pour les options courantes ; sinon régénère à partir de l'état actuel.
                        string xml = state.GeneratedAutounattendXml ?? mw.AutounattendService.GenerateXml(autounattendOptions);
                        await mw.AutounattendService.WriteToWorkspaceAsync(xml, workspace.IsoExtractDir, reporter);
                    }

                    if (buildBootable)
                    {
                        reporter.SetStatus("Construction de l'ISO bootable...");
                        string oscdimgPath = mw.OscdimgPath ?? throw new WinForgeBuildException("BuildIso", "Chemin oscdimg.exe introuvable.");
                        bool built = await mw.IsoBuilderService.BuildBootableIsoAsync(workspace.IsoExtractDir, job.OutputIsoPath, oscdimgPath, reporter);
                        if (!built)
                            throw new WinForgeBuildException("BuildIso", "Échec de la construction de l'ISO bootable.");
                    }
                });

                BuildProgressBar.Value = 100;
                BuildStatusText.Text = buildBootable
                    ? $"ISO créée : {job.OutputIsoPath}"
                    : $"Image préparée dans : {workspace.IsoExtractDir}";
                mw.SetStatus("Build terminé");
                mw.AppendLog("[Export] Job terminé avec succès.");
            }
            catch (WinForgeBuildException ex)
            {
                BuildStatusText.Text = $"Erreur à l'étape {ex.Stage} : {ex.Message}";
                mw.AppendLog($"[Export] Erreur ({ex.Stage}) : {ex.Message}");
                mw.SetStatus("Erreur de build");
            }
            catch (Exception ex)
            {
                string detail = string.IsNullOrEmpty(ex.Message) ? ex.ToString() : ex.Message;
                BuildStatusText.Text = "Erreur pendant l'exécution : " + detail;
                mw.AppendLog($"[Export] Exception ({ex.GetType().FullName}) : {detail}");
                if (ex.InnerException != null)
                    mw.AppendLog($"[Export] Cause interne : {ex.InnerException}");
                mw.SetStatus("Erreur de build");
            }
            finally
            {
                try
                {
                    if (imageMounted)
                        await mw.DismService.UnmountImageAsync(workspace.MountDir, commit: false, reporter);
                }
                catch (Exception ex)
                {
                    mw.AppendLog("[Export] Échec du nettoyage du montage image : " + ex.Message);
                }

                try
                {
                    if (isoMounted)
                        await mw.IsoService.DismountIsoAsync(sourceIsoPath, reporter);
                }
                catch (Exception ex)
                {
                    mw.AppendLog("[Export] Échec du nettoyage du montage ISO : " + ex.Message);
                }

                BtnBuild.IsEnabled = true;
            }
        }
    }
}
