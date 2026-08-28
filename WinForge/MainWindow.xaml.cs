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
        private readonly AdkDetectionService _adkDetectionService = new();
        private readonly DismService _dismService = new();
        private readonly IsoService _isoService = new();
        private readonly AutounattendService _autounattendService = new();
        private readonly IsoBuilderService _isoBuilderService = new();
        private readonly OptimisationService _optimisationService = new();
        private readonly WallpaperService _wallpaperService = new();
        private readonly UsbWriterService _usbWriterService = new();
        private readonly InternalAppScannerService _internalAppScannerService = new();
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
        public WallpaperService WallpaperService => _wallpaperService;
        public UsbWriterService UsbWriterService => _usbWriterService;
        public InternalAppScannerService InternalAppScannerService => _internalAppScannerService;

        public object? CurrentModulePage => ModuleFrame.Content;

        public MainWindow()
        {
            InitializeComponent();

            // Sans ça, Windows dessine sa propre barre de titre système (fond clair, non thémé)
            // au-dessus de la topbar custom — visible comme un liseré blanc en haut de la fenêtre.
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            // Les boutons système (minimiser/agrandir/fermer) restent dessinés par Windows dans le
            // coin supérieur droit de AppTitleBar : sans ceci ils gardent leurs couleurs claires par
            // défaut, qui détonnent sur un fond sombre.
            var titleBar = AppWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            titleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 0xE4, 0xE7, 0xEB);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 0x2A, 0x30, 0x38);
            titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 0xE4, 0xE7, 0xEB);
            titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 0x1C, 0x2E, 0x40);

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

        public string GetCurrentIsoPath() => _isoPath;
        public void SetCurrentIsoPath(string path) => _isoPath = path;

        public IntPtr GetWindowHandle() => WindowNative.GetWindowHandle(this);

        public System.Collections.ObjectModel.ObservableCollection<LogEntry> LogEntries { get; } = new();

        public void AppendLog(string message)
        {
            // S'assure que la mise à jour de l'UI se fait sur le thread principal
            DispatcherQueue.TryEnqueue(() =>
            {
                LogEntries.Add(new LogEntry(message));
                LogListView.ScrollIntoView(LogEntries[^1]);
            });
        }

        public void SetStatus(string message)
        {
            StatusLabel.Text = message;
        }

        // Barres de progression désormais sans UI (l'ancien panneau latéral droit a été retiré) :
        // les appels restent des no-op silencieux plutôt que de modifier les 3 pages qui les
        // invoquent (DebloatingPage, InjectionPage, ExportISOPage) pour un affichage qui n'a plus
        // d'emplacement dans la maquette actuelle.
        public void UpdateSelectionCount(int count) { }
        public void UpdateDebloatProgress(double value) { }
        public void UpdateInjectProgress(double value) { }
        public void UpdateUpdateProgress(double value) { }

        // Seule source de vérité pour ces réglages (State.DisableTelemetry etc.) : la case cochée
        // sur OptimisationPage elle-même. Il n'y a plus de toggle miroir dans un panneau global.
        public void SyncGlobalOptimisationToggle(string toggleName, bool value)
        {
            switch (toggleName)
            {
                case "ToggleTelemetry": State.DisableTelemetry = value; break;
                case "ToggleCortana": State.DisableCortana = value; break;
                case "ToggleServices": State.OptimizeServices = value; break;
                case "TogglePerf": State.PerformanceMode = value; break;
            }
        }

        private void NavEditions_Click(object sender, RoutedEventArgs e)
        {
            ModuleFrame.Navigate(typeof(EditionsPage));
            SetStatus("Module Éditions");
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

        private void NavApps_Click(object sender, RoutedEventArgs e)
        {
            ModuleFrame.Navigate(typeof(AppsPage));
            SetStatus("Module Applications");
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

    }
}
