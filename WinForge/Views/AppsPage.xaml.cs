using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinForge.Models;
using WinRT.Interop;

namespace WinForge.Views;

public sealed partial class AppsPage : Page
{
    // Racine du dossier de projets internes S.O.S INFO LUDO — usage personnel/atelier
    // uniquement, jamais une variante destinée à un client (voir InternalAppScannerService).
    private const string SosInfoLudoRoot = @"C:\Users\Admin\SOSINFOLUDO";

    public ObservableCollection<AppInstallEntry> InternalApps { get; } = new();

    private ObservableCollection<AppInstallEntry> CustomApps { get; } = new();

    public AppsPage()
    {
        InitializeComponent();
        CustomAppsList.ItemsSource = CustomApps;
        RestoreFromState();
        SetActiveTab(showInternal: true);
    }

    private void TabInternal_Click(object sender, RoutedEventArgs e) => SetActiveTab(showInternal: true);
    private void TabCustom_Click(object sender, RoutedEventArgs e) => SetActiveTab(showInternal: false);

    private void SetActiveTab(bool showInternal)
    {
        InternalTabContent.Visibility = showInternal ? Visibility.Visible : Visibility.Collapsed;
        CustomTabContent.Visibility = showInternal ? Visibility.Collapsed : Visibility.Visible;

        // Page.Resources ne contient que ce qui est déclaré localement dans <Page.Resources> (rien
        // ici) : il ne remonte PAS automatiquement vers Application.Resources malgré la cascade de
        // résolution habituelle pour {StaticResource} en XAML. En code-behind, il faut accéder
        // explicitement à Application.Current.Resources pour les ressources globales du thème.
        var accent = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["WFAccentBrush"];
        var muted = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["WFMuted2Brush"];
        var text = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["WFTextBrush"];

        TabInternalButton.Foreground = showInternal ? text : muted;
        TabInternalButton.BorderBrush = showInternal ? accent : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        TabCustomButton.Foreground = showInternal ? muted : text;
        TabCustomButton.BorderBrush = showInternal ? new SolidColorBrush(Microsoft.UI.Colors.Transparent) : accent;
    }

    private void RestoreFromState()
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        // Les apps internes détectées lors d'un scan précédent (même session ou profil chargé)
        // réapparaissent cochées ; un nouveau scan les fusionnera avec RestoreCheckedState.
        foreach (var app in mw.State.AppsToInstall.Where(a => a.Source == AppInstallSource.CustomInstaller && a.InstallerPath?.StartsWith(SosInfoLudoRoot, StringComparison.OrdinalIgnoreCase) == true))
        {
            app.IsChecked = true;
            InternalApps.Add(app);
        }

        foreach (var app in mw.State.AppsToInstall.Where(a => a.Source == AppInstallSource.CustomInstaller && a.InstallerPath?.StartsWith(SosInfoLudoRoot, StringComparison.OrdinalIgnoreCase) != true))
            CustomApps.Add(app);

        if (!string.IsNullOrWhiteSpace(mw.State.WallpaperPath))
            WallpaperPathText.Text = mw.State.WallpaperPath;

        UpdateInternalAppsSummary();
        UpdateCustomTabCount();
    }

    private void SyncStateFromSelection()
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        var selected = InternalApps.Where(a => a.IsChecked)
            .Concat(CustomApps)
            .ToList();
        mw.State.AppsToInstall = selected;
        // Un XML déjà figé sur la page Autounattend ne contiendrait pas cette sélection : il doit
        // être régénéré au prochain build plutôt que réutilisé tel quel (voir InvalidateGeneratedXml).
        mw.State.GeneratedAutounattendXml = null;
        mw.AppendLog($"[Applications] {selected.Count} application(s) configurée(s) pour le premier démarrage.");
    }

    private void InternalAppCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // Deux CheckBox cochées en succession rapide (< quelques dizaines de ms) peuvent voir leur
        // second événement Checked se déclencher avant que le binding TwoWay du premier clic n'ait
        // fini d'écrire IsChecked sur le modèle : lire InternalApps.Count(a => a.IsChecked) à cet
        // instant sous-compte la sélection (observé : "1/12" au lieu de "2/12" pour deux coches
        // rapprochées). Différer d'une frame via DispatcherQueue laisse le binding se stabiliser
        // avant de recalculer, sans changer le comportement pour un clic isolé.
        DispatcherQueue.TryEnqueue(() =>
        {
            SyncStateFromSelection();
            UpdateInternalAppsSummary();
        });
    }

    private void UpdateInternalAppsSummary()
    {
        int count = InternalApps.Count(a => a.IsChecked);
        InternalTabCountText.Text = $"{count}/{InternalApps.Count}";

        if (InternalApps.Count == 0)
        {
            InternalAppsSummaryText.Text = "Clique sur « Scanner » pour détecter tes logiciels.";
            return;
        }

        InternalAppsSummaryText.Text = $"{count} / {InternalApps.Count} logiciel(s) sélectionné(s).";
    }

    private void UpdateCustomTabCount()
    {
        CustomTabCountText.Text = CustomApps.Count.ToString();
    }

    private void BtnScanInternalApps_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        var scanned = mw.InternalAppScannerService.ScanInternalApps(SosInfoLudoRoot);

        // Préserve l'état coché des apps déjà présentes (par chemin d'installeur), plutôt que
        // de tout redémarrer à zéro à chaque nouveau scan.
        var previouslyChecked = InternalApps.Where(a => a.IsChecked).Select(a => a.InstallerPath).ToHashSet();
        foreach (var app in scanned)
            app.IsChecked = previouslyChecked.Contains(app.InstallerPath);

        InternalApps.Clear();
        foreach (var app in scanned)
            InternalApps.Add(app);

        mw.AppendLog($"[Applications] {scanned.Count} logiciel(s) interne(s) détecté(s) dans SOSINFOLUDO.");
        SyncStateFromSelection();
        UpdateInternalAppsSummary();
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
        UpdateCustomTabCount();

        if (App.MainWindow is MainWindow mw)
            mw.AppendLog("[Applications] Installeur ajouté : " + file.Name);
    }

    private void BtnRemoveCustomApp_Click(object sender, RoutedEventArgs e)
    {
        if (CustomAppsList.SelectedItem is AppInstallEntry entry)
        {
            CustomApps.Remove(entry);
            SyncStateFromSelection();
            UpdateCustomTabCount();
        }
    }

    private async void BtnPickWallpaper_Click(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new();
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        InitializeWithWindow.Initialize(picker, App.MainWindow.GetWindowHandle());

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file == null)
            return;

        WallpaperPathText.Text = file.Path;

        if (App.MainWindow is MainWindow mw)
        {
            mw.State.WallpaperPath = file.Path;
            mw.State.GeneratedAutounattendXml = null;
            mw.AppendLog("[Applications] Fond d'écran sélectionné : " + file.Name);
        }
    }
}
