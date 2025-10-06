using SolusManifestApp.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace SolusManifestApp.Services
{
    public class ManifestApiService
    {
        private readonly HttpClient _httpClient;
        private readonly CacheService? _cacheService;
        private const string BaseUrl = "https://manifest.morrenus.xyz/api/v1";
        private readonly TimeSpan _statusCacheExpiration = TimeSpan.FromMinutes(5); // Cache status for 5 minutes

        public ManifestApiService(CacheService? cacheService = null)
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _cacheService = cacheService;
        }

        public async Task<Manifest?> GetManifestAsync(string appId, string apiKey)
        {
            try
            {
                var url = $"{BaseUrl}/manifest/{appId}?api_key={apiKey}";
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    var preview = json.Length > 200 ? json.Substring(0, 200) : json;
                    throw new Exception($"Manifest not available for App ID {appId}. API returned {response.StatusCode}: {preview}");
                }

                try
                {
                    var manifest = JsonConvert.DeserializeObject<Manifest>(json);
                    return manifest;
                }
                catch (JsonException jex)
                {
                    var preview = json.Length > 200 ? json.Substring(0, 200) : json;
                    throw new Exception($"Invalid JSON from API for App ID {appId}. Response: {preview}", jex);
                }
            }
            catch (Exception ex) when (ex is not JsonException)
            {
                throw new Exception($"Failed to fetch manifest for {appId}: {ex.Message}", ex);
            }
        }

        public async Task<List<Manifest>?> SearchGamesAsync(string query, string apiKey)
        {
            try
            {
                var url = $"{BaseUrl}/search?q={Uri.EscapeDataString(query)}&api_key={apiKey}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"API returned {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var results = JsonConvert.DeserializeObject<List<Manifest>>(json);
                return results;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to search games: {ex.Message}", ex);
            }
        }

        public async Task<List<Manifest>?> GetAllGamesAsync(string apiKey)
        {
            try
            {
                var url = $"{BaseUrl}/games?api_key={apiKey}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"API returned {response.StatusCode}");
                }

                var json = await response.Content.ReadAsStringAsync();
                var results = JsonConvert.DeserializeObject<List<Manifest>>(json);
                return results;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch games list: {ex.Message}", ex);
            }
        }

        public bool ValidateApiKey(string apiKey)
        {
            return !string.IsNullOrWhiteSpace(apiKey) && apiKey.StartsWith("smm", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> TestApiKeyAsync(string apiKey)
        {
            try
            {
                var url = $"{BaseUrl}/status/10?api_key={apiKey}";
                var response = await _httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<GameStatus?> GetGameStatusAsync(string appId, string apiKey)
        {
            // Check cache first if CacheService is available
            if (_cacheService != null && _cacheService.IsGameStatusCacheValid(appId, _statusCacheExpiration))
            {
                var (cachedJson, _) = _cacheService.GetCachedGameStatus(appId);
                if (!string.IsNullOrEmpty(cachedJson))
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<GameStatus>(cachedJson);
                    }
                    catch { }
                }
            }

            try
            {
                var url = $"{BaseUrl}/status/{appId}?api_key={apiKey}";
                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var status = JsonConvert.DeserializeObject<GameStatus>(json);

                // Cache the response
                _cacheService?.CacheGameStatus(appId, json);

                return status;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to fetch status for {appId}: {ex.Message}", ex);
            }
        }
    }
}
