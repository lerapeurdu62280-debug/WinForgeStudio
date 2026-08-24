using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinForge.Models;
using WinForge.Services;
using WinForge.Views;
using WinRT.Interop;

namespace WinForge
{
    public sealed partial class MainWindow : Window
    {
        private readonly ProfileService _profileService = new();
        private readonly AdkDetectionService _adkDetectionService = new();
        private readonly DismService _dismService = new();
        private readonly IsoService _isoService = new();
        private readonly AutounattendService _autounattendService = new();
        private readonly IsoBuilderService _isoBuilderService = new();
        private readonly OptimisationService _optimisationService = new();
        private string _isoPath = "";

        // Source de vérité unique pour l'état des modules. Le Frame de navigation détruit les
        // pages non affichées : on ne peut pas relire l'état depuis "la page actuellement affichée".
        // Chaque page pousse donc ses changements ici en temps réel.
        public AppState State { get; } = new();

        public bool IsAdkAvailable { get; private set; }
        public string? OscdimgPath { get; private set; }

        public AdkDetectionService AdkDetectionService => _adkDetectionService;
        public DismService DismService => _dismService;
        public IsoService IsoService => _isoService;
        public AutounattendService AutounattendService => _autounattendService;
        public IsoBuilderService IsoBuilderService => _isoBuilderService;
        public OptimisationService OptimisationService => _optimisationService;

        public object? CurrentModulePage => ModuleFrame.Content;

        public MainWindow()
        {
            InitializeComponent();
            ModuleFrame.Navigate(typeof(ExportISOPage));
            _ = InitializeStartupChecksAsync();
        }

        private async Task InitializeStartupChecksAsync()
        {
            var reporter = new UiProgressReporter(this, "Démarrage");

            try
            {
                await _dismService.CleanupOrphanedMountsAsync(reporter);
            }
            catch (Exception ex)
            {
                reporter.Log("Nettoyage des montages orphelins ignoré : " + ex.Message);
            }

            var adkResult = _adkDetectionService.DetectOscdimg();
            IsAdkAvailable = adkResult.Found;
            OscdimgPath = adkResult.OscdimgPath;

            if (IsAdkAvailable)
            {
                AppendLog($"[Démarrage] Windows ADK détecté : {OscdimgPath}");
            }
            else
            {
                AppendLog("[Démarrage] Windows ADK non détecté — la construction d'ISO bootable sera indisponible.");
                AppendLog($"[Démarrage] Téléchargement : {AdkDetectionService.DownloadUrl}");
                SetStatus("ADK non détecté — build ISO indisponible");
            }
        }

        public string GetCurrentIsoPath()
        {
            return _isoPath;
        }

        public IntPtr GetWindowHandle() => WindowNative.GetWindowHandle(this);

        public void AppendLog(string message)
        {
            // S'assure que la mise à jour de l'UI se fait sur le thread principal
            DispatcherQueue.TryEnqueue(() =>
            {
                LogText.Text += $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
            });
        }

        public void SetStatus(string message)
        {
            StatusLabel.Text = message;
        }

        public void UpdateSelectionCount(int count)
        {
            SelectionCountLabel.Text = count <= 1
                ? $"{count} élément sélectionné"
                : $"{count} éléments sélectionnés";
        }

        public void UpdateDebloatProgress(double value) => DebloatProgress.Value = value;
        public void UpdateInjectProgress(double value) => InjectProgress.Value = value;
        public void UpdateUpdateProgress(double value) => UpdateProgress.Value = value;

        public void SyncGlobalOptimisationToggle(string toggleName, bool value)
        {
            switch (toggleName)
            {
                case "ToggleTelemetry": GlobalTelemetryToggle.IsOn = value; State.DisableTelemetry = value; break;
                case "ToggleCortana": GlobalCortanaToggle.IsOn = value; State.DisableCortana = value; break;
                case "ToggleServices": GlobalServicesToggle.IsOn = value; State.OptimizeServices = value; break;
                case "TogglePerf": GlobalPerfToggle.IsOn = value; State.PerformanceMode = value; break;
            }
        }

