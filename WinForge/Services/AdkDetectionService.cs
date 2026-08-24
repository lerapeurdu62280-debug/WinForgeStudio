using System;
using System.IO;
using System.Runtime.InteropServices;
using WinForge.Services.Models;

namespace WinForge.Services;

public class AdkDetectionService
{
    private const string DeploymentToolsRelativePath =
        @"Assessment and Deployment Kit\Deployment Tools";

    public AdkDetectionResult DetectOscdimg()
    {
        string? overridePath = Environment.GetEnvironmentVariable("WINFORGE_OSCDIMG_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return new AdkDetectionResult
            {
                Found = true,
                OscdimgPath = overridePath,
                AdkRootPath = Path.GetDirectoryName(overridePath)
            };
        }

        foreach (string arch in GetArchSearchOrder())
        {
            foreach (string programFiles in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)
                     })
            {
                if (string.IsNullOrEmpty(programFiles))
                    continue;

                string candidate = Path.Combine(
                    programFiles,
                    "Windows Kits", "10",
                    DeploymentToolsRelativePath,
                    arch, "Oscdimg", "oscdimg.exe");

                if (File.Exists(candidate))
                {
                    return new AdkDetectionResult
                    {
                        Found = true,
                        OscdimgPath = candidate,
                        AdkRootPath = Path.Combine(programFiles, "Windows Kits", "10")
                    };
                }
            }
        }

        return new AdkDetectionResult { Found = false };
    }

    public bool IsAdkInstalled() => DetectOscdimg().Found;

    public const string DownloadUrl = "https://learn.microsoft.com/windows-hardware/get-started/adk-install";

    private static string[] GetArchSearchOrder()
    {
        string native = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            _ => "amd64"
        };

        return native switch
        {
            "amd64" => new[] { "amd64", "x86", "arm64" },
            "x86" => new[] { "x86", "amd64", "arm64" },
            "arm64" => new[] { "arm64", "amd64", "x86" },
            _ => new[] { "amd64", "x86", "arm64" }
        };
    }
}
