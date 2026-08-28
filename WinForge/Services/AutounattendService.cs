using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services.Models;

namespace WinForge.Services;

public class AutounattendService
{
    // Chemin relatif dans l'arborescence $OEM$ : tout ce qui est sous $1 est copié
    // par Windows Setup vers la racine de C:\ (donc $1\Setup\Scripts -> C:\Setup\Scripts).
    private const string OemScriptsRelativeDir = @"$OEM$\$1\Setup\Scripts";
    private const string OemAppsRelativeDir = @"$OEM$\$1\Setup\Scripts\Apps";
    private const string TargetScriptsDir = @"C:\Setup\Scripts";

    public string GenerateXml(AutounattendOptions options)
    {
        string user = string.IsNullOrWhiteSpace(options.Username) ? "Admin" : options.Username;

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">");

        bool needsOemCopy = options.AppsToInstall.Count > 0;

        sb.AppendLine("  <settings pass=\"windowsPE\">");
        sb.AppendLine("    <component name=\"Microsoft-Windows-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");

        // Sans cet indicateur, Windows Setup ignore silencieusement le dossier $OEM$
        // (aucune erreur, les fichiers ne sont juste jamais copiés vers C:\).
        if (needsOemCopy)
            sb.AppendLine("      <UseConfigurationSet>true</UseConfigurationSet>");

        // Sans ceci, Windows Setup peut chercher/télécharger des mises à jour dynamiques pendant
        // l'installation, ce qui déclenche l'écran "Veuillez garder votre PC allumé et branché"
        // (attente potentiellement longue, dépendante du réseau). Toujours désactivé.
        sb.AppendLine("      <DynamicUpdate>");
        sb.AppendLine("        <Enable>false</Enable>");
        sb.AppendLine("        <WillShowUI>Never</WillShowUI>");
        sb.AppendLine("      </DynamicUpdate>");

        if (options.BypassSystemRequirements)
        {
            sb.AppendLine("      <RunSynchronous>");
            AppendRegAddCommand(sb, 1, "BypassTPMCheck");
            AppendRegAddCommand(sb, 2, "BypassSecureBootCheck");
            AppendRegAddCommand(sb, 3, "BypassRAMCheck");
            AppendRegAddCommand(sb, 4, "BypassStorageCheck");
            AppendRegAddCommand(sb, 5, "BypassCPUCheck");
            sb.AppendLine("      </RunSynchronous>");
        }

        sb.AppendLine("    </component>");
        sb.AppendLine("  </settings>");

        bool hasPassword = !string.IsNullOrEmpty(options.Password);

