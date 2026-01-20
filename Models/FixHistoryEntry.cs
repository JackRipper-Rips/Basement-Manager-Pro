using System;

namespace SolusManifestApp.Models
{
    public class FixHistoryEntry
    {
        public int AppId { get; set; }
        public string GameName { get; set; } = string.Empty;
        public string InstallPath { get; set; } = string.Empty;
        public DateTime FixDate { get; set; }
    }
}
