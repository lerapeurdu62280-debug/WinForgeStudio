using System.Text.Json.Serialization;

namespace WinForge.Models;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ProfileData))]
[JsonSerializable(typeof(JobConfig))]
[JsonSerializable(typeof(WorkspaceConfig))]
public partial class WinForgeJsonContext : JsonSerializerContext
{
}
