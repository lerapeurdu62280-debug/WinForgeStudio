using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;
using WinForge.Services.Models;

namespace WinForge.Views;

public sealed partial class AutounattendPage : Page
{
    private readonly AutounattendService _autounattendService = new();

    public AutounattendPage()
    {
        InitializeComponent();
        LoadFromState();
    }

    private void LoadFromState()
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        TxtUsername.Text = mw.State.Username;
        TxtPassword.Password = mw.State.Password;
        ChkAutoLogon.IsChecked = mw.State.AutoLogon;
        ChkSkipOOBE.IsChecked = mw.State.SkipOobe;
        ChkBypassSystemRequirements.IsChecked = mw.State.BypassSystemRequirements;
    }

    private void TxtUsername_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (App.MainWindow is MainWindow mw)
        {
            mw.State.Username = string.IsNullOrWhiteSpace(TxtUsername.Text) ? "Admin" : TxtUsername.Text.Trim();
            InvalidateGeneratedXml(mw);
        }
    }

    private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mw)
        {
            mw.State.Password = TxtPassword.Password;
            InvalidateGeneratedXml(mw);
        }
    }

    private void ChkAutoLogon_Changed(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mw)
        {
            mw.State.AutoLogon = ChkAutoLogon.IsChecked == true;
            InvalidateGeneratedXml(mw);
        }
    }

    private void ChkSkipOOBE_Changed(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mw)
        {
            mw.State.SkipOobe = ChkSkipOOBE.IsChecked == true;
            InvalidateGeneratedXml(mw);
        }
    }

    private void ChkBypassSystemRequirements_Changed(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mw)
        {
            mw.State.BypassSystemRequirements = ChkBypassSystemRequirements.IsChecked == true;
            InvalidateGeneratedXml(mw);
        }
    }

    // Le XML figé au clic "Générer" ne doit plus être réutilisé tel quel si l'utilisateur
    // modifie un champ après coup : sinon le build injecterait une config périmée sans le dire.
    private static void InvalidateGeneratedXml(MainWindow mw)
    {
        mw.State.GeneratedAutounattendXml = null;
    }

    private void BtnGenerateXml_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is not MainWindow mw)
            return;

        var options = new AutounattendOptions
        {
            Username = mw.State.Username,
            Password = string.IsNullOrEmpty(mw.State.Password) ? null : mw.State.Password,
            AutoLogon = mw.State.AutoLogon,
            SkipOobe = mw.State.SkipOobe,
            BypassSystemRequirements = mw.State.BypassSystemRequirements
        };

        string xml = _autounattendService.GenerateXml(options);
        XmlPreview.Text = xml;
        mw.State.GeneratedAutounattendXml = xml;

        mw.AppendLog("[Autounattend] XML généré — ce contenu exact sera injecté dans l'ISO au build.");
        mw.SetStatus("XML autounattend généré");
    }
}
