namespace SolusManifestApp.Models
{
    public class SteamUser
    {
        public string SteamId64 { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string PersonaName { get; set; } = string.Empty;
        public bool IsMostRecent { get; set; }
        public long Timestamp { get; set; }
    }
}
