using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using WinForge.Models;

namespace WinForge.Views;

public sealed partial class DebloatingPage : Page
{
    public ObservableCollection<AppEntry> Categories { get; } = DebloatDatabase.GetAllApps();

    public DebloatingPage()
    {
        InitializeComponent();
        RestoreSelectionFromState();
        UpdateCount();
    }

    private void RestoreSelectionFromState()
    {
        if (App.MainWindow is not MainWindow mw || mw.State.SelectedApps.Count == 0)
            return;

        var selectedPackageNames = new HashSet<string>(mw.State.SelectedApps.Select(a => a.PackageName));
        foreach (var category in Categories)
        {
            foreach (var app in category.Children)
                app.IsSelected = selectedPackageNames.Contains(app.PackageName);
        }
    }

    private void CategoryCheckBox_Changed(object sender, RoutedEventArgs e) => UpdateCount();

    private void CheckBox_Changed(object sender, RoutedEventArgs e) => UpdateCount();

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var category in Categories)
            category.IsSelected = true;
        UpdateCount();
    }

    private void BtnDeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var category in Categories)
            category.IsSelected = false;
        UpdateCount();
    }

    private void BtnSafeOnly_Click(object sender, RoutedEventArgs e)
    {
        foreach (var category in Categories)
        {
            foreach (var app in category.Children)
                app.IsSelected = app.Risk == RiskLevel.Safe;
        }
        UpdateCount();
    }

    public List<AppEntry> GetSelectedApps()
    {
        var result = new List<AppEntry>();
        foreach (var category in Categories)
            result.AddRange(category.Children.Where(a => a.IsSelected));
        return result;
    }

    private void UpdateCount()
    {
        var selected = GetSelectedApps();
        int count = selected.Count;
        SummaryText.Text = count <= 1
            ? $"{count} application sélectionnée"
            : $"{count} applications sélectionnées";

        if (App.MainWindow is MainWindow mw)
        {
            mw.State.SelectedApps = selected;
            mw.UpdateSelectionCount(count);
            mw.AppendLog($"[Debloat] {count} application(s) configurées.");
            mw.SetStatus($"✅ {count} app(s) configurées pour suppression");
            mw.UpdateDebloatProgress(count * 5 > 100 ? 100 : count * 5);
        }
    }
}
