using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinForge.Services;
using WinRT.Interop;

namespace WinForge.Views;

public sealed partial class EditionsPage : Page
{
    public EditionsPage()
    {
        InitializeComponent();
        RestoreFromState();
    }

    private void RestoreFromState()
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        string isoPath = mw.GetCurrentIsoPath();
        if (string.IsNullOrWhiteSpace(isoPath) || !File.Exists(isoPath))
            return;

        IsoNameLabel.Text = Path.GetFileName(isoPath);
        IsoVersionLabel.Text = "Version : Windows ISO détectée";
        IsoSizeLabel.Text = $"Taille : {new FileInfo(isoPath).Length / (1024 * 1024)} Mo";
    }

    private async void BtnOpenISO_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        FileOpenPicker picker = new();
        picker.FileTypeFilter.Add(".iso");
        InitializeWithWindow.Initialize(picker, mw.GetWindowHandle());

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file == null)
            return;

        mw.SetCurrentIsoPath(file.Path);
        IsoNameLabel.Text = file.Name;
        IsoVersionLabel.Text = "Version : Windows ISO détectée";
        IsoSizeLabel.Text = $"Taille : {new FileInfo(file.Path).Length / (1024 * 1024)} Mo";

        mw.AppendLog($"[Éditions] ISO chargée : {file.Name}");
        mw.SetStatus("ISO chargée");

        await LoadEditionsAsync(file.Path);
    }

    private async Task LoadEditionsAsync(string isoPath)
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        EditionComboBox.IsEnabled = false;
        EditionComboBox.ItemsSource = null;

        var reporter = new UiProgressReporter(mw, "Éditions");
        bool mounted = false;

        try
        {
            var mountResult = await mw.IsoService.MountIsoAsync(isoPath, reporter);
            mounted = true;

            string installImage = mw.IsoService.LocateInstallImage(mountResult.DriveLetter);
            var editions = await mw.IsoService.GetEditionsAsync(installImage, reporter);

            await mw.IsoService.DismountIsoAsync(isoPath, reporter);
            mounted = false;

            if (editions.Count == 0)
            {
                mw.AppendLog("[Éditions] Aucune édition détectée, index 1 utilisé par défaut.");
                return;
            }

            EditionComboBox.ItemsSource = editions;
            EditionComboBox.SelectedIndex = 0;
            EditionComboBox.IsEnabled = editions.Count > 1;
            mw.State.EditionIndex = editions[0].Index;
        }
        catch (Exception ex)
        {
            mw.AppendLog($"[Éditions] Échec de la lecture des éditions ({ex.Message}), index 1 utilisé par défaut.");
            mw.State.EditionIndex = 1;
        }
        finally
        {
            if (mounted)
            {
                try { await mw.IsoService.DismountIsoAsync(isoPath, reporter); }
                catch (Exception ex) { mw.AppendLog($"[Éditions] Échec du démontage de nettoyage : {ex.Message}"); }
            }
        }
    }

    private void EditionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        if (EditionComboBox.SelectedItem is Services.Models.WimEditionInfo edition)
            mw.State.EditionIndex = edition.Index;
    }
}