        sb.AppendLine("  <settings pass=\"oobeSystem\">");
        // L'attribut language (et les autres identifiants du composant) est obligatoire :
        // sans lui, oobeldr.exe rejette tout le composant ("User input error was detected in
        // unattend file"), ce qui fait échouer windeploy.exe avec le code générique 0x80220003
        // ("Windows n'a pas pu terminer l'installation") sans autre message explicite.
        sb.AppendLine("    <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
        sb.AppendLine("      <UserAccounts>");
        sb.AppendLine("        <LocalAccounts>");
        sb.AppendLine("          <LocalAccount wcm:action=\"add\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\">");
        sb.AppendLine($"            <Name>{System.Security.SecurityElement.Escape(user)}</Name>");
        sb.AppendLine("            <Group>Administrators</Group>");
        // Un compte local créé sans <Password> fait échouer la passe oobeSystem ("Windows n'a
        // pas pu terminer l'installation") dès lors que SkipUserOOBE empêche toute saisie manuelle.
        if (hasPassword)
        {
            sb.AppendLine("            <Password>");
            sb.AppendLine($"              <Value>{System.Security.SecurityElement.Escape(options.Password!)}</Value>");
            sb.AppendLine("              <PlainText>true</PlainText>");
            sb.AppendLine("            </Password>");
        }
        sb.AppendLine("          </LocalAccount>");
        sb.AppendLine("        </LocalAccounts>");
        sb.AppendLine("      </UserAccounts>");

        // AutoLogon sans mot de passe est une configuration invalide pour Windows Setup
        // (passe oobeSystem en échec, "Windows n'a pas pu terminer l'installation") : on
        // désactive silencieusement l'auto-logon plutôt que de générer un XML qui casse l'install.
        bool autoLogonValid = options.AutoLogon && !string.IsNullOrEmpty(options.Password);
        if (autoLogonValid)
        {
            sb.AppendLine("      <AutoLogon>");
            sb.AppendLine("        <Enabled>true</Enabled>");
            sb.AppendLine($"        <Username>{System.Security.SecurityElement.Escape(user)}</Username>");
            sb.AppendLine("        <Password>");
            sb.AppendLine($"          <Value>{System.Security.SecurityElement.Escape(options.Password!)}</Value>");
            sb.AppendLine("          <PlainText>true</PlainText>");
            sb.AppendLine("        </Password>");
            sb.AppendLine("      </AutoLogon>");
        }
        else
        {
            sb.AppendLine("      <AutoLogon><Enabled>false</Enabled></AutoLogon>");
        }

        if (options.SkipOobe)
        {
            // SkipMachineOOBE seul est déprécié depuis Vista et insuffisant sur Windows 11 :
            // il faut aussi neutraliser les écrans OOBE modernes (compte réseau, confidentialité).
            // SkipUserOOBE ne peut être activé que si le compte a un mot de passe : sinon Windows
            // Setup n'a aucun moyen de finaliser la création du compte (échec "Pre OOBE" silencieux,
            // "Windows n'a pas pu terminer l'installation") faute d'écran pour le saisir.
            sb.AppendLine("      <OOBE>");
            sb.AppendLine("        <HideEULAPage>true</HideEULAPage>");
            sb.AppendLine("        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>");
            sb.AppendLine("        <HideOnlineAccountScreens>true</HideOnlineAccountScreens>");
            sb.AppendLine("        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>");
            sb.AppendLine("        <ProtectYourPC>3</ProtectYourPC>");
            sb.AppendLine("        <NetworkLocation>Home</NetworkLocation>");
            sb.AppendLine("        <SkipMachineOOBE>true</SkipMachineOOBE>");
            if (hasPassword)
                sb.AppendLine("        <SkipUserOOBE>true</SkipUserOOBE>");
            sb.AppendLine("      </OOBE>");
        }
        else
        {
            sb.AppendLine("      <OOBE><SkipMachineOOBE>false</SkipMachineOOBE></OOBE>");
        }

        bool needsAssistantStartup = options.AppsToInstall.Count > 0;

        sb.AppendLine("      <FirstLogonCommands>");
        int cmdOrder = 1;

        // WinSAT (Windows System Assessment Tool) lance une évaluation matérielle (CPU/disque/RAM)
        // au tout premier démarrage sur une installation fraîche, via une tâche planifiée système —
        // désactivée systématiquement, aucune option n'expose ce comportement dans l'UI.
        sb.AppendLine("        <SynchronousCommand wcm:action=\"add\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\">");
        sb.AppendLine($"          <Order>{cmdOrder++}</Order>");
        sb.AppendLine("          <CommandLine>schtasks.exe /Change /TN \"\\Microsoft\\Windows\\Maintenance\\WinSAT\" /Disable</CommandLine>");
        sb.AppendLine("          <Description>WinForge - Désactivation de l'évaluation de performance WinSAT</Description>");
        sb.AppendLine("        </SynchronousCommand>");

        if (needsAssistantStartup)
        {
            // Le Windows App SDK Runtime n'est pas embarqué dans l'exe (voir WinForgeAssistant.csproj
            // pour le pourquoi) : il doit être installé sur la machine cible avant le premier
            // lancement, sinon l'assistant plante silencieusement (aucune fenêtre, aucune erreur
            // visible sans tracing .NET explicite). --quiet est l'option officielle Microsoft pour
            // une installation sans interaction ; le contexte SYSTEM de FirstLogonCommands a déjà
            // les droits nécessaires pour un provisioning system-wide.
            sb.AppendLine("        <SynchronousCommand wcm:action=\"add\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\">");
            sb.AppendLine($"          <Order>{cmdOrder++}</Order>");
            sb.AppendLine($"          <CommandLine>\"{TargetScriptsDir}\\Assistant\\WindowsAppRuntimeInstall-x64.exe\" --quiet</CommandLine>");
            sb.AppendLine("          <Description>WinForge - Installation du Windows App SDK Runtime (requis par WinForge Assistant)</Description>");
            sb.AppendLine("        </SynchronousCommand>");

            // Ancienne approche abandonnée n°1 : schtasks /RU SYSTEM déclenché en FirstLogonCommands.
            // SYSTEM n'a pas de profil utilisateur : winget introuvable, installeurs perMachine:false
            // installés dans un profil invisible — problèmes qui rendaient cette approche fragile,
            // pas FirstLogonCommands en soi.
            //
            // Ancienne approche abandonnée n°2 : raccourci dans le dossier Startup. Fonctionnait,
            // mais l'assistant n'apparaissait qu'APRÈS l'arrivée sur le bureau (demande explicite de
            // l'utilisateur : le voir AVANT). Le dossier Startup ne se déclenche qu'à l'ouverture de
            // session, donc toujours après le rendu du bureau.
            //
            // Approche actuelle : lancer WinForgeAssistant.exe directement en SynchronousCommand.
            // FirstLogonCommands (passe oobeSystem) s'exécute dans le contexte du premier utilisateur
            // en cours de création, pas SYSTEM — contrairement à RunSynchronousCommand en passe
            // specialize. SynchronousCommand attend la fermeture du process avant de continuer :
            // Windows Setup n'affiche donc le bureau qu'une fois l'assistant fermé (comportement
            // voulu, l'utilisateur voit l'assistant tourner avant le bureau).
            sb.AppendLine("        <SynchronousCommand wcm:action=\"add\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\">");
            sb.AppendLine($"          <Order>{cmdOrder++}</Order>");
            sb.AppendLine($"          <CommandLine>\"{TargetScriptsDir}\\Assistant\\WinForgeAssistant.exe\"</CommandLine>");
            sb.AppendLine("          <Description>WinForge - Lancement de WinForge Assistant avant l'arrivée sur le bureau</Description>");
            sb.AppendLine("        </SynchronousCommand>");
        }

        sb.AppendLine("      </FirstLogonCommands>");

        sb.AppendLine("    </component>");
        sb.AppendLine("  </settings>");
        sb.AppendLine("</unattend>");

        return sb.ToString();
    }

