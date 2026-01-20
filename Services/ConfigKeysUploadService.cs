using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SolusManifestApp.Models;
using SolusManifestApp.Tools.ConfigVdfKeyExtractor;

namespace SolusManifestApp.Services
{
    public class ConfigKeysUploadService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly LoggerService _loggerService;
        private readonly NotificationService _notificationService;
        private readonly SteamService _steamService;
        private Timer? _uploadTimer;
        private bool _isUploading = false;
        private readonly TimeSpan _uploadInterval = TimeSpan.FromHours(1); // Upload every hour

        public ConfigKeysUploadService(SettingsService settingsService, LoggerService loggerService, NotificationService notificationService, SteamService steamService)
        {
            _settingsService = settingsService;
            _loggerService = loggerService;
            _notificationService = notificationService;
            _steamService = steamService;
        }

        public void Start()
        {
            var settings = _settingsService.LoadSettings();

            // Check if either Morrenus or Basement upload is enabled
            bool anyUploadEnabled = (settings.SelectedStore == StoreProvider.Morrenus && settings.AutoUploadConfigKeys) ||
                                   (settings.SelectedStore == StoreProvider.Basement && settings.BasementAutoUploadConfigKeys);

            if (!anyUploadEnabled)
            {
                _loggerService.Log("INFO", "Config keys auto-upload is disabled");
                return; // Feature disabled
            }

            _loggerService.Log("INFO", $"Config keys auto-upload service started for {settings.SelectedStore}");

            // Upload immediately on startup
            _ = UploadNewKeysAsync();

            // Then schedule periodic uploads
            _uploadTimer = new Timer(
                async _ => await UploadNewKeysAsync(),
                null,
                _uploadInterval,
                _uploadInterval
            );
        }

        public void Stop()
        {
            _uploadTimer?.Dispose();
            _uploadTimer = null;
        }

        private async Task UploadNewKeysAsync()
        {
            if (_isUploading)
            {
                _loggerService.Log("INFO", "Config keys upload already in progress, skipping...");
                return;
            }

            _isUploading = true;

            try
            {
                var settings = _settingsService.LoadSettings();

                // Determine which store to use and check if upload is enabled
                bool uploadEnabled = settings.SelectedStore == StoreProvider.Basement
                    ? settings.BasementAutoUploadConfigKeys
                    : settings.AutoUploadConfigKeys;

                if (!uploadEnabled)
                {
                    return; // Feature was disabled
                }

                // Get the appropriate API key and last upload time
                string apiKey = settings.SelectedStore == StoreProvider.Basement
                    ? settings.BasementApiKey
                    : settings.ApiKey;

                DateTime lastUpload = settings.SelectedStore == StoreProvider.Basement
                    ? settings.BasementLastConfigKeysUpload
                    : settings.LastConfigKeysUpload;

                // Check if enough time has passed since last upload
                var timeSinceLastUpload = DateTime.Now - lastUpload;
                if (timeSinceLastUpload < _uploadInterval)
                {
                    var remainingTime = _uploadInterval - timeSinceLastUpload;
                    _loggerService.Log("INFO", $"Skipping config keys upload - next upload in {remainingTime.TotalMinutes:F0} minutes");
                    return;
                }

                if (string.IsNullOrEmpty(apiKey))
                {
                    _loggerService.Log("INFO", $"Cannot upload config keys: {settings.SelectedStore} API key not configured");
                    // Don't show notification for missing API key on every check - user may not want to configure it
                    return;
                }

                // Step 1: Get Steam directory and build config.vdf path
                string steamPath = _steamService.GetSteamPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    _loggerService.Log("INFO", "Cannot upload config keys: Steam directory not detected");
                    _notificationService.ShowNotification("Config Keys Check", "Steam directory not detected. Please set your Steam path in Settings.", NotificationType.Info);
                    return;
                }

                string configVdfPath = Path.Combine(steamPath, "config", "config.vdf");
                if (!File.Exists(configVdfPath))
                {
                    _loggerService.Log("INFO", $"Config.vdf not found at: {configVdfPath}");
                    _notificationService.ShowNotification("Config Keys Check", "Steam config.vdf file not found. Make sure Steam has been run at least once.", NotificationType.Info);
                    return;
                }

                _loggerService.Log("INFO", "Extracting depot keys from config.vdf...");
                var extractionResult = VdfKeyExtractor.ExtractKeysFromVdf(configVdfPath, null);

                if (!extractionResult.Success || extractionResult.Keys.Count == 0)
                {
                    _loggerService.Log("INFO", "No new keys found in config.vdf");
                    return;
                }

                _loggerService.Log("INFO", $"Extracted {extractionResult.Keys.Count} keys from config.vdf");

                // Step 2: Get existing depot IDs from server
                _loggerService.Log("INFO", "Fetching existing depot IDs from server...");
                var existingDepotIds = await GetExistingDepotIdsAsync(apiKey, settings.SelectedStore, settings.BasementApiUrl);

                if (existingDepotIds == null)
                {
                    _loggerService.Log("INFO", "Failed to fetch existing depot IDs from server");
                    return;
                }

                _loggerService.Log("INFO", $"Server has {existingDepotIds.Count} existing depot IDs");

                // Step 3: Filter to only new keys
                var newKeys = extractionResult.Keys
                    .Where(kvp => !existingDepotIds.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                if (newKeys.Count == 0)
                {
                    _loggerService.Log("INFO", "No new keys to upload - all keys already exist on server");
                    _notificationService.ShowNotification("Config Keys Check", "No new depot keys to upload. All keys are already on the server.", NotificationType.Info);
                    return;
                }

                _loggerService.Log("INFO", $"Found {newKeys.Count} new keys to upload");

                // Show notification that upload is starting
                string storeName = settings.SelectedStore == StoreProvider.Basement ? "Basement" : "Morrenus";
                _notificationService.ShowNotification("Config Keys Upload", $"Uploading {newKeys.Count} new depot keys to {storeName}...", NotificationType.Info);

                // Step 4: Save to uniquely named file using machine name and timestamp
                string machineName = Environment.MachineName.Replace(" ", "_");
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
                string fileName = $"{machineName}_{timestamp}_keys.txt";
                string tempPath = Path.Combine(Path.GetTempPath(), fileName);

                string keyContent = VdfKeyExtractor.FormatKeysAsText(newKeys);
                await File.WriteAllTextAsync(tempPath, keyContent);

                _loggerService.Log("INFO", $"Saved new keys to: {fileName}");

                // Step 5: Upload to server
                _loggerService.Log("INFO", "Uploading new keys to server...");
                var uploadResult = await UploadKeysFileAsync(tempPath, apiKey, settings.SelectedStore, settings.BasementApiUrl);

                if (uploadResult.Success)
                {
                    _loggerService.Log("INFO", $"Successfully uploaded {newKeys.Count} new keys! ({uploadResult.Message})");
                    _notificationService.ShowSuccess($"Successfully uploaded {newKeys.Count} new depot keys!", "Config Keys Upload");

                    // Update last upload timestamp based on selected store
                    if (settings.SelectedStore == StoreProvider.Basement)
                    {
                        settings.BasementLastConfigKeysUpload = DateTime.Now;
                    }
                    else
                    {
                        settings.LastConfigKeysUpload = DateTime.Now;
                    }
                    _settingsService.SaveSettings(settings);

                    // Clean up temp file
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch { /* Ignore cleanup errors */ }
                }
                else
                {
                    _loggerService.Log("ERROR", $"Failed to upload keys: {uploadResult.Message}");
                    _notificationService.ShowError($"Failed to upload keys: {uploadResult.Message}", "Config Keys Upload Failed");
                }
            }
            catch (Exception ex)
            {
                _loggerService.Log("ERROR", $"Error during config keys upload: {ex.Message}");
                _notificationService.ShowError($"Error during config keys upload: {ex.Message}", "Config Keys Upload Error");
            }
            finally
            {
                _isUploading = false;
            }
        }

