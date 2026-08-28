using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WinForge.Services.Models;

namespace WinForge.Services;

public class UsbWriterService
{
    // Ne renvoie jamais un disque interne : BusType est lu côté matériel (contrôleur), pas
    // déduit d'une lettre de lecteur ou d'un chemin — un disque interne ne peut pas usurper "USB".
    public async Task<List<UsbDeviceInfo>> ListUsbDevicesAsync()
    {
        string command =
            "Get-Disk | Where-Object { $_.BusType -eq 'USB' } | " +
            "Select-Object Number, FriendlyName, Size, SerialNumber | ConvertTo-Json -Compress";

        string output = await RunPowerShellAsync(command);
        output = output.Trim();
        if (string.IsNullOrEmpty(output))
            return new List<UsbDeviceInfo>();

        // ConvertTo-Json ne renvoie pas un tableau si un seul élément correspond.
        var json = output.StartsWith("[") ? output : $"[{output}]";

        using var doc = JsonDocument.Parse(json);
        var result = new List<UsbDeviceInfo>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            result.Add(new UsbDeviceInfo
            {
                DiskNumber = element.GetProperty("Number").GetInt32(),
                FriendlyName = element.TryGetProperty("FriendlyName", out var fn) ? fn.GetString() ?? "Périphérique USB" : "Périphérique USB",
                SizeBytes = element.GetProperty("Size").GetInt64(),
                SerialNumber = element.TryGetProperty("SerialNumber", out var sn) ? (sn.GetString() ?? "") : ""
            });
        }
        return result;
    }

    // Efface, partitionne (GPT+FAT32 pour l'UEFI moderne), copie le contenu de l'ISO extraite
    // et rend la clé bootable BIOS legacy en plus (bootsect.exe), à la manière de Rufus en
    // mode "Windows USB standard". Le disque cible doit avoir été confirmé par l'utilisateur
    // AVANT cet appel — cette méthode ne redemande aucune confirmation.
    public async Task WriteIsoToUsbAsync(int diskNumber, string extractedIsoRoot, IProgressReporter reporter)
    {
        reporter.SetStatus("Préparation du disque USB...");
        reporter.Log($"[USB] Ciblage du disque {diskNumber} — nettoyage et partitionnement.");

        // diskpart : nettoyage complet, table GPT, une partition FAT32 active formatée avec
        // lettre assignée automatiquement. FAT32 est nécessaire pour le boot UEFI standard
        // (le firmware UEFI ne lit que FAT sur la partition ESP/boot) — voir la gestion du
        // dépassement 4 Go de install.wim plus bas (copie séparée + split si besoin).
        string diskpartScript =
            $"select disk {diskNumber}\n" +
            "clean\n" +
            "convert gpt\n" +
            "create partition primary\n" +
            "format fs=fat32 quick label=\"WINFORGE\"\n" +
            "assign\n" +
            "active\n" +
            "exit\n";

        string driveLetter = await RunDiskpartAsync(diskpartScript, reporter);
        reporter.Log($"[USB] Clé préparée, montée sur {driveLetter}:");

        await CopyIsoContentAsync(extractedIsoRoot, driveLetter, reporter);
        await MakeBootableAsync(extractedIsoRoot, driveLetter, reporter);

        reporter.SetStatus("Clé USB bootable créée.");
        reporter.Log("[USB] Terminé — la clé est prête à démarrer.");
    }

    private async Task CopyIsoContentAsync(string sourceRoot, string driveLetter, IProgressReporter reporter)
    {
        reporter.SetStatus("Copie des fichiers sur la clé USB...");
        string destRoot = $"{driveLetter}:\\";

        var allFiles = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
        int total = allFiles.Length;
        int done = 0;

        // install.wim dépasse fréquemment 4 Go (limite d'un fichier unique sur FAT32) : on le
        // détecte et on le scinde avec dism /Split-Image plutôt que d'échouer la copie à mi-chemin.
        string wimPath = Path.Combine(sourceRoot, "sources", "install.wim");
        bool needsSplit = File.Exists(wimPath) && new FileInfo(wimPath).Length > 4_000_000_000L;

        foreach (var file in allFiles)
        {
            string relative = Path.GetRelativePath(sourceRoot, file);
            string dest = Path.Combine(destRoot, relative);

            bool isInstallWim = needsSplit && string.Equals(file, wimPath, StringComparison.OrdinalIgnoreCase);
            if (!isInstallWim)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, overwrite: true);
            }

            done++;
            if (done % 25 == 0 || done == total)
                reporter.SetProgress(done * 100.0 / total);
        }

        if (needsSplit)
        {
            reporter.Log("[USB] install.wim dépasse 4 Go (limite FAT32), découpage via DISM...");
            string destSwm = Path.Combine(destRoot, "sources", "install.swm");
            Directory.CreateDirectory(Path.GetDirectoryName(destSwm)!);
            await RunProcessAsync("dism.exe", $"/Split-Image /ImageFile:\"{wimPath}\" /SWMFile:\"{destSwm}\" /FileSize:3800", reporter, "USB");
        }

        reporter.Log("[USB] Copie des fichiers terminée.");
    }

    private async Task MakeBootableAsync(string extractedIsoRoot, string driveLetter, IProgressReporter reporter)
    {
        // bootsect.exe vit dans l'ISO Windows elle-même (boot\bootsect.exe), pas dans l'ADK :
        // il est donc déjà présent dans le workspace extrait, sans dépendance supplémentaire.
        string bootsectPath = Path.Combine(extractedIsoRoot, "boot", "bootsect.exe");
        if (!File.Exists(bootsectPath))
        {
            reporter.Log("[USB] bootsect.exe introuvable dans l'ISO source, boot BIOS legacy non configuré (le boot UEFI reste fonctionnel).");
            return;
        }

        reporter.SetStatus("Configuration du démarrage (BIOS legacy)...");
        await RunProcessAsync(bootsectPath, $"/nt60 {driveLetter}: /force /mbr", reporter, "USB");
        reporter.Log("[USB] Secteur de démarrage BIOS legacy configuré.");
    }

    private async Task<string> RunDiskpartAsync(string script, IProgressReporter reporter)
    {
        string scriptPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(scriptPath, script, Encoding.ASCII);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "diskpart.exe"),
                Arguments = $"/s \"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi)!;
            string output = await proc.StandardOutput.ReadToEndAsync();
            string error = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            reporter.Log($"[USB] diskpart : {output}");

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"Échec diskpart (code {proc.ExitCode}) : {error}{output}");

            // diskpart n'expose pas directement la lettre attribuée dans sa sortie de façon fiable
            // selon la version : on l'interroge séparément via Get-Volume sur le disque connu.
            return await GetAssignedDriveLetterAsync(reporter);
        }
        finally
        {
            File.Delete(scriptPath);
        }
    }

    private async Task<string> GetAssignedDriveLetterAsync(IProgressReporter reporter)
    {
        string command =
            "$p = Get-Partition | Where-Object { $_.DriveLetter -and $_.Type -eq 'Basic' } | " +
            "Sort-Object -Property @{Expression='CreationTime';Descending=$true} -ErrorAction SilentlyContinue | " +
            "Select-Object -First 1 -ExpandProperty DriveLetter; $p";

        for (int attempt = 0; attempt < 10; attempt++)
        {
            string output = (await RunPowerShellAsync(command)).Trim();
            if (output.Length == 1 && char.IsLetter(output[0]))
                return output;
            await Task.Delay(500);
        }

        throw new InvalidOperationException("Impossible de déterminer la lettre de lecteur attribuée à la clé USB après formatage.");
    }

    private static async Task RunProcessAsync(string fileName, string arguments, IProgressReporter reporter, string logPrefix)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi)!;
        string output = await proc.StandardOutput.ReadToEndAsync();
        string error = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Échec de {Path.GetFileName(fileName)} (code {proc.ExitCode}) : {error}{output}");

        reporter.Log($"[{logPrefix}] {Path.GetFileName(fileName)} : {output.Trim()}");
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
}
