using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WinForge.Services.Models;

namespace WinForge.Services;

public class OptimisationService
{
    private const string SoftwareHiveKey = @"HKLM\WF_OPT_SOFTWARE";
    private const string SystemHiveKey = @"HKLM\WF_OPT_SYSTEM";

    public async Task ApplyAsync(string mountDir, OptimisationOptions options, IProgressReporter reporter)
    {
        if (!options.AnyEnabled)
        {
            reporter.Log("[Optimisation] Aucune option activée, étape ignorée.");
            return;
        }

        string softwareHivePath = Path.Combine(mountDir, "Windows", "System32", "config", "SOFTWARE");
        string systemHivePath = Path.Combine(mountDir, "Windows", "System32", "config", "SYSTEM");

        bool softwareLoaded = false;
        bool systemLoaded = false;

        try
        {
            reporter.SetStatus("Application des optimisations (registre offline)...");

            softwareLoaded = await LoadHiveAsync(SoftwareHiveKey, softwareHivePath, reporter);
            systemLoaded = await LoadHiveAsync(SystemHiveKey, systemHivePath, reporter);

            if (options.DisableTelemetry)
                await ApplyDisableTelemetryAsync(reporter);

            if (options.DisableCortana)
                await ApplyDisableCortanaAsync(reporter);

            if (options.OptimizeServices)
                await ApplyOptimizeServicesAsync(reporter);

            if (options.PerformanceMode)
                await ApplyPerformanceModeAsync(reporter);

            reporter.Log("[Optimisation] Toutes les optimisations demandées ont été appliquées.");
        }
        finally
        {
            // Les handles de ruche doivent être libérés avant "reg unload", sinon Windows refuse (erreur 32).
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (systemLoaded)
                await UnloadHiveAsync(SystemHiveKey, reporter);
            if (softwareLoaded)
                await UnloadHiveAsync(SoftwareHiveKey, reporter);
        }
    }

    private async Task ApplyDisableTelemetryAsync(IProgressReporter reporter)
    {
        reporter.Log("[Optimisation] Désactivation de la télémétrie...");

        await RunRegAsync($@"add ""{SoftwareHiveKey}\Policies\Microsoft\Windows\DataCollection"" /v AllowTelemetry /t REG_DWORD /d 0 /f", reporter);
        await RunRegAsync($@"add ""{SoftwareHiveKey}\Microsoft\Windows\CurrentVersion\Policies\DataCollection"" /v AllowTelemetry /t REG_DWORD /d 0 /f", reporter);
        await RunRegAsync($@"add ""{SystemHiveKey}\ControlSet001\Services\DiagTrack"" /v Start /t REG_DWORD /d 4 /f", reporter);
        await RunRegAsync($@"add ""{SystemHiveKey}\ControlSet001\Services\dmwappushservice"" /v Start /t REG_DWORD /d 4 /f", reporter);
        await RunRegAsync($@"add ""{SoftwareHiveKey}\Microsoft\Windows\CurrentVersion\Policies\CloudContent"" /v DisableTailoredExperiencesWithDiagnosticData /t REG_DWORD /d 1 /f", reporter);
        await RunRegAsync($@"add ""{SoftwareHiveKey}\Microsoft\Windows\CurrentVersion\AdvertisingInfo"" /v Enabled /t REG_DWORD /d 0 /f", reporter);
    }

    private async Task ApplyDisableCortanaAsync(IProgressReporter reporter)
    {
        reporter.Log("[Optimisation] Désactivation de Cortana...");

        await RunRegAsync($@"add ""{SoftwareHiveKey}\Policies\Microsoft\Windows\Windows Search"" /v AllowCortana /t REG_DWORD /d 0 /f", reporter);
        await RunRegAsync($@"add ""{SoftwareHiveKey}\Policies\Microsoft\Windows\Windows Search"" /v CortanaConsent /t REG_DWORD /d 0 /f", reporter);
        await RunRegAsync($@"add ""{SoftwareHiveKey}\Policies\Microsoft\Windows\Windows Search"" /v DisableWebSearch /t REG_DWORD /d 1 /f", reporter);
        await RunRegAsync($@"add ""{SoftwareHiveKey}\Policies\Microsoft\Windows\Windows Search"" /v ConnectedSearchUseWeb /t REG_DWORD /d 0 /f", reporter);
    }

