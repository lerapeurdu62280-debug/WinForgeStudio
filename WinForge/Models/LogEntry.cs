using Microsoft.UI.Xaml;
using System;
using System.Text.RegularExpressions;

namespace WinForge.Models;

// Une ligne du journal affiché dans MainWindow : sépare l'horodatage, le tag de module
// optionnel ("[Applications]", "[Export]"...) et le reste du message, pour un rendu coloré
// façon terminal (timestamp discret, tag en accent) au lieu d'une seule chaîne de texte brute.
public class LogEntry
{
    private static readonly Regex TagPattern = new(@"^\[(?<tag>[^\]]+)\]\s*(?<rest>.*)$", RegexOptions.Compiled);

    public DateTime Timestamp { get; }
    public string? Tag { get; }
    public string Message { get; }

    public string TimestampText => Timestamp.ToString("HH:mm:ss");
    public Visibility HasTagVisibility => Tag != null ? Visibility.Visible : Visibility.Collapsed;

    public LogEntry(string rawMessage)
    {
        Timestamp = DateTime.Now;

        var match = TagPattern.Match(rawMessage);
        if (match.Success)
        {
            Tag = $"[{match.Groups["tag"].Value}]";
            Message = match.Groups["rest"].Value;
        }
        else
        {
            Tag = null;
            Message = rawMessage;
        }
    }
}
