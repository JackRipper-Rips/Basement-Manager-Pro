using SolusManifestApp.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SolusManifestApp.Services
{
    public class ProtocolHandlerService
    {
        private readonly DownloadService _downloadService;
        private readonly FileInstallService _fileInstallService;
        private readonly SettingsService _settingsService;
        private readonly NotificationService _notificationService;
        private readonly StoreApiFactory _storeApiFactory;
        private readonly SteamApiService _steamApiService;
        private readonly LoggerService _logger;

        public ProtocolHandlerService(
            DownloadService downloadService,
            FileInstallService fileInstallService,
            SettingsService settingsService,
            NotificationService notificationService,
            StoreApiFactory storeApiFactory,
            SteamApiService steamApiService)
        {
            _downloadService = downloadService;
            _fileInstallService = fileInstallService;
            _settingsService = settingsService;
            _notificationService = notificationService;
            _storeApiFactory = storeApiFactory;
            _steamApiService = steamApiService;
            _logger = new LoggerService("ProtocolHandler");
        }

        public async Task HandleProtocolAsync(string protocolPath)
        {
            if (string.IsNullOrEmpty(protocolPath))
                return;

            var parts = protocolPath.Split('/');
            if (parts.Length < 2)
                return;

            var action = parts[0].ToLower();

            // Handle different URL formats:
            // download/install/400
            // download/400
            // install/400
            if (action == "download" && parts.Length >= 3 && parts[1].ToLower() == "install")
            {
                // download/install/appid
                await HandleDownloadAndInstall(parts[2], true);
            }
            else if (action == "download" && parts.Length >= 2)
            {
                // download/appid
                await HandleDownload(parts[1]);
            }
            else if (action == "install" && parts.Length >= 2)
            {
                // install/appid
                await HandleInstall(parts[1]);
            }
        }

        private async Task HandleDownload(string appId)
        {
            try
            {
                _logger.Log("INFO", $"Protocol: HandleDownload called for App ID: {appId}");

                var settings = _settingsService.LoadSettings();
                var apiKey = _storeApiFactory.GetActiveApiKey();

                _logger.Log("INFO", $"Protocol: Using store: {settings.SelectedStore}, API Key length: {apiKey?.Length ?? 0}");

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.Log("ERROR", "Protocol: No API key configured");
                    _notificationService.ShowError("API key not configured. Please set it in Settings.");
                    return;
                }

                _notificationService.ShowNotification("Download Started", $"Starting download for App ID: {appId}", NotificationType.Info);

                _logger.Log("INFO", $"Protocol: Building manifest for App ID: {appId}");

                // Build manifest manually (like StoreViewModel does)
                // The Basement API's /manifest/{appId} returns the ZIP file directly, not JSON
                var baseUrl = settings.SelectedStore == StoreProvider.Basement
                    ? settings.BasementApiUrl
                    : "https://manifest.morrenus.xyz/api/v1";

                var manifest = new Manifest
                {
                    AppId = appId,
                    Name = $"App {appId}",
                    IconUrl = string.Empty,
                    Size = 0,
                    DownloadUrl = $"{baseUrl}/manifest/{appId}"
                };

                _logger.Log("INFO", $"Protocol: Manifest built. Name: {manifest.Name}, DownloadUrl: {manifest.DownloadUrl}");

                await _downloadService.DownloadGameFileOnlyAsync(manifest, settings.DownloadsPath, apiKey);

                _notificationService.ShowSuccess($"Download completed for App ID: {appId}");
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Protocol: Download failed for App ID {appId}: {ex.Message}\nStack: {ex.StackTrace}");
                _notificationService.ShowError($"Download failed for App ID {appId}: {ex.Message}");
            }
        }

        private async Task HandleInstall(string appId)
        {
            try
            {
                var settings = _settingsService.LoadSettings();
                var zipPath = Path.Combine(settings.DownloadsPath, $"{appId}.zip");

                if (!File.Exists(zipPath))
                {
                    _notificationService.ShowError($"File not found for App ID: {appId}. Please download it first.");
                    return;
                }

                _notificationService.ShowNotification("Installation Started", $"Starting installation for App ID: {appId}", NotificationType.Info);

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await InstallGameFile(zipPath, appId, false);
                });
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Installation failed for App ID {appId}: {ex.Message}");
            }
        }

        private async Task HandleDownloadAndInstall(string appId, bool autoDeleteZip)
        {
            try
            {
                _logger.Log("INFO", $"Protocol: HandleDownloadAndInstall called for App ID: {appId}");

                var settings = _settingsService.LoadSettings();
                var apiKey = _storeApiFactory.GetActiveApiKey();

                _logger.Log("INFO", $"Protocol: Using store: {settings.SelectedStore}, API Key length: {apiKey?.Length ?? 0}");

                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.Log("ERROR", "Protocol: No API key configured");
                    _notificationService.ShowError("API key not configured. Please set it in Settings.");
                    return;
                }

                _notificationService.ShowNotification("Download & Install", $"Starting download and install for App ID: {appId}", NotificationType.Info);

                _logger.Log("INFO", $"Protocol: Building manifest for App ID: {appId}");

                // Build manifest manually (like StoreViewModel does)
                // The Basement API's /manifest/{appId} returns the ZIP file directly, not JSON
                var baseUrl = settings.SelectedStore == StoreProvider.Basement
                    ? settings.BasementApiUrl
                    : "https://manifest.morrenus.xyz/api/v1";

                var manifest = new Manifest
                {
                    AppId = appId,
                    Name = $"App {appId}",
                    IconUrl = string.Empty,
                    Size = 0,
                    DownloadUrl = $"{baseUrl}/manifest/{appId}"
                };

                _logger.Log("INFO", $"Protocol: Manifest built. Name: {manifest.Name}, DownloadUrl: {manifest.DownloadUrl}");

                var zipPath = await _downloadService.DownloadGameFileOnlyAsync(manifest, settings.DownloadsPath, apiKey);

                _notificationService.ShowNotification("Download Complete", $"Download completed, now installing App ID: {appId}", NotificationType.Info);

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await InstallGameFile(zipPath, appId, autoDeleteZip);
                });
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Protocol: Download/Install failed for App ID {appId}: {ex.Message}\nStack: {ex.StackTrace}");
                _notificationService.ShowError($"Download/Install failed for App ID {appId}: {ex.Message}");
            }
        }

        private async Task InstallGameFile(string zipPath, string appId, bool autoDeleteZip)
        {
            try
            {
                var settings = _settingsService.LoadSettings();

                await _fileInstallService.InstallFromZipAsync(zipPath, null);

                _notificationService.ShowSuccess($"Installation completed for App ID: {appId}. Restart Steam to see changes.");

                // Auto-delete ZIP if requested
                if (autoDeleteZip && File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                    _notificationService.ShowNotification("Cleanup", $"Deleted ZIP file for App ID: {appId}", NotificationType.Info);
                }
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Installation failed: {ex.Message}");
            }
        }
    }
}
