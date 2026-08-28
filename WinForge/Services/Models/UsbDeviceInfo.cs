namespace WinForge.Services.Models;

public class UsbDeviceInfo
{
    public int DiskNumber { get; set; }
    public string FriendlyName { get; set; } = "";
    public long SizeBytes { get; set; }
    public string SerialNumber { get; set; } = "";

    public string DisplayLabel => $"{FriendlyName} — {SizeBytes / 1_000_000_000.0:F1} Go (Disque {DiskNumber})";
}
