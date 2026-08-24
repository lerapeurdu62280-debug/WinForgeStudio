using System.IO;
using System.Text;
using System.Threading.Tasks;
using WinForge.Services.Models;

namespace WinForge.Services;

public class AutounattendService
{
    public string GenerateXml(AutounattendOptions options)
    {
        string user = string.IsNullOrWhiteSpace(options.Username) ? "Admin" : options.Username;

        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">");

        if (options.BypassSystemRequirements)
        {
            sb.AppendLine("  <settings pass=\"windowsPE\">");
            sb.AppendLine("    <component name=\"Microsoft-Windows-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\">");
            sb.AppendLine("      <RunSynchronous>");
            AppendRegAddCommand(sb, 1, "BypassTPMCheck");
            AppendRegAddCommand(sb, 2, "BypassSecureBootCheck");
            AppendRegAddCommand(sb, 3, "BypassRAMCheck");
            AppendRegAddCommand(sb, 4, "BypassStorageCheck");
            AppendRegAddCommand(sb, 5, "BypassCPUCheck");
            sb.AppendLine("      </RunSynchronous>");
            sb.AppendLine("    </component>");
            sb.AppendLine("  </settings>");
        }

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
        sb.AppendLine("    </component>");
        sb.AppendLine("  </settings>");
        sb.AppendLine("</unattend>");

        return sb.ToString();
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