    // Nom de fichier utilisé à la fois pour copier l'installeur dans l'ISO et pour le référencer
    // depuis WinForge Assistant : préfixé par son index quand plusieurs installeurs custom
    // partagent le même nom de fichier (ex. deux "setup.exe" d'outils différents).
    private static string GetCopiedInstallerFileName(AppInstallEntry app, int index)
        => $"{index:D2}_{Path.GetFileName(app.InstallerPath)}";

    // Copie les installeurs custom (ajouts manuels) dans $OEM$\$1\Setup\Scripts\Apps.
    // Windows Setup copie automatiquement tout $OEM$\$1\... vers C:\ pendant l'installation, avant
    // même le premier logon. WinForge Assistant (voir WriteAssistantToWorkspaceAsync) scanne ensuite
    // ce même dossier une fois lancé dans la session utilisateur, pour proposer ces installeurs.
    public Task WriteAppsToWorkspaceAsync(List<AppInstallEntry> apps, string extractedIsoRoot, IProgressReporter reporter)
    {
        if (apps.Count == 0)
            return Task.CompletedTask;

        string appsDir = Path.Combine(extractedIsoRoot, "sources", OemAppsRelativeDir);
        Directory.CreateDirectory(appsDir);

        int customIndex = 0;
        foreach (var app in apps)
        {
            if (app.Source != AppInstallSource.CustomInstaller || string.IsNullOrWhiteSpace(app.InstallerPath))
                continue;

            if (!File.Exists(app.InstallerPath))
            {
                reporter.Log($"Installeur introuvable, ignoré : {app.InstallerPath}");
                customIndex++;
                continue;
            }

            string destPath = Path.Combine(appsDir, GetCopiedInstallerFileName(app, customIndex));
            File.Copy(app.InstallerPath, destPath, overwrite: true);
            reporter.Log($"Installeur copié dans l'ISO : {destPath}");
            customIndex++;
        }

        return Task.CompletedTask;
    }

