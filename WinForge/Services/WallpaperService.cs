using System.IO;
using System.Threading.Tasks;

namespace WinForge.Services;

public class WallpaperService
{
    // $OEM$\$$\... est copié par Windows Setup vers %WINDIR%\... après l'installation (avant
    // le premier logon) : contrairement à un patch de l'image WIM montée, les fichiers arrivent
    // avec des droits normaux, sans ACL héritée de TrustedInstaller à contourner (takeown/icacls).
    private const string OemWallpaperRelativeDir = @"$OEM$\$$\Web\Wallpaper\Windows";
    private const string OemLockscreenRelativeDir = @"$OEM$\$$\Web\Screen";

    public async Task WriteToWorkspaceAsync(string wallpaperSourcePath, string? lockscreenSourcePath, string extractedIsoRoot, IProgressReporter reporter)
    {
        if (!File.Exists(wallpaperSourcePath))
            throw new FileNotFoundException("Image de fond d'écran introuvable.", wallpaperSourcePath);

        reporter.SetStatus("Préparation du fond d'écran personnalisé...");

        string wallpaperDir = Path.Combine(extractedIsoRoot, "sources", OemWallpaperRelativeDir);
        Directory.CreateDirectory(wallpaperDir);
        string wallpaperTarget = Path.Combine(wallpaperDir, "img0.jpg");
        File.Copy(wallpaperSourcePath, wallpaperTarget, overwrite: true);
        reporter.Log($"[Wallpaper] Image de fond d'écran préparée : {wallpaperTarget}");

        if (!string.IsNullOrWhiteSpace(lockscreenSourcePath) && File.Exists(lockscreenSourcePath))
        {
            string lockscreenDir = Path.Combine(extractedIsoRoot, "sources", OemLockscreenRelativeDir);
            Directory.CreateDirectory(lockscreenDir);
            string lockscreenTarget = Path.Combine(lockscreenDir, "img100.jpg");
            File.Copy(lockscreenSourcePath, lockscreenTarget, overwrite: true);
            reporter.Log($"[Wallpaper] Image d'écran de verrouillage préparée : {lockscreenTarget}");
        }

        await Task.CompletedTask;
    }
}
