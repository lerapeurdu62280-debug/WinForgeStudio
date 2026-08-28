using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinForge.Models;

namespace WinForge.Services;

// Scanne le dossier SOSINFOLUDO pour proposer les installeurs "maison" à installer sur cette
// image WinForge (usage personnel/atelier uniquement — jamais une ISO destinée à un client).
public class InternalAppScannerService
{
    // Sécurité : n'importe jamais une variante "client" ni un outil de gestion de licences par
    // erreur dans une image WinForge. Un installeur est écarté dès que son chemin ou son nom de
    // fichier contient l'un de ces marqueurs, même si un installeur "Owner" légitime existe à côté.
    private static readonly string[] ExcludedMarkers = { "client", "\\dist\\client\\", "keygen", "license", "licensemanager" };

    public List<AppInstallEntry> ScanInternalApps(string sosInfoLudoRoot)
    {
        var result = new List<AppInstallEntry>();

        if (!Directory.Exists(sosInfoLudoRoot))
            return result;

        foreach (var projectDir in Directory.GetDirectories(sosInfoLudoRoot))
        {
            string projectName = Path.GetFileName(projectDir);
            string? installer = FindBestInstaller(projectDir);
            if (installer == null)
                continue;

            result.Add(new AppInstallEntry
            {
                Source = AppInstallSource.CustomInstaller,
                Name = projectName,
                InstallerPath = installer,
                SilentArgs = "/S"
            });
        }

        return result.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    // La plupart des projets Pilot publient sous dist/, mais RepairPilot utilise dist-owner/
    // (nom de dossier irrégulier) — les deux sont pris en compte pour ne pas manquer d'apps.
    private static readonly string[] DistDirNames = { "dist", "dist-owner" };

    private static string? FindBestInstaller(string projectDir)
    {
        var candidates = DistDirNames
            .Select(name => Path.Combine(projectDir, name))
            .Where(Directory.Exists)
            .SelectMany(distDir => Directory.GetFiles(distDir, "*.exe", SearchOption.AllDirectories))
            .Where(IsLikelyInstaller)
            .Where(p => !IsClientVariant(p))
            .ToList();

        if (candidates.Count == 0)
            return null;

        // Priorité aux noms explicitement "Owner" ou "Setup" (hors client) ; à défaut, le plus
        // récent — les dossiers win-unpacked contiennent l'exécutable brut, pas un installeur,
        // mais servent de repli si aucun setup packagé n'a été trouvé.
        var packaged = candidates.Where(p => !p.Contains("win-unpacked", StringComparison.OrdinalIgnoreCase)).ToList();
        var pool = packaged.Count > 0 ? packaged : candidates;

        return pool.OrderByDescending(File.GetLastWriteTimeUtc).First();
    }

    private static bool IsLikelyInstaller(string path)
    {
        string name = Path.GetFileName(path);
        // Exclut les binaires annexes embarqués par electron-builder (désinstalleurs, helpers).
        return !name.Contains("Uninstall", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("elevate.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClientVariant(string path)
    {
        string normalized = path.Replace('/', '\\').ToLowerInvariant();
        return ExcludedMarkers.Any(marker => normalized.Contains(marker));
    }
}
