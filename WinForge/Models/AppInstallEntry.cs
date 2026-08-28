using System.ComponentModel;

namespace WinForge.Models;

public enum AppInstallSource
{
    // Seule source restante côté WinForge Studio : les catalogues génériques (winget, puis
    // Chocolatey) ont été retirés au profit du catalogue intégré à WinForge Assistant, qui
    // s'exécute dans la session utilisateur réelle (voir C:\Dev\WinForgeAssistant\README.md).
    CustomInstaller
}

public class AppInstallEntry : INotifyPropertyChanged
{
    private bool _isChecked;

    public AppInstallSource Source { get; set; }
    public string Name { get; set; } = "";

    // Chemin local vers le .exe/.msi au moment du build (copié dans l'ISO).
    public string? InstallerPath { get; set; }

    // Arguments d'installation silencieuse (ex. "/S", "/quiet /norestart").
    public string? SilentArgs { get; set; }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
                return;
            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
