namespace WinForge.Services.Models;

public class DismOperationResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string RawOutput { get; set; } = "";
}
