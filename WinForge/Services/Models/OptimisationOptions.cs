namespace WinForge.Services.Models;

public class OptimisationOptions
{
    public bool DisableTelemetry { get; set; }
    public bool DisableCortana { get; set; }
    public bool OptimizeServices { get; set; }
    public bool PerformanceMode { get; set; }

    public bool AnyEnabled => DisableTelemetry || DisableCortana || OptimizeServices || PerformanceMode;
}
