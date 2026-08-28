using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinForge.Models;
using WinRT.Interop;

namespace WinForge.Views;

public sealed partial class AppsPage : Page
{
    private ObservableCollection<AppInstallEntry> CustomApps { get; } = new();

    public AppsPage()
    {
        InitializeComponent();
        CustomAppsList.ItemsSource = CustomApps;
        RestoreFromState();
    }

    private void RestoreFromState()
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        foreach (var app in mw.State.AppsToInstall)
            CustomApps.Add(app);
    }

    private void SyncStateFromSelection()
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        mw.State.AppsToInstall = new System.Collections.Generic.List<AppInstallEntry>(CustomApps);
        // Un XML déjà figé sur la page Autounattend ne contiendrait pas cette sélection : il doit
        // être régénéré au prochain build plutôt que réutilisé tel quel (voir InvalidateGeneratedXml).
        mw.State.GeneratedAutounattendXml = null;
        mw.AppendLog($"[Applications] {CustomApps.Count} application(s) configurée(s) pour le premier démarrage.");
    }

    private async void BtnAddCustomApp_Click(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new();
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".msi");
        InitializeWithWindow.Initialize(picker, App.MainWindow.GetWindowHandle());

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file == null)
            return;

        // Un .msi lancé directement via Start-Process n'est pas exécutable par lui-même dans la
        // plupart des cas (l'argument silencieux NSIS/Inno "/S" ne s'y applique pas) : msiexec
        // /qn est le silencieux MSI standard. Pour un .exe on suppose NSIS/Inno ("/S"), le plus
        // répandu — l'utilisateur peut avoir un installeur qui attend autre chose (InstallShield
        // "/s /v/qn", par exemple), non détectable automatiquement.
        bool isMsi = file.FileType.Equals(".msi", System.StringComparison.OrdinalIgnoreCase);
        var entry = new AppInstallEntry
        {
            Source = AppInstallSource.CustomInstaller,
            Name = file.Name,
            InstallerPath = file.Path,
            SilentArgs = isMsi ? "/qn /norestart" : "/S"
        };
        CustomApps.Add(entry);
        SyncStateFromSelection();

        if (App.MainWindow is MainWindow mw)
            mw.AppendLog("[Applications] Installeur ajouté : " + file.Name);
    }

    private void BtnRemoveCustomApp_Click(object sender, RoutedEventArgs e)
    {
        if (CustomAppsList.SelectedItem is AppInstallEntry entry)
        {
            CustomApps.Remove(entry);
            SyncStateFromSelection();
        }
    }
}
