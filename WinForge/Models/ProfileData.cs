using System.Collections.Generic;

namespace WinForge.Models;

public class ProfileData
{
    public string ProfileName { get; set; } = "Nouveau profil";
    public string IsoPath { get; set; } = "";
    public List<string> SelectedPackages { get; set; } = new();

    public bool DisableTelemetry { get; set; }
    public bool DisableCortana { get; set; }
    public bool OptimizeServices { get; set; }
    public bool PerformanceMode { get; set; }

    public List<string> Drivers { get; set; } = new();
    public List<string> Updates { get; set; } = new();

    public string Username { get; set; } = "Admin";
    public bool AutoLogon { get; set; }
    public bool SkipOobe { get; set; } = true;

    public string OutputIsoName { get; set; } = "WinForge_Custom.iso";
    public bool BuildBootable { get; set; } = true;
    public bool InjectAutounattend { get; set; } = true;
}