    private async Task ApplyOptimizeServicesAsync(IProgressReporter reporter)
    {
        reporter.Log("[Optimisation] Optimisation des services non essentiels...");

        // Start : 2 = Automatique, 3 = Manuel, 4 = Désactivé
        string[] servicesToDisable =
        {
            "DiagTrack",
            "dmwappushservice",
            "MapsBroker",
            "RetailDemo",
            "RemoteRegistry",
            "WerSvc",
            "WSearch"
        };

        foreach (var service in servicesToDisable)
        {
            await RunRegAsync($@"add ""{SystemHiveKey}\ControlSet001\Services\{service}"" /v Start /t REG_DWORD /d 4 /f", reporter);
        }

        string[] servicesToManual =
        {
            "SysMain",
            "Spooler"
        };

        foreach (var service in servicesToManual)
        {
            await RunRegAsync($@"add ""{SystemHiveKey}\ControlSet001\Services\{service}"" /v Start /t REG_DWORD /d 3 /f", reporter);
        }
    }

    private async Task ApplyPerformanceModeAsync(IProgressReporter reporter)
    {
        reporter.Log("[Optimisation] Application du mode performance...");

        // Effets visuels : 2 = ajuster pour de meilleures performances
        await RunRegAsync($@"add ""{SoftwareHiveKey}\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"" /v VisualFXSetting /t REG_DWORD /d 2 /f", reporter);

        // Désactive l'animation des fenêtres et transparence de la barre des tâches
        await RunRegAsync($@"add ""{SoftwareHiveKey}\Microsoft\Windows\CurrentVersion\Explorer\Advanced"" /v TaskbarAnimations /t REG_DWORD /d 0 /f", reporter);
        await RunRegAsync($@"add ""{SoftwareHiveKey}\Microsoft\Windows\CurrentVersion\Explorer\Advanced"" /v EnableTransparency /t REG_DWORD /d 0 /f", reporter);

        // Priorité des processus courts / réactivité du système au lieu des programmes en arrière-plan
        await RunRegAsync($@"add ""{SystemHiveKey}\ControlSet001\Control\PriorityControl"" /v Win32PrioritySeparation /t REG_DWORD /d 38 /f", reporter);

        // Désactive la mise en veille prolongée (libère de l'espace disque, comportement "perf" courant)
        await RunRegAsync($@"add ""{SystemHiveKey}\ControlSet001\Control\Power"" /v HibernateEnabled /t REG_DWORD /d 0 /f", reporter);
    }

    private async Task<bool> LoadHiveAsync(string hiveKey, string hiveFilePath, IProgressReporter reporter)
    {
        if (!File.Exists(hiveFilePath))
        {
            reporter.Log($"[Optimisation] Ruche introuvable, ignorée : {hiveFilePath}");
            return false;
        }

        var result = await RunRegAsync($@"load ""{hiveKey}"" ""{hiveFilePath}""", reporter, reportFailure: true);
        if (!result.Success)
            throw new InvalidOperationException($"Échec du chargement de la ruche registre {hiveFilePath} (code {result.ExitCode}).");

        return true;
    }

    private async Task UnloadHiveAsync(string hiveKey, IProgressReporter reporter)
    {
        const int maxAttempts = 3;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var result = await RunRegAsync($@"unload ""{hiveKey}""", reporter, reportFailure: attempt == maxAttempts);
            if (result.Success)
                return;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(500);
        }
    }

    private static async Task<DismOperationResult> RunRegAsync(string arguments, IProgressReporter reporter, bool reportFailure = false)
    {
        string regPath = Path.Combine(Environment.SystemDirectory, "reg.exe");

        var psi = new ProcessStartInfo
        {
            FileName = regPath,
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

        bool success = proc.ExitCode == 0;
        if (!success && reportFailure)
            reporter.Log($"[Optimisation] Échec reg.exe (code {proc.ExitCode}) : {error}{output}");

        return new DismOperationResult { Success = success, ExitCode = proc.ExitCode, RawOutput = output + error };
    }
}
