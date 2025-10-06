using SolusManifestApp.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace SolusManifestApp.Services
{
    public class CacheService
    {
        private readonly string _cacheFolder;
        private readonly string _iconCacheFolder;
        private readonly string _dataCacheFolder;
        private readonly HttpClient _httpClient;
        private readonly LoggerService? _logger;

        public CacheService(LoggerService? logger = null)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _cacheFolder = Path.Combine(appData, "SolusManifestApp", "Cache");
            _iconCacheFolder = Path.Combine(_cacheFolder, "Icons");
            _dataCacheFolder = Path.Combine(_cacheFolder, "Data");

            Directory.CreateDirectory(_iconCacheFolder);
            Directory.CreateDirectory(_dataCacheFolder);

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            _logger = logger;
        }

        // Icon Caching
        public async Task<string?> GetIconAsync(string appId, string iconUrl)
        {
            if (string.IsNullOrEmpty(iconUrl))
                return null;

            var iconPath = Path.Combine(_iconCacheFolder, $"{appId}.jpg");

            // Return cached icon if exists
            if (File.Exists(iconPath))
            {
                return iconPath;
            }

            // Download icon
            try
            {
                var response = await _httpClient.GetAsync(iconUrl);
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(iconPath, bytes);
                    return iconPath;
                }
            }
            catch
            {
                // Return null if download fails
            }

            return null;
        }

        public bool HasCachedIcon(string appId)
        {
            var iconPath = Path.Combine(_iconCacheFolder, $"{appId}.jpg");
            return File.Exists(iconPath);
        }

        public string? GetCachedIconPath(string appId)
        {
            var iconPath = Path.Combine(_iconCacheFolder, $"{appId}.jpg");
            return File.Exists(iconPath) ? iconPath : null;
        }

        public async Task<string?> GetSteamGameIconAsync(string appId, string? localSteamIconPath, string cdnIconUrl)
        {
            // Check if already cached
            var cachedPath = Path.Combine(_iconCacheFolder, $"steam_{appId}.jpg");
            if (File.Exists(cachedPath))
            {
                return cachedPath;
            }

            // Try to copy from Steam's local cache first
            if (!string.IsNullOrEmpty(localSteamIconPath) && File.Exists(localSteamIconPath))
            {
                try
                {
                    File.Copy(localSteamIconPath, cachedPath, overwrite: true);
                    return cachedPath;
                }
                catch
                {
                    // Fall through to CDN download
                }
            }

            // Try header images only
            var cdnUrls = new[]
            {
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg",
                $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg"
            };

            _logger?.Debug($"Trying {cdnUrls.Length} CDN URLs for AppId {appId}");

            foreach (var url in cdnUrls)
            {
                try
                {
                    _logger?.Debug($"Attempting: {url}");
                    var response = await _httpClient.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        await File.WriteAllBytesAsync(cachedPath, bytes);
                        _logger?.Info($"✓ Success! Downloaded {bytes.Length} bytes from {url}");
                        return cachedPath;
                    }
                    else
                    {
                        _logger?.Debug($"✗ Failed: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Debug($"✗ Exception: {ex.Message}");
                }
            }

            // Fallback to Steam Store API
            _logger?.Info($"All CDN URLs failed, trying Steam Store API for AppId {appId}");
            try
            {
                var storeApiUrl = $"https://store.steampowered.com/api/appdetails/?appids={appId}";
                _logger?.Debug($"Fetching: {storeApiUrl}");

                var storeResponse = await _httpClient.GetAsync(storeApiUrl);
                if (storeResponse.IsSuccessStatusCode)
                {
                    var json = await storeResponse.Content.ReadAsStringAsync();

                    // Parse JSON to get header_image or capsule_image
                    dynamic? data = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(json);
                    if (data != null && data[appId] != null && data[appId]["success"] == true)
                    {
                        var gameData = data[appId]["data"];
                        string? imageUrl = gameData["header_image"]?.ToString();

                        if (!string.IsNullOrEmpty(imageUrl))
                        {
                            _logger?.Info($"Found image URL from Steam Store API: {imageUrl}");

                            var imageResponse = await _httpClient.GetAsync(imageUrl);
                            if (imageResponse.IsSuccessStatusCode)
                            {
                                var bytes = await imageResponse.Content.ReadAsByteArrayAsync();
                                await File.WriteAllBytesAsync(cachedPath, bytes);
                                _logger?.Info($"✓ Success! Downloaded {bytes.Length} bytes from Steam Store API");
                                return cachedPath;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"Steam Store API fallback failed: {ex.Message}");
            }

            _logger?.Warning($"✗ All methods failed for AppId {appId}");
            return null;
        }

        public void ClearIconCache()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_iconCacheFolder))
                {
                    File.Delete(file);
                }
            }
            catch { }
        }

        // Data Caching for Offline Mode
        public void CacheManifests(List<Manifest> manifests)
        {
            try
            {
                var json = JsonConvert.SerializeObject(manifests, Formatting.Indented);
                var filePath = Path.Combine(_dataCacheFolder, "manifests.json");
                File.WriteAllText(filePath, json);
            }
            catch { }
        }

        public List<Manifest>? GetCachedManifests()
        {
            try
            {
                var filePath = Path.Combine(_dataCacheFolder, "manifests.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<List<Manifest>>(json);
                }
            }
            catch { }

            return null;
        }

        public void CacheManifest(Manifest manifest)
        {
            try
            {
                var json = JsonConvert.SerializeObject(manifest, Formatting.Indented);
                var filePath = Path.Combine(_dataCacheFolder, $"manifest_{manifest.AppId}.json");
                File.WriteAllText(filePath, json);
            }
            catch { }
        }

        public Manifest? GetCachedManifest(string appId)
        {
            try
            {
                var filePath = Path.Combine(_dataCacheFolder, $"manifest_{appId}.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    return JsonConvert.DeserializeObject<Manifest>(json);
                }
            }
            catch { }

            return null;
        }

        public bool IsOfflineMode()
        {
            // Check if we have cached data
            var filePath = Path.Combine(_dataCacheFolder, "manifests.json");
            return File.Exists(filePath);
        }

        public void ClearDataCache()
        {
            try
            {
                foreach (var file in Directory.GetFiles(_dataCacheFolder))
                {
                    File.Delete(file);
                }
            }
            catch { }
        }

        public void ClearAllCache()
        {
            ClearIconCache();
            ClearDataCache();
        }

        public long GetCacheSize()
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.GetFiles(_cacheFolder, "*", SearchOption.AllDirectories))
                {
                    size += new FileInfo(file).Length;
                }
            }
            catch { }

            return size;
        }

        // Steam App List Caching (for game name lookups)
        public void CacheSteamAppList(string jsonData)
        {
            try
            {
                var filePath = Path.Combine(_dataCacheFolder, "steam_applist.json");
                var cacheInfo = new
                {
                    timestamp = DateTime.Now,
                    data = jsonData
                };
                var json = JsonConvert.SerializeObject(cacheInfo, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch { }
        }

        public (string? data, DateTime? timestamp) GetCachedSteamAppList()
        {
            try
            {
                var filePath = Path.Combine(_dataCacheFolder, "steam_applist.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var obj = JsonConvert.DeserializeObject<dynamic>(json);
                    return (obj?.data?.ToString(), obj?.timestamp != null ? (DateTime)obj.timestamp : null);
                }
            }
            catch { }

            return (null, null);
        }

        public bool IsSteamAppListCacheValid(TimeSpan maxAge)
        {
            var (_, timestamp) = GetCachedSteamAppList();
            if (timestamp.HasValue)
            {
                return DateTime.Now - timestamp.Value < maxAge;
            }
            return false;
        }

        // Game Status Caching (from manifest API)
        public void CacheGameStatus(string appId, string jsonData)
        {
            try
            {
                var filePath = Path.Combine(_dataCacheFolder, $"status_{appId}.json");
                var cacheInfo = new
                {
                    timestamp = DateTime.Now,
                    data = jsonData
                };
                var json = JsonConvert.SerializeObject(cacheInfo, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch { }
        }

        public (string? data, DateTime? timestamp) GetCachedGameStatus(string appId)
        {
            try
            {
                var filePath = Path.Combine(_dataCacheFolder, $"status_{appId}.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var obj = JsonConvert.DeserializeObject<dynamic>(json);
                    return (obj?.data?.ToString(), obj?.timestamp != null ? (DateTime)obj.timestamp : null);
                }
            }
            catch { }

            return (null, null);
        }

        public bool IsGameStatusCacheValid(string appId, TimeSpan maxAge)
        {
            var (_, timestamp) = GetCachedGameStatus(appId);
            if (timestamp.HasValue)
            {
                return DateTime.Now - timestamp.Value < maxAge;
            }
            return false;
        }
    }
}
