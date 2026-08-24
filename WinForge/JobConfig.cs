using System.Text.Json;
using System.IO;

namespace WinForge.Models
{
    public class WorkspaceConfig
    {
        public string Root { get; set; } = @"C:\WinForge\Work";
        public string IsoExtractDir { get; set; } = @"C:\WinForge\Work\ISO_Source";
        public string MountDir { get; set; } = @"C:\WinForge\Work\Mount";
    }

    public class JobConfig
    {
        public string SourceIsoPath { get; set; } = "";
        public string OutputIsoPath { get; set; } = "";
        public int EditionIndex { get; set; } = 1;

        public WorkspaceConfig Workspace { get; set; } = new WorkspaceConfig();

        public void SaveToFile(string path)
        {
            string json = JsonSerializer.Serialize(this, WinForgeJsonContext.Default.JobConfig);
            File.WriteAllText(path, json);
        }
    }
}