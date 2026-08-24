using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WinForge.Services.Models;

namespace WinForge.Services;

public class DismService
{
    private static string DismPath => Path.Combine(Environment.SystemDirectory, "dism.exe");

    public Task<DismOperationResult> MountImageAsync(string wimPath, int index, string mountDir, IProgressReporter reporter)
    {
        Directory.CreateDirectory(mountDir);
        reporter.Log($"Montage de l'image (index {index}) sur {mountDir}...");
        return RunDismAsync(
            $"/Mount-Image /ImageFile:\"{wimPath}\" /Index:{index} /MountDir:\"{mountDir}\"",
            reporter);
    }

    public async Task<List<string>> GetProvisionedAppxPackagesAsync(string mountDir, IProgressReporter reporter)
    {
        reporter.Log("Lecture des applications provisionnées...");
        var result = await RunDismAsync($"/Image:\"{mountDir}\" /Get-ProvisionedAppxPackages", reporter, reportFailure: true);

        // DISM sur système localisé (FR) insère un caractère U+FEFF avant les ":" des libellés,
        // qui devient un U+FFFD après capture via redirection de process : "PackageName<?>:".
        var names = new List<string>();
        foreach (Match m in Regex.Matches(result.RawOutput, @"PackageName.?:\s*(.+)"))
            names.Add(m.Groups[1].Value.Trim());

        reporter.Log($"{names.Count} application(s) provisionnée(s) trouvée(s).");
        return names;
    }

    public Task<DismOperationResult> RemoveProvisionedAppxPackageAsync(string mountDir, string packageFullName, IProgressReporter reporter)
    {
        reporter.Log($"Suppression de {packageFullName}...");
        return RunDismAsync(
            $"/Image:\"{mountDir}\" /Remove-ProvisionedAppxPackage /PackageName:\"{packageFullName}\"",
            reporter);
    }

    public Task<DismOperationResult> AddDriverAsync(string mountDir, string infPath, IProgressReporter reporter)
    {
        reporter.Log($"Injection du pilote {Path.GetFileName(infPath)}...");
        return RunDismAsync(
            $"/Image:\"{mountDir}\" /Add-Driver /Driver:\"{infPath}\" /Recurse",
            reporter);
    }

    public Task<DismOperationResult> AddPackageAsync(string mountDir, string cabOrMsuPath, IProgressReporter reporter)
    {
        reporter.Log($"Injection de la mise à jour {Path.GetFileName(cabOrMsuPath)}...");
        return RunDismAsync(
            $"/Image:\"{mountDir}\" /Add-Package /PackagePath:\"{cabOrMsuPath}\"",
            reporter);
    }

    public Task<DismOperationResult> UnmountImageAsync(string mountDir, bool commit, IProgressReporter reporter)
    {
        string verb = commit ? "/Commit" : "/Discard";
        reporter.Log($"Démontage de l'image ({(commit ? "commit" : "discard")})...");
        return RunDismAsync($"/Unmount-Image /MountDir:\"{mountDir}\" {verb}", reporter);
    }

    public async Task<bool> IsMountedAsync(string mountDir)
    {
        var reporter = new NullProgressReporter();
        var result = await RunDismAsync("/Get-MountedImageInfo", reporter);
        return result.RawOutput.Contains(mountDir, StringComparison.OrdinalIgnoreCase);
    }

    public Task<DismOperationResult> CleanupOrphanedMountsAsync(IProgressReporter reporter)
    {
        reporter.Log("Nettoyage des points de montage orphelins...");
        return RunDismAsync("/Cleanup-Mountpoints", reporter);
    }

    private static async Task<DismOperationResult> RunDismAsync(string arguments, IProgressReporter reporter, bool reportFailure = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = DismPath,
            // /English force la sortie DISM en anglais quelle que soit la langue du système hôte,
            // pour que le parsing par regex (PackageName/Index/Name/...) reste fiable.
            Arguments = "/English " + arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        var output = new StringBuilder();

        using var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null)
                return;

            output.AppendLine(e.Data);

            var progressMatch = Regex.Match(e.Data, @"(\d+(?:\.\d+)?)\s*%");
            if (progressMatch.Success && double.TryParse(progressMatch.Groups[1].Value, out double pct))
            {
                try
                {
                    reporter.SetProgress(Math.Clamp(pct, 0, 100));
                }
                catch
                {
                    // Ne jamais laisser un souci de mise à jour UI faire échouer le job DISM en cours.
                }
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                output.AppendLine(e.Data);
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync();

        bool success = proc.ExitCode == 0;
        string raw = output.ToString();

        if (!success && reportFailure)
            reporter.Log($"Échec DISM (code {proc.ExitCode}).");

        return new DismOperationResult { Success = success, ExitCode = proc.ExitCode, RawOutput = raw };
    }

    private sealed class NullProgressReporter : IProgressReporter
    {
        public void Log(string message) { }
        public void SetStatus(string message) { }
        public void SetProgress(double value) { }
    }
}