    // Chemin du build de WinForge Assistant (projet séparé, voir C:\Dev\WinForgeAssistant\README.md).
    // Publié via : dotnet publish -c Release -p:Platform=x64 -r win-x64 --self-contained true
    // WindowsAppSDKSelfContained volontairement PAS activé (voir WinForgeAssistant.csproj) : casse
    // le chargement CsWinRT sur le SDK 2.3.1 avec .NET 8 (TypeLoadException sur ComInterfaceEntry,
    // reproduit de façon stable). Le Windows App SDK Runtime est donc installé séparément, via le
    // redistribuable officiel Microsoft, avant le premier lancement de l'assistant.
    private const string AssistantPublishDir = @"C:\Dev\WinForgeAssistant\WinForgeAssistant\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish";
    private const string AssistantRuntimeInstallerPath = @"C:\Dev\WinForgeAssistant\Redist\WindowsAppRuntimeInstall-x64.exe";
    private const string OemAssistantRelativeDir = @"$OEM$\$1\Setup\Scripts\Assistant";

    // Copie le build de WinForge Assistant (+ le redistribuable Windows App SDK Runtime) dans l'ISO
    // et prépare un raccourci .lnk vers son exe. FirstLogonCommands se contente de copier ce .lnk
    // déjà prêt vers le dossier Startup de l'utilisateur (voir GenerateXml) : générer un .lnk
    // dynamiquement en pleine passe oobeSystem serait plus fragile que de le construire ici, au
    // moment du build, avec le chemin cible connu.
    public Task WriteAssistantToWorkspaceAsync(string extractedIsoRoot, IProgressReporter reporter)
    {
        if (!Directory.Exists(AssistantPublishDir))
        {
            reporter.Log($"WinForge Assistant introuvable ({AssistantPublishDir}), non inclus dans l'ISO. " +
                "Publier le projet avec : dotnet publish -c Release -p:Platform=x64 -r win-x64 --self-contained true");
            return Task.CompletedTask;
        }

        string assistantDestDir = Path.Combine(extractedIsoRoot, "sources", OemAssistantRelativeDir);
        Directory.CreateDirectory(assistantDestDir);

        foreach (string sourceFile in Directory.GetFiles(AssistantPublishDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(AssistantPublishDir, sourceFile);
            string destFile = Path.Combine(assistantDestDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Copy(sourceFile, destFile, overwrite: true);
        }
        reporter.Log($"WinForge Assistant copié dans l'ISO : {assistantDestDir}");

        if (File.Exists(AssistantRuntimeInstallerPath))
        {
            string runtimeDest = Path.Combine(assistantDestDir, "WindowsAppRuntimeInstall-x64.exe");
            File.Copy(AssistantRuntimeInstallerPath, runtimeDest, overwrite: true);
            reporter.Log($"Redistribuable Windows App SDK Runtime copié dans l'ISO : {runtimeDest}");
        }
        else
        {
            reporter.Log($"Redistribuable Windows App SDK Runtime introuvable ({AssistantRuntimeInstallerPath}) : " +
                "l'assistant risque de ne pas se lancer sur une machine sans ce runtime déjà installé.");
        }

        return Task.CompletedTask;
    }

    private static void AppendRegAddCommand(StringBuilder sb, int order, string valueName)
    {
        sb.AppendLine("        <RunSynchronousCommand wcm:action=\"add\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\">");
        sb.AppendLine($"          <Order>{order}</Order>");
        sb.AppendLine($"          <Path>reg.exe add HKLM\\SYSTEM\\Setup\\LabConfig /v {valueName} /t REG_DWORD /d 1 /f</Path>");
        sb.AppendLine("        </RunSynchronousCommand>");
    }

    public async Task WriteToWorkspaceAsync(string xml, string extractedIsoRoot, IProgressReporter reporter)
    {
        string path = Path.Combine(extractedIsoRoot, "autounattend.xml");
        await File.WriteAllTextAsync(path, xml, Encoding.UTF8);
        reporter.Log($"autounattend.xml écrit : {path}");
    }
}
