using System.Collections.Generic;
using WinForge.Models;

namespace WinForge.Services.Models;

public class AutounattendOptions
{
    public string Username { get; set; } = "Admin";
    public bool AutoLogon { get; set; }
    public bool SkipOobe { get; set; } = true;
    public string? Password { get; set; }
    public bool BypassSystemRequirements { get; set; }

    // Si non vide, un bloc FirstLogonCommands lance le script d'installation silencieuse
    // (Chocolatey + installeurs custom) une seule fois, en arrière-plan, après le premier logon.
    public List<AppInstallEntry> AppsToInstall { get; set; } = new();

    // Wallpaper/lockscreen sont déposés via $OEM$\$$\... (voir WallpaperService), qui nécessite
    // le même <UseConfigurationSet>true</UseConfigurationSet> que les apps custom : GenerateXml
    // doit savoir qu'un wallpaper est prévu même si AppsToInstall est vide.
    public bool HasWallpaper { get; set; }
}
