using Windows.UI;

namespace WinForge.Models;

public class AppDebloatItem
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string PackageName { get; set; } = "";
    public Color RiskColor { get; set; }
    public bool IsSelected { get; set; }
}