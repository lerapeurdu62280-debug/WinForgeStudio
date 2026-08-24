using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WinForge.Views;

public sealed partial class InjectionPage : Page
{
    private ObservableCollection<string> Items { get; } = new();

    public InjectionPage()
    {
        InitializeComponent();
        InjectionList.ItemsSource = Items;
        RestoreFromState();
    }

    private void RestoreFromState()
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        foreach (var driver in mw.State.DriverPaths)
            Items.Add("Driver : " + driver);
        foreach (var update in mw.State.UpdatePaths)
            Items.Add("MAJ : " + update);
    }

    private void SyncStateFromItems()
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        mw.State.DriverPaths = Items
            .Where(i => i.StartsWith("Driver : "))
            .Select(i => i.Substring("Driver : ".Length))
            .ToList();

        mw.State.UpdatePaths = Items
            .Where(i => i.StartsWith("MAJ : "))
            .Select(i => i.Substring("MAJ : ".Length))
            .ToList();
    }

    private async void BtnAddDriver_Click(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new();
        picker.FileTypeFilter.Add(".inf");
        picker.FileTypeFilter.Add(".sys");
        InitializeWithWindow.Initialize(picker, App.MainWindow.GetWindowHandle());

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file == null) return;

        Items.Add("Driver : " + file.Path);
        SyncStateFromItems();

        if (App.MainWindow is MainWindow mw)
        {
            mw.AppendLog("[Injection] Driver ajouté : " + file.Name);
            mw.UpdateInjectProgress(30);
        }
    }

    private async void BtnAddUpdate_Click(object sender, RoutedEventArgs e)
    {
        FileOpenPicker picker = new();
        picker.FileTypeFilter.Add(".cab");
        picker.FileTypeFilter.Add(".msu");
        InitializeWithWindow.Initialize(picker, App.MainWindow.GetWindowHandle());

        StorageFile? file = await picker.PickSingleFileAsync();
        if (file == null) return;

        Items.Add("MAJ : " + file.Path);
        SyncStateFromItems();

        if (App.MainWindow is MainWindow mw)
        {
            mw.AppendLog("[Injection] Mise à jour ajoutée : " + file.Name);
            mw.UpdateUpdateProgress(40);
        }
    }

    public ObservableCollection<string> GetItems() => Items;
}
