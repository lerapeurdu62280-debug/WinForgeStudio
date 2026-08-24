using System;
using System.IO;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

public class ProfileService
{
    public async Task SaveProfileAsync(string path, ProfileData profile)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(profile, WinForgeJsonContext.Default.ProfileData);
        await File.WriteAllTextAsync(path, json);
    }

    public async Task<ProfileData?> LoadProfileAsync(string path)
    {
        if (!File.Exists(path))
            return null;

        string json = await File.ReadAllTextAsync(path);
        return System.Text.Json.JsonSerializer.Deserialize(json, WinForgeJsonContext.Default.ProfileData);
    }

    public string EnsureWfpExtension(string path)
    {
        if (path.EndsWith(".wfp", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return path;

        return path + ".wfp";
    }
}