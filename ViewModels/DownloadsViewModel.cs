using SolusManifestApp.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolusManifestApp.Models;
using SolusManifestApp.Services;
using SolusManifestApp.Views.Dialogs;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SolusManifestApp.ViewModels
{
    public partial class DownloadsViewModel : ObservableObject
    {
        private readonly DownloadService _downloadService;
        private readonly FileInstallService _fileInstallService;
        private readonly SettingsService _settingsService;
        private readonly DepotDownloadService _depotDownloadService;
        private readonly SteamService _steamService;
        private readonly SteamApiService _steamApiService;
        private readonly NotificationService _notificationService;
        private readonly LibraryRefreshService _libraryRefreshService;

        [ObservableProperty]
        private ObservableCollection<DownloadItem> _activeDownloads;

        [ObservableProperty]
        private ObservableCollection<string> _downloadedFiles = new();

        [ObservableProperty]
        private string _statusMessage = "No downloads";

        [ObservableProperty]
        private bool _isInstalling;

        public DownloadsViewModel(
            DownloadService downloadService,
            FileInstallService fileInstallService,
            SettingsService settingsService,
            DepotDownloadService depotDownloadService,
            SteamService steamService,
            SteamApiService steamApiService,
            NotificationService notificationService,
            LibraryRefreshService libraryRefreshService)
        {
            _downloadService = downloadService;
            _fileInstallService = fileInstallService;
            _settingsService = settingsService;
            _depotDownloadService = depotDownloadService;
            _steamService = steamService;
            _steamApiService = steamApiService;
            _notificationService = notificationService;
            _libraryRefreshService = libraryRefreshService;

            ActiveDownloads = _downloadService.ActiveDownloads;

            RefreshDownloadedFiles();

            // Subscribe to download completed event for auto-refresh
            _downloadService.DownloadCompleted += OnDownloadCompleted;
        }

        private async void OnDownloadCompleted(object? sender, DownloadItem downloadItem)
        {
            // Auto-refresh the downloaded files list when a download completes
            RefreshDownloadedFiles();

            // Check if auto-install is enabled
            var settings = _settingsService.LoadSettings();
            if (settings.AutoInstallAfterDownload && !string.IsNullOrEmpty(downloadItem.DestinationPath) && File.Exists(downloadItem.DestinationPath))
            {
                // Auto-install the downloaded file
                await InstallFile(downloadItem.DestinationPath);
            }
        }

        [RelayCommand]
        private void RefreshDownloadedFiles()
        {
            var settings = _settingsService.LoadSettings();

            if (string.IsNullOrEmpty(settings.DownloadsPath) || !Directory.Exists(settings.DownloadsPath))
            {
                DownloadedFiles.Clear();
                StatusMessage = "No downloads folder configured";
                return;
            }

            try
            {
                var files = Directory.GetFiles(settings.DownloadsPath, "*.zip")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .ToList();

                DownloadedFiles = new ObservableCollection<string>(files);
                StatusMessage = files.Count > 0 ? $"{files.Count} file(s) ready to install" : "No downloaded files";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task InstallFile(string filePath)
        {
            if (IsInstalling)
            {
                MessageBoxHelper.Show(
                    "Another installation is in progress",
                    "Please Wait",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            IsInstalling = true;
            var fileName = Path.GetFileName(filePath);
            StatusMessage = $"Installing {fileName}...";

            try
            {
                var settings = _settingsService.LoadSettings();
                var appId = Path.GetFileNameWithoutExtension(filePath);

                // Validate appId for GreenLuma mode
                if (settings.Mode == ToolMode.GreenLuma)
                {
                    // Check if app already exists in AppList
                    string? customPath = null;
                    if (settings.GreenLumaSubMode == GreenLumaMode.StealthAnyFolder)
                    {
                        var injectorDir = Path.GetDirectoryName(settings.DLLInjectorPath);
                        if (!string.IsNullOrEmpty(injectorDir))
                        {
                            customPath = Path.Combine(injectorDir, "AppList");
                        }
                    }

                    if (_fileInstallService.IsAppIdInAppList(appId, customPath))
                    {
                        MessageBoxHelper.Show(
                            $"App ID {appId} already exists in AppList folder. Cannot install duplicate game.",
                            "Duplicate App ID",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        StatusMessage = "Installation cancelled - Duplicate App ID";
                        IsInstalling = false;
                        return;
                    }

                    // Validate app exists in Steam's official app list
                    StatusMessage = "Validating App ID...";
                    var steamAppList = await _steamApiService.GetAppListAsync();
                    var gameName = _steamApiService.GetGameName(appId, steamAppList);

                    if (gameName == "Unknown Game")
                    {
                        MessageBoxHelper.Show(
                            $"App ID {appId} not found in Steam's app list. Cannot install invalid game.",
                            "Invalid App ID",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        StatusMessage = "Installation cancelled - Invalid App ID";
                        IsInstalling = false;
                        return;
                    }
                }

                List<string>? selectedDepotIds = null;

                // For GreenLuma mode, show depot selection dialog
                if (settings.Mode == ToolMode.GreenLuma)
                {
                    // Check current AppList count before proceeding
                    string? customPath = null;
                    if (settings.GreenLumaSubMode == GreenLumaMode.StealthAnyFolder)
                    {
                        var injectorDir = Path.GetDirectoryName(settings.DLLInjectorPath);
                        if (!string.IsNullOrEmpty(injectorDir))
                        {
                            customPath = Path.Combine(injectorDir, "AppList");
                        }
                    }

                    var appListPath = customPath ?? Path.Combine(_steamService.GetSteamPath(), "AppList");
                    var currentCount = Directory.Exists(appListPath) ? Directory.GetFiles(appListPath, "*.txt").Length : 0;

                    if (currentCount >= 128)
                    {
                        MessageBoxHelper.Show(
                            $"AppList is full ({currentCount}/128 files). Cannot add more games. Please uninstall some games first.",
                            "AppList Full",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        StatusMessage = "Installation cancelled - AppList full";
                        IsInstalling = false;
                        return;
                    }

                    // Extract lua content from zip
                    StatusMessage = $"Analyzing depot information...";
                    var luaContent = _downloadService.ExtractLuaContentFromZip(filePath, appId);

                    // Get combined depot info (lua names/sizes + steamcmd languages)
                    var depots = await _depotDownloadService.GetCombinedDepotInfo(appId, luaContent);

                    if (depots.Count > 0)
                    {
                        // Calculate max depots that can be selected
                        var maxDepotsAllowed = 128 - currentCount - 1; // -1 for main app ID

                        if (maxDepotsAllowed <= 0)
                        {
                            MessageBoxHelper.Show(
                                $"AppList is nearly full ({currentCount}/128 files). Cannot add more games. Please uninstall some games first.",
                                "AppList Full",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            StatusMessage = "Installation cancelled - AppList full";
                            IsInstalling = false;
                            return;
                        }

                        // Show warning if space is limited
                        if (maxDepotsAllowed < depots.Count)
                        {
                            MessageBoxHelper.Show(
                                $"AppList has limited space. You can only select up to {maxDepotsAllowed} depots (currently {currentCount}/128 files).",
                                "Limited AppList Space",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                        }
                        // Show depot selection dialog
                        var depotDialog = new DepotSelectionDialog(depots);
                        var dialogResult = depotDialog.ShowDialog();

                        if (dialogResult == true && depotDialog.SelectedDepotIds.Count > 0)
                        {
                            selectedDepotIds = depotDialog.SelectedDepotIds;
                        }
                        else
                        {
                            StatusMessage = "Installation cancelled";
                            IsInstalling = false;
                            return;
                        }

                        // Generate AppList with main appid + selected depot IDs
                        StatusMessage = $"Generating AppList for selected depots...";
                        var appListIds = new List<string> { appId };
                        appListIds.AddRange(selectedDepotIds);

                        // Reuse customPath from earlier check
                        _fileInstallService.GenerateAppList(appListIds, customPath);

                        // Generate ACF file for the game
                        StatusMessage = $"Generating ACF file...";
                        string? libraryFolder = settings.UseDefaultInstallLocation ? null : settings.SelectedLibraryFolder;
                        _fileInstallService.GenerateACF(appId, appId, appId, libraryFolder);
                    }
                }

                // Install files using the proper installation service
                StatusMessage = $"Installing files...";

                var depotKeys = await _fileInstallService.InstallFromZipAsync(
                    filePath,
                    settings.Mode == ToolMode.GreenLuma,
                    message => StatusMessage = message,
                    selectedDepotIds);

                // If GreenLuma mode, update Config.VDF with depot keys
                if (settings.Mode == ToolMode.GreenLuma && depotKeys.Count > 0)
                {
                    StatusMessage = $"Updating Config.VDF with {depotKeys.Count} depot keys...";
                    var success = _fileInstallService.UpdateConfigVdfWithDepotKeys(depotKeys);
                    if (!success)
                    {
                        MessageBoxHelper.Show(
                            "Failed to update config.vdf with depot keys. You may need to add them manually.",
                            "Warning",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }

                _notificationService.ShowSuccess($"{fileName} has been installed successfully! Restart Steam for changes to take effect.", "Installation Complete");

                StatusMessage = $"{fileName} installed successfully";

                // Notify library to add the game instantly
                _libraryRefreshService.NotifyGameInstalled(appId, settings.Mode == ToolMode.GreenLuma);

                // Auto-delete the ZIP file
                File.Delete(filePath);
                RefreshDownloadedFiles();
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Installation failed: {ex.Message}";
                MessageBoxHelper.Show(
                    $"Failed to install {fileName}: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsInstalling = false;
            }
        }

        [RelayCommand]
        private void CancelDownload(DownloadItem item)
        {
            _downloadService.CancelDownload(item.Id);
            StatusMessage = $"Cancelled: {item.GameName}";
        }

        [RelayCommand]
        private void RemoveDownload(DownloadItem item)
        {
            _downloadService.RemoveDownload(item);
        }

        [RelayCommand]
        private void ClearCompleted()
        {
            _downloadService.ClearCompletedDownloads();
        }

        [RelayCommand]
        private void DeleteFile(string filePath)
        {
            var result = MessageBoxHelper.Show(
                $"Are you sure you want to delete {Path.GetFileName(filePath)}?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    File.Delete(filePath);
                    RefreshDownloadedFiles();
                    StatusMessage = "File deleted";
                }
                catch (System.Exception ex)
                {
                    MessageBoxHelper.Show(
                        $"Failed to delete file: {ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        [RelayCommand]
        private void OpenDownloadsFolder()
        {
            var settings = _settingsService.LoadSettings();

            if (!string.IsNullOrEmpty(settings.DownloadsPath) && Directory.Exists(settings.DownloadsPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = settings.DownloadsPath,
                    UseShellExecute = true
                });
            }
        }
    }
}
