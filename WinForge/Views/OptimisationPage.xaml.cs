using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinForge.Views;

public sealed partial class OptimisationPage : Page
{
    public OptimisationPage()
    {
        InitializeComponent();
        RefreshFromState();
    }

    public void RefreshFromState()
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        ToggleTelemetry.IsOn = mw.State.DisableTelemetry;
        ToggleCortana.IsOn = mw.State.DisableCortana;
        ToggleServices.IsOn = mw.State.OptimizeServices;
        TogglePerf.IsOn = mw.State.PerformanceMode;
    }

    private void Toggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggle && App.MainWindow is MainWindow mw)
        {
            mw.AppendLog($"[Optim] {toggle.Header} = {(toggle.IsOn ? "ON" : "OFF")}");
            mw.SyncGlobalOptimisationToggle(toggle.Name, toggle.IsOn);
        }
    }
}
