using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace SolusManifestApp.Services
{
    public class SteamApp
    {
        [JsonProperty("appid")]
        public int AppId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class SteamAppList
    {
        [JsonProperty("apps")]
        public List<SteamApp> Apps { get; set; } = new();
    }

    public class SteamApiResponse
    {
        [JsonProperty("applist")]
        public SteamAppList? AppList { get; set; }

        [JsonProperty("response")]
        public SteamStoreServiceResponse? Response { get; set; }
    }

    // New IStoreService/GetAppList response models
    public class SteamStoreServiceResponse
    {
        [JsonProperty("apps")]
        public List<SteamStoreApp> Apps { get; set; } = new();

        [JsonProperty("have_more_results")]
        public bool HaveMoreResults { get; set; }

        [JsonProperty("last_appid")]
        public int LastAppId { get; set; }
    }

    public class SteamStoreApp
    {
        [JsonProperty("appid")]
        public int AppId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("last_modified")]
        public long LastModified { get; set; }

        [JsonProperty("price_change_number")]
        public long PriceChangeNumber { get; set; }
    }

    // Steam Store Search Models
    public class SteamStoreSearchItem
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("tiny_image")]
        public string TinyImage { get; set; } = string.Empty;

        [JsonProperty("capsule_image")]
        public string CapsuleImage { get; set; } = string.Empty;

        [JsonProperty("header_image")]
        public string HeaderImage { get; set; } = string.Empty;

        [JsonProperty("metascore")]
        public string Metascore { get; set; } = string.Empty;

        [JsonProperty("price")]
        public SteamPrice? Price { get; set; }
    }

    public class SteamPrice
    {
        [JsonProperty("currency")]
        public string Currency { get; set; } = string.Empty;

        [JsonProperty("initial")]
        public int Initial { get; set; }

        [JsonProperty("final")]
        public int Final { get; set; }

        [JsonProperty("discount_percent")]
        public int DiscountPercent { get; set; }

        [JsonProperty("initial_formatted")]
        public string InitialFormatted { get; set; } = string.Empty;

        [JsonProperty("final_formatted")]
        public string FinalFormatted { get; set; } = string.Empty;
    }

    public class SteamStoreSearchResponse
    {
        [JsonProperty("items")]
        public List<SteamStoreSearchItem> Items { get; set; } = new();

        [JsonProperty("total")]
        public int Total { get; set; }
    }

    public class SteamApiService
    {
        private readonly HttpClient _httpClient;
        private readonly CacheService _cacheService;
        private SteamApiResponse? _cachedData;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromDays(7); // Cache for 7 days
        private string? _steamWebApiKey;

        public SteamApiService(CacheService cacheService, string? steamWebApiKey = null)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            // Add headers to mimic browser requests
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _cacheService = cacheService;
            _steamWebApiKey = steamWebApiKey;
        }

        // For cases where CacheService is not available (backward compatibility)
        public SteamApiService()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _cacheService = new CacheService();
            _steamWebApiKey = null;
        }

        public void SetSteamWebApiKey(string? apiKey)
        {
            _steamWebApiKey = apiKey;
        }

        public async Task<SteamApiResponse?> GetAppListAsync(bool forceRefresh = false)
        {
            // Return in-memory cache if available
            if (!forceRefresh && _cachedData != null)
            {
                return _cachedData;
            }

            // Check disk cache
            if (!forceRefresh && _cacheService.IsSteamAppListCacheValid(_cacheExpiration))
            {
                var (cachedJson, _) = _cacheService.GetCachedSteamAppList();
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    try
                    {
                        _cachedData = JsonConvert.DeserializeObject<SteamApiResponse>(cachedJson);
                        return _cachedData;
                    }
                    catch { }
                }
            }

            // If no API key provided, skip API call and return empty result
            // The app will rely on manifest cache and other sources for game names
            if (string.IsNullOrWhiteSpace(_steamWebApiKey))
            {
                // Try to return stale cache if available
                var (cachedJson, _) = _cacheService.GetCachedSteamAppList();
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    try
                    {
                        _cachedData = JsonConvert.DeserializeObject<SteamApiResponse>(cachedJson);
                        return _cachedData;
                    }
                    catch { }
                }

                // Return empty response - game names will come from other sources
                return new SteamApiResponse
                {
                    AppList = new SteamAppList
                    {
                        Apps = new List<SteamApp>()
                    }
                };
            }

            // Fetch from API using new IStoreService endpoint
            try
            {
                // Note: The new endpoint uses paginated results
                // We'll fetch all pages to get the complete list
                var allApps = new List<SteamApp>();
                int lastAppId = 0;
                bool haveMoreResults = true;
                int maxPages = 100; // Safety limit to prevent infinite loops
                int pageCount = 0;

                while (haveMoreResults && pageCount < maxPages)
                {
                    var url = lastAppId > 0
                        ? $"https://api.steampowered.com/IStoreService/GetAppList/v1/?key={_steamWebApiKey}&include_games=true&include_dlc=true&max_results=50000&last_appid={lastAppId}"
                        : $"https://api.steampowered.com/IStoreService/GetAppList/v1/?key={_steamWebApiKey}&include_games=true&include_dlc=true&max_results=50000";

                    var response = await _httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    var json = await response.Content.ReadAsStringAsync();
                    var pageData = JsonConvert.DeserializeObject<SteamApiResponse>(json);

                    if (pageData?.Response?.Apps != null && pageData.Response.Apps.Count > 0)
                    {
                        // Convert SteamStoreApp to SteamApp
                        foreach (var storeApp in pageData.Response.Apps)
                        {
                            allApps.Add(new SteamApp
                            {
                                AppId = storeApp.AppId,
                                Name = storeApp.Name
                            });
                        }

                        haveMoreResults = pageData.Response.HaveMoreResults;
                        lastAppId = pageData.Response.LastAppId;
                        pageCount++;
                    }
                    else
                    {
                        break;
                    }
                }

                // Build final response in old format for backward compatibility
                var data = new SteamApiResponse
                {
                    AppList = new SteamAppList
                    {
                        Apps = allApps
                    }
                };

                // Cache to memory
                _cachedData = data;

                // Cache to disk
                var cacheJson = JsonConvert.SerializeObject(data);
                _cacheService.CacheSteamAppList(cacheJson);

                return data;
            }
            catch (Exception ex)
            {
                // Try to return stale cache if API fails
                var (cachedJson, _) = _cacheService.GetCachedSteamAppList();
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    try
                    {
                        _cachedData = JsonConvert.DeserializeObject<SteamApiResponse>(cachedJson);
                        return _cachedData;
                    }
                    catch { }
                }

                throw new Exception($"Failed to fetch Steam app list: {ex.Message}", ex);
            }
        }

        public string GetGameName(string appId, SteamApiResponse? steamData = null)
        {
            var data = steamData ?? _cachedData;

            if (data == null)
                return "Unknown Game";

            var app = data.AppList.Apps.FirstOrDefault(a => a.AppId.ToString() == appId);
            return app?.Name ?? "Unknown Game";
        }

        public async Task<string> GetGameNameAsync(string appId)
        {
            var data = await GetAppListAsync();
            return GetGameName(appId, data);
        }

        public Dictionary<string, string> BuildAppIdToNameDictionary(SteamApiResponse? steamData = null)
        {
            var data = steamData ?? _cachedData;

            if (data == null)
                return new Dictionary<string, string>();

            return data.AppList.Apps.ToDictionary(
                app => app.AppId.ToString(),
                app => app.Name
            );
        }

        // Steam Store Search - Matching your bot's implementation
        public async Task<SteamStoreSearchResponse?> SearchStoreAsync(string searchTerm, int limit = 25)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return null;

            try
            {
                var cleanedTerm = searchTerm.Trim();

                // Build URL exactly like the working browser URL
                var baseUrl = "https://store.steampowered.com/api/storesearch/";
                var queryParams = new Dictionary<string, string>
                {
                    { "term", cleanedTerm },
                    { "l", "english" },
                    { "cc", "US" },
                    { "realm", "1" },
                    { "origin", "https://store.steampowered.com" },
                    { "f", "jsonfull" },
                    { "start", "0" },
                    { "count", Math.Min(limit * 3, 50).ToString() }
                };

                var queryString = string.Join("&", queryParams.Select(kvp =>
                    $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
                var fullUrl = $"{baseUrl}?{queryString}";

                var response = await _httpClient.GetAsync(fullUrl);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Steam API returned {response.StatusCode}: {errorContent}. URL: {fullUrl}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonConvert.DeserializeObject<SteamStoreSearchResponse>(json);

                return searchResponse;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to search Steam store: {ex.Message}", ex);
            }
        }

        // Cache search results
        public async Task<SteamStoreSearchResponse?> SearchStoreWithCacheAsync(string searchTerm, int limit = 25)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return null;

            var cacheKey = $"search_{searchTerm.ToLower().Trim()}_{limit}";

            // Check cache first
            if (_cacheService.IsGameStatusCacheValid(cacheKey, TimeSpan.FromMinutes(30)))
            {
                var (cachedJson, _) = _cacheService.GetCachedGameStatus(cacheKey);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<SteamStoreSearchResponse>(cachedJson);
                    }
                    catch { }
                }
            }

            // Fetch from API
            var result = await SearchStoreAsync(searchTerm, limit);

            // Cache result
            if (result != null)
            {
                var json = JsonConvert.SerializeObject(result);
                _cacheService.CacheGameStatus(cacheKey, json);
            }

            return result;
        }
    }
}