        private void GlobalOptimToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch toggle)
                return;

            switch (toggle.Name)
            {
                case "GlobalTelemetryToggle": State.DisableTelemetry = toggle.IsOn; break;
                case "GlobalCortanaToggle": State.DisableCortana = toggle.IsOn; break;
                case "GlobalServicesToggle": State.OptimizeServices = toggle.IsOn; break;
                case "GlobalPerfToggle": State.PerformanceMode = toggle.IsOn; break;
            }

            if (ModuleFrame.Content is OptimisationPage optimPage)
                optimPage.RefreshFromState();
        }

        private void NavDebloat_Click(object sender, RoutedEventArgs e)
        {
            ModuleFrame.Navigate(typeof(DebloatingPage));
            SetStatus("Module Debloating");
        }

        private void NavOptim_Click(object sender, RoutedEventArgs e)
        {
            ModuleFrame.Navigate(typeof(OptimisationPage));
            SetStatus("Module Optimisation");
        }

        private void NavInject_Click(object sender, RoutedEventArgs e)
        {
            ModuleFrame.Navigate(typeof(InjectionPage));
            SetStatus("Module Injection");
        }

        private void NavUnattend_Click(object sender, RoutedEventArgs e)
        {
            ModuleFrame.Navigate(typeof(AutounattendPage));
            SetStatus("Module Autounattend");
        }

        private void NavBuild_Click(object sender, RoutedEventArgs e)
        {
            ModuleFrame.Navigate(typeof(ExportISOPage));
            SetStatus("Module Export ISO");
        }

        private void BtnGoBuild_Click(object sender, RoutedEventArgs e)
        {
            ModuleFrame.Navigate(typeof(ExportISOPage));
            SetStatus("Prêt pour la construction ISO");
        }

        private async void BtnOpenISO_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker picker = new();
            picker.FileTypeFilter.Add(".iso");
            InitializeWithWindow.Initialize(picker, GetWindowHandle());

            StorageFile? file = await picker.PickSingleFileAsync();
            if (file == null)
                return;

            _isoPath = file.Path;
            IsoNameLabel.Text = file.Name;
            IsoVersionLabel.Text = "Version : Windows ISO détectée";
            IsoSizeLabel.Text = $"Taille : {new FileInfo(file.Path).Length / (1024 * 1024)} Mo";
            EstimatedSizeLabel.Text = "Taille estimée : analyse en attente";

            AppendLog($"ISO chargée : {file.Name}");
            SetStatus("ISO chargée");

            await LoadEditionsAsync(_isoPath);
        }

        private async Task LoadEditionsAsync(string isoPath)
        {
            EditionComboBox.IsEnabled = false;
            EditionComboBox.ItemsSource = null;

            var reporter = new UiProgressReporter(this, "Éditions");
            bool mounted = false;

            try
            {
                var mountResult = await _isoService.MountIsoAsync(isoPath, reporter);
                mounted = true;

                string installImage = _isoService.LocateInstallImage(mountResult.DriveLetter);
                var editions = await _isoService.GetEditionsAsync(installImage, reporter);

                await _isoService.DismountIsoAsync(isoPath, reporter);
                mounted = false;

                if (editions.Count == 0)
                {
                    AppendLog("[Éditions] Aucune édition détectée, index 1 utilisé par défaut.");
                    return;
                }

                EditionComboBox.ItemsSource = editions;
                EditionComboBox.SelectedIndex = 0;
                EditionComboBox.IsEnabled = editions.Count > 1;
                State.EditionIndex = editions[0].Index;
            }
            catch (Exception ex)
            {
                AppendLog($"[Éditions] Échec de la lecture des éditions ({ex.Message}), index 1 utilisé par défaut.");
                State.EditionIndex = 1;
            }
            finally
            {
                if (mounted)
                {
                    try { await _isoService.DismountIsoAsync(isoPath, reporter); }
                    catch (Exception ex) { AppendLog($"[Éditions] Échec du démontage de nettoyage : {ex.Message}"); }
                }
            }
        }

        private void EditionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EditionComboBox.SelectedItem is Services.Models.WimEditionInfo edition)
                State.EditionIndex = edition.Index;
        }

        private ProfileData BuildProfileFromUi()
        {
            return new ProfileData
            {
                IsoPath = _isoPath,
                DisableTelemetry = State.DisableTelemetry,
                DisableCortana = State.DisableCortana,
                OptimizeServices = State.OptimizeServices,
                PerformanceMode = State.PerformanceMode,
                OutputIsoName = State.OutputIsoName,
                BuildBootable = State.BuildBootable,
                InjectAutounattend = State.InjectAutounattend,
                Username = State.Username,
                AutoLogon = State.AutoLogon,
                SkipOobe = State.SkipOobe,
                Drivers = new List<string>(State.DriverPaths),
                Updates = new List<string>(State.UpdatePaths)
            };
        }

        private async void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSavePicker picker = new();
                picker.SuggestedFileName = "winforge-profile";
                picker.FileTypeChoices.Add("WinForge Profile", new List<string> { ".wfp" });
                picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
                InitializeWithWindow.Initialize(picker, GetWindowHandle());

                StorageFile? file = await picker.PickSaveFileAsync();
                if (file == null)
                    return;

                var profile = BuildProfileFromUi();
                await _profileService.SaveProfileAsync(file.Path, profile);

                AppendLog($"Profil sauvegardé : {file.Name}");
                SetStatus("Profil sauvegardé");
            }
            catch (Exception ex)
            {
                AppendLog("Erreur sauvegarde profil : " + ex.Message);
                SetStatus("Erreur sauvegarde");
            }
        }

        private async void BtnLoadProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileOpenPicker picker = new();
                picker.FileTypeFilter.Add(".wfp");
                picker.FileTypeFilter.Add(".json");
                InitializeWithWindow.Initialize(picker, GetWindowHandle());

                StorageFile? file = await picker.PickSingleFileAsync();
                if (file == null)
                    return;

                var profile = await _profileService.LoadProfileAsync(file.Path);
                if (profile == null)
                    return;

                _isoPath = profile.IsoPath;
                GlobalTelemetryToggle.IsOn = profile.DisableTelemetry;
                GlobalCortanaToggle.IsOn = profile.DisableCortana;
                GlobalServicesToggle.IsOn = profile.OptimizeServices;
                GlobalPerfToggle.IsOn = profile.PerformanceMode;

                State.DisableTelemetry = profile.DisableTelemetry;
                State.DisableCortana = profile.DisableCortana;
                State.OptimizeServices = profile.OptimizeServices;
                State.PerformanceMode = profile.PerformanceMode;
                State.OutputIsoName = profile.OutputIsoName;
                State.BuildBootable = profile.BuildBootable;
                State.InjectAutounattend = profile.InjectAutounattend;
                State.Username = profile.Username;
                State.AutoLogon = profile.AutoLogon;
                State.SkipOobe = profile.SkipOobe;
                State.DriverPaths = new List<string>(profile.Drivers);
                State.UpdatePaths = new List<string>(profile.Updates);

                if (IsoNameLabel != null)
                {
                    IsoNameLabel.Text = string.IsNullOrWhiteSpace(_isoPath) ? "Aucune ISO chargée" : Path.GetFileName(_isoPath);
                }

                if (!string.IsNullOrWhiteSpace(_isoPath) && File.Exists(_isoPath))
                {
                    _ = LoadEditionsAsync(_isoPath);
                }

                ProfileSelector.Items.Add(profile.ProfileName);
                ProfileSelector.SelectedItem = profile.ProfileName;

                AppendLog($"Profil chargé : {file.Name}");
                SetStatus("Profil chargé");
            }
            catch (Exception ex)
            {
                AppendLog("Erreur chargement profil : " + ex.Message);
                SetStatus("Erreur chargement");
            }
        }
    }
}
