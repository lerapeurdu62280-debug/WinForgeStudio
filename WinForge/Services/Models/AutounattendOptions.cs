namespace WinForge.Services.Models;

public class AutounattendOptions
{
    public string Username { get; set; } = "Admin";
    public bool AutoLogon { get; set; }
    public bool SkipOobe { get; set; } = true;
    public string? Password { get; set; }
    public bool BypassSystemRequirements { get; set; }
}
