using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolusManifestApp.Models;
using SolusManifestApp.Services;
using SolusManifestApp.Views.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SolusManifestApp.ViewModels
{
    public partial class LuaInstallerViewModel : ObservableObject
    {
        private readonly FileInstallService _fileInstallService;
        private readonly NotificationService _notificationService;
        private readonly SettingsService _settingsService;
        private readonly DepotDownloadService _depotDownloadService;
        private readonly SteamService _steamService;
        private readonly DownloadService _downloadService;
        private readonly SteamApiService _steamApiService;

        [ObservableProperty]
        private string _selectedFilePath = string.Empty;

        [ObservableProperty]
        private string _selectedFileName = "No file selected";

        [ObservableProperty]
        private bool _hasFileSelected = false;

        [ObservableProperty]
        private bool _isInstalling = false;

        [ObservableProperty]
        private string _statusMessage = "Drop a .zip, .lua, or .manifest file here to install";

        [ObservableProperty]
        private bool _isGreenLumaMode;

        public LuaInstallerViewModel(
            FileInstallService fileInstallService,
            NotificationService notificationService,
            SettingsService settingsService,
            DepotDownloadService depotDownloadService,
            SteamService steamService,
            DownloadService downloadService,
            SteamApiService steamApiService)
        {
            _fileInstallService = fileInstallService;
            _notificationService = notificationService;
            _settingsService = settingsService;
            _depotDownloadService = depotDownloadService;
            _steamService = steamService;
            _downloadService = downloadService;
            _steamApiService = steamApiService;

            // Load initial mode
            var settings = _settingsService.LoadSettings();
            IsGreenLumaMode = settings.Mode == ToolMode.GreenLuma;
        }

        public void RefreshMode()
        {
            var settings = _settingsService.LoadSettings();
            IsGreenLumaMode = settings.Mode == ToolMode.GreenLuma;
        }

        [RelayCommand]
        private void ProcessDroppedFiles(string[] files)
        {
            if (files == null || files.Length == 0)
                return;

            var file = files.FirstOrDefault(f =>
                f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".lua", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase));

            if (file != null)
            {
                SelectedFilePath = file;
                SelectedFileName = Path.GetFileName(file);
                HasFileSelected = true;

                if (file.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = $"Ready to install manifest: {SelectedFileName}";
                }
                else if (file.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = $"Ready to install Lua file: {SelectedFileName}";
                }
                else
                {
                    StatusMessage = $"Ready to install: {SelectedFileName}";
                }
            }
            else
            {
                _notificationService.ShowWarning("Please drop a .zip, .lua, or .manifest file");
            }
        }

        [RelayCommand]
        private void BrowseFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Supported Files (*.zip;*.lua;*.manifest)|*.zip;*.lua;*.manifest|Lua Archives (*.zip)|*.zip|Lua Files (*.lua)|*.lua|Manifest Files (*.manifest)|*.manifest|All files (*.*)|*.*",
                Title = "Select File to Install"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedFilePath = dialog.FileName;
                SelectedFileName = Path.GetFileName(dialog.FileName);
                HasFileSelected = true;

                if (dialog.FileName.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = $"Ready to install manifest: {SelectedFileName}";
                }
                else if (dialog.FileName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    StatusMessage = $"Ready to install Lua file: {SelectedFileName}";
                }
                else
                {
                    StatusMessage = $"Ready to install: {SelectedFileName}";
                }
            }
        }

        [RelayCommand]
        private async Task InstallFile()
        {
            if (string.IsNullOrEmpty(SelectedFilePath) || !File.Exists(SelectedFilePath))
            {
                _notificationService.ShowError("Please select a valid file first");
                return;
            }

            IsInstalling = true;
            StatusMessage = $"Installing {SelectedFileName}...";

            try
            {
                var settings = _settingsService.LoadSettings();

                if (SelectedFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract appId from filename (e.g., 224060.zip -> 224060)
                    var appId = Path.GetFileNameWithoutExtension(SelectedFilePath);

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
                            _notificationService.ShowError($"App ID {appId} already exists in AppList folder. Cannot install duplicate game.");
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
                            _notificationService.ShowError($"App ID {appId} not found in Steam's app list. Cannot install invalid game.");
                            StatusMessage = "Installation cancelled - Invalid App ID";
                            IsInstalling = false;
                            return;
                        }
                    }

                    List<string>? selectedDepotIds = null;

                    // Follow GreenLuma flow if mode is enabled
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
                            _notificationService.ShowError($"AppList is full ({currentCount}/128 files). Cannot add more games. Please uninstall some games first.");
                            StatusMessage = "Installation cancelled - AppList full";
                            IsInstalling = false;
                            return;
                        }

                        // Extract lua content from the zip
                        StatusMessage = $"Analyzing depot information...";
                        var luaContent = _downloadService.ExtractLuaContentFromZip(SelectedFilePath, appId);

                        // Get combined depot info (lua names/sizes + steamcmd languages)
                        var depots = await _depotDownloadService.GetCombinedDepotInfo(appId, luaContent);

                        if (depots.Count > 0)
                        {
                            // Calculate max depots that can be selected
                            var maxDepotsAllowed = 128 - currentCount - 1; // -1 for main app ID

                            if (maxDepotsAllowed <= 0)
                            {
                                _notificationService.ShowError($"AppList is nearly full ({currentCount}/128 files). Cannot add more games. Please uninstall some games first.");
                                StatusMessage = "Installation cancelled - AppList full";
                                IsInstalling = false;
                                return;
                            }

                            // Show warning if space is limited
                            if (maxDepotsAllowed < depots.Count)
                            {
                                _notificationService.ShowWarning($"AppList has limited space. You can only select up to {maxDepotsAllowed} depots (currently {currentCount}/128 files).");
                            }
                            // Show depot selection dialog
                            var depotDialog = new DepotSelectionDialog(depots);
                            var result = depotDialog.ShowDialog();

                            if (result == true && depotDialog.SelectedDepotIds.Count > 0)
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

                    // Install ZIP (contains .lua and .manifest files)
                    var depotKeys = await _fileInstallService.InstallFromZipAsync(
                        SelectedFilePath,
                        settings.Mode == ToolMode.GreenLuma,
                        message =>
                        {
                            StatusMessage = message;
                        },
                        selectedDepotIds);

                    // If GreenLuma mode, update Config.VDF with depot keys
                    if (settings.Mode == ToolMode.GreenLuma && depotKeys.Count > 0)
                    {
                        StatusMessage = $"Updating Config.VDF with {depotKeys.Count} depot keys...";
                        _fileInstallService.UpdateConfigVdfWithDepotKeys(depotKeys);
                    }
                }
                else if (SelectedFilePath.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    // Install individual .lua file
                    await _fileInstallService.InstallLuaFileAsync(SelectedFilePath);
                }
                else if (SelectedFilePath.EndsWith(".manifest", StringComparison.OrdinalIgnoreCase))
                {
                    // Install .manifest file
                    await _fileInstallService.InstallManifestFileAsync(SelectedFilePath);
                }
                else
                {
                    throw new Exception("Unsupported file type");
                }

                _notificationService.ShowSuccess($"{SelectedFileName} installed successfully!\n\nRestart Steam for changes to take effect.");
                StatusMessage = "Installation complete! Restart Steam for changes to take effect.";

                // Clear selection
                SelectedFilePath = string.Empty;
                SelectedFileName = "No file selected";
                HasFileSelected = false;
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Installation failed: {ex.Message}");
                StatusMessage = $"Installation failed: {ex.Message}";
            }
            finally
            {
                IsInstalling = false;
            }
        }

        [RelayCommand]
        private void ClearSelection()
        {
            SelectedFilePath = string.Empty;
            SelectedFileName = "No file selected";
            HasFileSelected = false;
            StatusMessage = "Drop a .zip, .lua, or .manifest file here to install";
        }
    }
}
