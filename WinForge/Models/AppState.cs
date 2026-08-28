using System.Collections.Generic;

namespace WinForge.Models;

public class AppState
{
    public string Username { get; set; } = "Admin";
    public string Password { get; set; } = "";
    public bool AutoLogon { get; set; }
    public bool SkipOobe { get; set; } = true;
    public bool BypassSystemRequirements { get; set; }

    // XML figé au moment du clic "Générer" (page Autounattend). Si présent, le build
    // réutilise ce texte tel quel au lieu de régénérer depuis les options courantes —
    // ce que l'utilisateur voit dans l'aperçu est exactement ce qui sera injecté dans l'ISO.
    public string? GeneratedAutounattendXml { get; set; }

    public List<AppEntry> SelectedApps { get; set; } = new();

    public List<string> DriverPaths { get; set; } = new();
    public List<string> UpdatePaths { get; set; } = new();

    public int EditionIndex { get; set; } = 1;

    public string OutputIsoName { get; set; } = "WinForge_Custom.iso";
    public bool BuildBootable { get; set; } = true;
    public bool InjectAutounattend { get; set; } = true;

    public bool DisableTelemetry { get; set; }
    public bool DisableCortana { get; set; }
    public bool OptimizeServices { get; set; }
    public bool PerformanceMode { get; set; }

    // Apps installées au premier démarrage (winget silencieux + installeurs .exe/.msi maison).
    public List<AppInstallEntry> AppsToInstall { get; set; } = new();
}
