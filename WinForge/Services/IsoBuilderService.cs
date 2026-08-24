using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace WinForge.Services;

public class IsoBuilderService
{
    public bool ValidateBootFilesPresent(string sourceDir, out string? missingFile)
    {
        string etfsboot = Path.Combine(sourceDir, "boot", "etfsboot.com");
        string efisys = Path.Combine(sourceDir, "efi", "microsoft", "boot", "efisys.bin");

        if (!File.Exists(etfsboot))
        {
            missingFile = etfsboot;
            return false;
        }

        if (!File.Exists(efisys))
        {
            missingFile = efisys;
            return false;
        }

        missingFile = null;
        return true;
    }

    public async Task<bool> BuildBootableIsoAsync(string sourceDir, string outputIsoPath, string oscdimgExePath, IProgressReporter reporter)
    {
        if (!ValidateBootFilesPresent(sourceDir, out string? missing))
        {
            reporter.Log($"Fichiers de boot manquants : {missing}");
            return false;
        }

        string etfsboot = Path.Combine(sourceDir, "boot", "etfsboot.com");
        string efisys = Path.Combine(sourceDir, "efi", "microsoft", "boot", "efisys.bin");

        Directory.CreateDirectory(Path.GetDirectoryName(outputIsoPath)!);
        if (File.Exists(outputIsoPath))
            File.Delete(outputIsoPath);

        string bootData = $"2#p0,e,b\"{etfsboot}\"#pEF,e,b\"{efisys}\"";
        string arguments = $"-m -o -u2 -udfver102 -bootdata:{bootData} \"{sourceDir}\" \"{outputIsoPath}\"";

        reporter.Log("Construction de l'ISO bootable en cours (cela peut prendre plusieurs minutes)...");
        reporter.SetStatus("Construction de l'ISO...");

        var psi = new ProcessStartInfo
        {
            FileName = oscdimgExePath,
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
        {
            reporter.Log($"Échec oscdimg (code {proc.ExitCode}) : {error}\n{output}");
            return false;
        }

        reporter.Log($"ISO construite : {outputIsoPath}");
        return true;
    }
}