        private async Task<HashSet<string>?> GetExistingDepotIdsAsync(string apiKey, StoreProvider storeProvider, string basementApiUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                // Determine the API endpoint based on store provider
                string apiUrl = storeProvider == StoreProvider.Basement
                    ? $"{basementApiUrl}/depot-keys"
                    : "https://manifest.morrenus.xyz/api/v1/depot-keys";

                var response = await httpClient.GetAsync(apiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var jsonDoc = JsonDocument.Parse(content);

                // Use existing_depot_ids (keys actually in combinedkeys.key file)
                // Not pending_depot_ids (keys uploaded but not yet in file)
                if (jsonDoc.RootElement.TryGetProperty("existing_depot_ids", out var depotIdsElement))
                {
                    var depotIds = new HashSet<string>();
                    foreach (var id in depotIdsElement.EnumerateArray())
                    {
                        depotIds.Add(id.GetString() ?? "");
                    }
                    return depotIds;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<(bool Success, string Message)> UploadKeysFileAsync(string filePath, string apiKey, StoreProvider storeProvider, string basementApiUrl)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                using var form = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(filePath));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

                string fileName = Path.GetFileName(filePath);
                form.Add(fileContent, "file", fileName);

                // Determine the API endpoint based on store provider
                string apiUrl = storeProvider == StoreProvider.Basement
                    ? $"{basementApiUrl}/upload-machine-keys"
                    : "https://manifest.morrenus.xyz/api/v1/upload-machine-keys";

                var response = await httpClient.PostAsync(apiUrl, form);
                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var jsonDoc = JsonDocument.Parse(responseString);
                        int validLines = jsonDoc.RootElement.TryGetProperty("valid_lines", out var validElement)
                            ? validElement.GetInt32() : 0;
                        int invalidLines = jsonDoc.RootElement.TryGetProperty("invalid_lines_removed", out var invalidElement)
                            ? invalidElement.GetInt32() : 0;

                        string message = $"{validLines} valid lines";
                        if (invalidLines > 0)
                        {
                            message += $", {invalidLines} invalid removed";
                        }

                        return (true, message);
                    }
                    catch
                    {
                        return (true, "Upload successful");
                    }
                }

                return (false, $"HTTP {(int)response.StatusCode}: {responseString}");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
