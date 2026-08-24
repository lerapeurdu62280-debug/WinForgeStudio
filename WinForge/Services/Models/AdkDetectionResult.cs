namespace WinForge.Services.Models;

public class AdkDetectionResult
{
    public bool Found { get; set; }
    public string? OscdimgPath { get; set; }
    public string? AdkRootPath { get; set; }
}
