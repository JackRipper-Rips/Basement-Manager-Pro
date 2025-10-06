using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace SolusManifestApp.Services
{
    public class UpdateInfo
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("body")]
        public string Body { get; set; } = string.Empty;

        [JsonProperty("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;

        [JsonProperty("published_at")]
        public DateTime PublishedAt { get; set; }
    }

    public class UpdateService
    {
        private readonly HttpClient _httpClient;
        private const string GitHubApiUrl = "https://api.github.com/repos/{owner}/{repo}/releases/latest";
        private string _owner = "YourGitHubUsername"; // TODO: Replace with actual username
        private string _repo = "MorrenusApp"; // TODO: Replace with actual repo name

        public UpdateService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "MorrenusApp");
        }

        public void SetRepository(string owner, string repo)
        {
            _owner = owner;
            _repo = repo;
        }

        public string GetCurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }

        public async Task<(bool hasUpdate, UpdateInfo? updateInfo)> CheckForUpdatesAsync()
        {
            try
            {
                var url = GitHubApiUrl.Replace("{owner}", _owner).Replace("{repo}", _repo);
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return (false, null);
                }

                var json = await response.Content.ReadAsStringAsync();
                var updateInfo = JsonConvert.DeserializeObject<UpdateInfo>(json);

                if (updateInfo == null)
                {
                    return (false, null);
                }

                var currentVersion = GetCurrentVersion();
                var latestVersion = updateInfo.TagName.TrimStart('v');

                var hasUpdate = CompareVersions(currentVersion, latestVersion) < 0;

                return (hasUpdate, updateInfo);
            }
            catch
            {
                return (false, null);
            }
        }

        private int CompareVersions(string current, string latest)
        {
            try
            {
                var currentParts = current.Split('.').Select(int.Parse).ToArray();
                var latestParts = latest.Split('.').Select(int.Parse).ToArray();

                var maxLength = Math.Max(currentParts.Length, latestParts.Length);

                for (int i = 0; i < maxLength; i++)
                {
                    var currentPart = i < currentParts.Length ? currentParts[i] : 0;
                    var latestPart = i < latestParts.Length ? latestParts[i] : 0;

                    if (currentPart < latestPart)
                        return -1;
                    if (currentPart > latestPart)
                        return 1;
                }

                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
