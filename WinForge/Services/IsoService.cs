using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Services.Models;

namespace WinForge.Services;

public class IsoService
{
    public async Task<IsoMountResult> MountIsoAsync(string isoPath, IProgressReporter reporter)
    {
        reporter.Log($"Montage de l'ISO : {isoPath}");

        // Get-Volume peut ne pas voir la lettre de lecteur immédiatement après Mount-DiskImage
        // (attribution asynchrone côté Windows) : on retente pendant quelques secondes.
        string command =
            $"$img = Mount-DiskImage -ImagePath '{EscapeSingleQuotes(isoPath)}' -PassThru; " +
            "$letter = $null; " +
            "for ($i = 0; $i -lt 20 -and -not $letter; $i++) { " +
            "$letter = ($img | Get-Volume).DriveLetter; " +
            "if (-not $letter) { Start-Sleep -Milliseconds 500 } " +
            "}; " +
            "$letter";

        string output = await RunPowerShellAsync(command);
        string trimmed = output.Trim();

        if (trimmed.Length == 0 || !char.IsLetter(trimmed[0]))
            throw new InvalidOperationException($"Impossible de déterminer la lettre de lecteur après montage de l'ISO. Sortie : {output}");

        char driveLetter = trimmed[0];
        reporter.Log($"ISO montée sur le lecteur {driveLetter}:");

        return new IsoMountResult { DriveLetter = driveLetter, ImagePath = isoPath };
    }

    public async Task DismountIsoAsync(string isoPath, IProgressReporter reporter)
    {
        reporter.Log("Démontage de l'ISO...");
        string command = $"Dismount-DiskImage -ImagePath '{EscapeSingleQuotes(isoPath)}'";
        await RunPowerShellAsync(command);
        reporter.Log("ISO démontée.");
    }

    public string LocateInstallImage(char driveLetter)
    {
        string sourcesDir = $"{driveLetter}:\\sources";
        string wim = Path.Combine(sourcesDir, "install.wim");
        if (File.Exists(wim))
            return wim;

        string esd = Path.Combine(sourcesDir, "install.esd");
        if (File.Exists(esd))
            return esd;

        throw new FileNotFoundException($"Aucun install.wim ni install.esd trouvé sous {sourcesDir}.");
    }

    public async Task CopyIsoContentsAsync(char driveLetter, string destDir, IProgressReporter reporter)
    {
        string sourceRoot = $"{driveLetter}:\\";
        reporter.Log($"Copie du contenu de l'ISO vers {destDir}...");

        if (Directory.Exists(destDir))
        {
            await Task.Run(() => DeleteDirectoryForce(destDir));
        }
        Directory.CreateDirectory(destDir);

        await Task.Run(() => CopyDirectory(sourceRoot, destDir, reporter));

        reporter.Log("Copie terminée.");
    }

    private static void DeleteDirectoryForce(string path)
    {
        foreach (string file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        const int maxAttempts = 5;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                Directory.Delete(path, true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException) when (attempt < maxAttempts)
            {
                Thread.Sleep(500);
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir, IProgressReporter reporter)
    {
        Directory.CreateDirectory(destDir);

        foreach (string dir in Directory.GetDirectories(sourceDir))
        {
            string name = Path.GetFileName(dir);
            CopyDirectory(dir, Path.Combine(destDir, name), reporter);
        }

        foreach (string file in Directory.GetFiles(sourceDir))
        {
            string destFile = Path.Combine(destDir, Path.GetFileName(file));

            if (File.Exists(destFile))
            {
                var existingAttrs = File.GetAttributes(destFile);
                if ((existingAttrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                    File.SetAttributes(destFile, existingAttrs & ~FileAttributes.ReadOnly);
            }

            File.Copy(file, destFile, true);

            var attrs = File.GetAttributes(destFile);
            if ((attrs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                File.SetAttributes(destFile, attrs & ~FileAttributes.ReadOnly);
        }
    }

    public async Task<List<WimEditionInfo>> GetEditionsAsync(string wimOrEsdPath, IProgressReporter reporter)
    {
        reporter.Log($"Lecture des éditions de {Path.GetFileName(wimOrEsdPath)}...");

        string dismPath = Path.Combine(Environment.SystemDirectory, "dism.exe");
        // /English force les libellés (Name/Index/Description) en anglais, indépendamment
        // de la langue du système hôte — nécessaire pour un parsing fiable du texte de sortie.
        string args = $"/English /Get-WimInfo /WimFile:\"{wimOrEsdPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = dismPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        string output;
        using (var proc = Process.Start(psi)!)
        {
            output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
        }

        // Même caractère parasite que dans DismService (DISM localisé insère un U+FEFF avant ":").
        var editions = new List<WimEditionInfo>();
        var indexMatches = Regex.Matches(output, @"Index.?:\s*(\d+)");
        var nameMatches = Regex.Matches(output, @"Name.?:\s*(.+)");
        var descMatches = Regex.Matches(output, @"Description.?:\s*(.+)");

        for (int i = 0; i < indexMatches.Count; i++)
        {
            editions.Add(new WimEditionInfo
            {
                Index = int.Parse(indexMatches[i].Groups[1].Value.Trim()),
                Name = i < nameMatches.Count ? nameMatches[i].Groups[1].Value.Trim() : "",
                Description = i < descMatches.Count ? descMatches[i].Groups[1].Value.Trim() : ""
            });
        }

        reporter.Log($"{editions.Count} édition(s) trouvée(s).");
        return editions;
    }

    private static async Task<string> RunPowerShellAsync(string command)
    {
        string powershellPath = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        string encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));

        var psi = new ProcessStartInfo
        {
            FileName = powershellPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-NonInteractive");
        psi.ArgumentList.Add("-EncodedCommand");
        psi.ArgumentList.Add(encodedCommand);

        using var proc = Process.Start(psi)!;
        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Erreur PowerShell (code {proc.ExitCode}) : {error}");

        return output;
    }

    private static string EscapeSingleQuotes(string value) => value.Replace("'", "''");
}
