using SolusManifestApp.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SolusManifestApp.Models;
using SolusManifestApp.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace SolusManifestApp.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SteamService _steamService;
        private readonly SettingsService _settingsService;
        private readonly ManifestApiService _manifestApiService;
        private readonly BackupService _backupService;
        private readonly CacheService _cacheService;
        private readonly NotificationService _notificationService;
        private readonly LuaInstallerViewModel _luaInstallerViewModel;
        private readonly SteamLibraryService _steamLibraryService;
        private readonly ThemeService _themeService;

        [ObservableProperty]
        private AppSettings _settings;

        [ObservableProperty]
        private string _steamPath = string.Empty;

        [ObservableProperty]
        private string _apiKey = string.Empty;

        [ObservableProperty]
        private string _downloadsPath = string.Empty;

        [ObservableProperty]
        private bool _autoCheckUpdates;

        [ObservableProperty]
        private bool _minimizeToTray;

        [ObservableProperty]
        private bool _autoInstallAfterDownload;

        [ObservableProperty]
        private bool _showNotifications;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        [ObservableProperty]
        private ObservableCollection<string> _apiKeyHistory = new();

        [ObservableProperty]
        private string? _selectedHistoryKey;

        [ObservableProperty]
        private long _cacheSize;

        [ObservableProperty]
        private bool _isSteamToolsMode;

        [ObservableProperty]
        private bool _isGreenLumaMode;

        [ObservableProperty]
        private bool _isGreenLumaNormalMode;

        [ObservableProperty]
        private bool _isGreenLumaStealthAnyFolderMode;

        [ObservableProperty]
        private bool _isGreenLumaStealthUser32Mode;

        [ObservableProperty]
        private string _appListPath = string.Empty;

        [ObservableProperty]
        private string _dllInjectorPath = string.Empty;

        [ObservableProperty]
        private bool _useDefaultInstallLocation;

        [ObservableProperty]
        private ObservableCollection<string> _libraryFolders = new();

        [ObservableProperty]
        private string _selectedLibraryFolder = string.Empty;

        [ObservableProperty]
        private bool _isAdvancedNormalMode;

        [ObservableProperty]
        private string _selectedThemeName = "Default";

        [ObservableProperty]
        private bool _hasUnsavedChanges;

        private bool _isLoading; // Flag to prevent marking as unsaved during load

        public bool ShowAdvancedNormalModeSettings => IsGreenLumaNormalMode && IsAdvancedNormalMode;

        // Mark as unsaved when properties change
        partial void OnSteamPathChanged(string value) => MarkAsUnsaved();
        partial void OnApiKeyChanged(string value) => MarkAsUnsaved();
        partial void OnDownloadsPathChanged(string value) => MarkAsUnsaved();
        partial void OnAutoCheckUpdatesChanged(bool value) => MarkAsUnsaved();
        partial void OnMinimizeToTrayChanged(bool value) => MarkAsUnsaved();
        partial void OnAutoInstallAfterDownloadChanged(bool value) => MarkAsUnsaved();
        partial void OnShowNotificationsChanged(bool value) => MarkAsUnsaved();
        partial void OnSelectedThemeNameChanged(string value) => MarkAsUnsaved();
        partial void OnUseDefaultInstallLocationChanged(bool value) => MarkAsUnsaved();
        partial void OnSelectedLibraryFolderChanged(string value) => MarkAsUnsaved();
        partial void OnDllInjectorPathChanged(string value) => MarkAsUnsaved();

        private void MarkAsUnsaved()
        {
            if (!_isLoading)
            {
                HasUnsavedChanges = true;
            }
        }

        partial void OnIsSteamToolsModeChanged(bool value)
        {
            if (value)
            {
                IsGreenLumaMode = false;
                Settings.Mode = ToolMode.SteamTools;
            }
            MarkAsUnsaved();
        }

        partial void OnIsGreenLumaModeChanged(bool value)
        {
            if (value)
            {
                IsSteamToolsMode = false;
                Settings.Mode = ToolMode.GreenLuma;
            }
            MarkAsUnsaved();
        }

        partial void OnIsGreenLumaNormalModeChanged(bool value)
        {
            if (value)
            {
                IsGreenLumaStealthAnyFolderMode = false;
                IsGreenLumaStealthUser32Mode = false;
                Settings.GreenLumaSubMode = GreenLumaMode.Normal;

                // Auto-set DLLInjector path to {steampath}/DLLInjector.exe (unless advanced mode is enabled)
                if (!IsAdvancedNormalMode && !string.IsNullOrEmpty(Settings.SteamPath))
                {
                    DllInjectorPath = Path.Combine(Settings.SteamPath, "DLLInjector.exe");
                }
            }
            OnPropertyChanged(nameof(ShowAdvancedNormalModeSettings));
            MarkAsUnsaved();
        }

        partial void OnIsAdvancedNormalModeChanged(bool value)
        {
            if (!value && IsGreenLumaNormalMode)
            {
                // When unchecking advanced mode, reset to default path
                if (!string.IsNullOrEmpty(Settings.SteamPath))
                {
                    DllInjectorPath = Path.Combine(Settings.SteamPath, "DLLInjector.exe");
                }
            }
            OnPropertyChanged(nameof(ShowAdvancedNormalModeSettings));
            MarkAsUnsaved();
        }

        partial void OnIsGreenLumaStealthAnyFolderModeChanged(bool value)
        {
            if (value)
            {
                IsGreenLumaNormalMode = false;
                IsGreenLumaStealthUser32Mode = false;
                Settings.GreenLumaSubMode = GreenLumaMode.StealthAnyFolder;
            }
            MarkAsUnsaved();
        }

        partial void OnIsGreenLumaStealthUser32ModeChanged(bool value)
        {
            if (value)
            {
                IsGreenLumaNormalMode = false;
                IsGreenLumaStealthAnyFolderMode = false;
                Settings.GreenLumaSubMode = GreenLumaMode.StealthUser32;
            }
            MarkAsUnsaved();
        }

        public SettingsViewModel(
            SteamService steamService,
            SettingsService settingsService,
            ManifestApiService manifestApiService,
            BackupService backupService,
            CacheService cacheService,
            NotificationService notificationService,
            LuaInstallerViewModel luaInstallerViewModel,
            SteamLibraryService steamLibraryService,
            ThemeService themeService)
        {
            _steamService = steamService;
            _settingsService = settingsService;
            _manifestApiService = manifestApiService;
            _backupService = backupService;
            _cacheService = cacheService;
            _notificationService = notificationService;
            _luaInstallerViewModel = luaInstallerViewModel;
            _steamLibraryService = steamLibraryService;
            _themeService = themeService;

            _settings = new AppSettings();
            LoadSettings();
            UpdateCacheSize();
        }

        [RelayCommand]
        private void LoadSettings()
        {
            _isLoading = true; // Prevent marking as unsaved during load

            Settings = _settingsService.LoadSettings();

            // Auto-detect Steam path if not set
            if (string.IsNullOrEmpty(Settings.SteamPath))
            {
                var detectedPath = _steamService.GetSteamPath();
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    Settings.SteamPath = detectedPath;
                }
            }

            SteamPath = Settings.SteamPath;
            ApiKey = Settings.ApiKey;
            DownloadsPath = Settings.DownloadsPath;
            AutoCheckUpdates = Settings.AutoCheckUpdates;
            MinimizeToTray = Settings.MinimizeToTray;
            AutoInstallAfterDownload = Settings.AutoInstallAfterDownload;
            ShowNotifications = Settings.ShowNotifications;
            ApiKeyHistory = new ObservableCollection<string>(Settings.ApiKeyHistory);
            AppListPath = Settings.AppListPath;
            UseDefaultInstallLocation = Settings.UseDefaultInstallLocation;
            SelectedLibraryFolder = Settings.SelectedLibraryFolder;

            // Load library folders
            var folders = _steamLibraryService.GetLibraryFolders();
            LibraryFolders = new ObservableCollection<string>(folders);

            // Set default if none selected
            if (string.IsNullOrEmpty(SelectedLibraryFolder) && LibraryFolders.Any())
            {
                SelectedLibraryFolder = LibraryFolders.First();
            }

            // Set mode radio buttons
            IsSteamToolsMode = Settings.Mode == ToolMode.SteamTools;
            IsGreenLumaMode = Settings.Mode == ToolMode.GreenLuma;

            // Set GreenLuma sub-mode radio buttons
            IsGreenLumaNormalMode = Settings.GreenLumaSubMode == GreenLumaMode.Normal;
            IsGreenLumaStealthAnyFolderMode = Settings.GreenLumaSubMode == GreenLumaMode.StealthAnyFolder;
            IsGreenLumaStealthUser32Mode = Settings.GreenLumaSubMode == GreenLumaMode.StealthUser32;

            // Set theme
            SelectedThemeName = Settings.Theme.ToString();

            // Auto-set DLLInjector path based on mode
            if (Settings.GreenLumaSubMode == GreenLumaMode.Normal)
            {
                // Normal mode: Always use {SteamPath}/DLLInjector.exe
                if (!string.IsNullOrEmpty(Settings.SteamPath))
                {
                    DllInjectorPath = Path.Combine(Settings.SteamPath, "DLLInjector.exe");
                    Settings.DLLInjectorPath = DllInjectorPath;
                }
            }
            else if (Settings.GreenLumaSubMode == GreenLumaMode.StealthAnyFolder)
            {
                // Stealth Any Folder: Use saved path
                DllInjectorPath = Settings.DLLInjectorPath;

                // Auto-set AppListPath to {DLLInjectorPath directory}/AppList
                if (!string.IsNullOrEmpty(DllInjectorPath))
                {
                    var injectorDir = Path.GetDirectoryName(DllInjectorPath);
                    if (!string.IsNullOrEmpty(injectorDir))
                    {
                        AppListPath = Path.Combine(injectorDir, "AppList");
                        Settings.AppListPath = AppListPath;
                    }
                }
            }
            else
            {
                // Stealth User32: No custom paths needed
                DllInjectorPath = Settings.DLLInjectorPath;
            }

            _isLoading = false;
            HasUnsavedChanges = false; // Clear unsaved changes flag after load

            StatusMessage = "Settings loaded";
        }

        [RelayCommand]
        private void SaveSettings()
        {
            Settings.SteamPath = SteamPath;
            Settings.ApiKey = ApiKey;
            Settings.DownloadsPath = DownloadsPath;
            Settings.AutoCheckUpdates = AutoCheckUpdates;
            Settings.MinimizeToTray = MinimizeToTray;
            Settings.AutoInstallAfterDownload = AutoInstallAfterDownload;
            Settings.ShowNotifications = ShowNotifications;
            Settings.AppListPath = AppListPath;
            Settings.DLLInjectorPath = DllInjectorPath;
            Settings.UseDefaultInstallLocation = UseDefaultInstallLocation;
            Settings.SelectedLibraryFolder = SelectedLibraryFolder;

            // Parse and save theme
            if (Enum.TryParse<AppTheme>(SelectedThemeName, out var theme))
            {
                Settings.Theme = theme;
            }

            try
            {
                _settingsService.SaveSettings(Settings);
                _steamService.SetCustomSteamPath(SteamPath);

                // Apply theme
                _themeService.ApplyTheme(Settings.Theme);

                // Refresh mode on Installer page
                _luaInstallerViewModel.RefreshMode();

                HasUnsavedChanges = false; // Clear unsaved changes flag after successful save
                StatusMessage = "Settings saved successfully!";
                _notificationService.ShowSuccess("Settings saved successfully!");
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _notificationService.ShowError($"Failed to save settings: {ex.Message}");
            }
        }

        [RelayCommand]
        private void BrowseSteamPath()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Steam.exe",
                Filter = "Steam Executable|steam.exe|All Files|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                var path = Path.GetDirectoryName(dialog.FileName);
                if (!string.IsNullOrEmpty(path) && _steamService.ValidateSteamPath(path))
                {
                    SteamPath = path;
                    StatusMessage = "Steam path updated";

                    // Refresh library folders
                    var folders = _steamLibraryService.GetLibraryFolders();
                    LibraryFolders = new ObservableCollection<string>(folders);
                    if (LibraryFolders.Any())
                    {
                        SelectedLibraryFolder = LibraryFolders.First();
                    }
                }
                else
                {
                    _notificationService.ShowError("Invalid Steam installation path");
                }
            }
        }

        [RelayCommand]
        private void BrowseDownloadsPath()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Downloads Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                DownloadsPath = dialog.FolderName;
                Directory.CreateDirectory(DownloadsPath);
                StatusMessage = "Downloads path updated";
            }
        }

        [RelayCommand]
        private void BrowseAppListPath()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select AppList Folder"
            };

            if (dialog.ShowDialog() == true)
            {
                AppListPath = dialog.FolderName;
                Directory.CreateDirectory(AppListPath);
                StatusMessage = "AppList path updated";
            }
        }

        [RelayCommand]
        private void BrowseDLLInjectorPath()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select DLLInjector.exe",
                Filter = "DLLInjector|DLLInjector.exe|Executable Files|*.exe|All Files|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                DllInjectorPath = dialog.FileName;

                // Auto-set AppListPath to {DLLInjectorPath directory}/AppList for StealthAnyFolder mode
                if (Settings.GreenLumaSubMode == GreenLumaMode.StealthAnyFolder)
                {
                    var injectorDir = Path.GetDirectoryName(DllInjectorPath);
                    if (!string.IsNullOrEmpty(injectorDir))
                    {
                        AppListPath = Path.Combine(injectorDir, "AppList");
                    }
                }

                StatusMessage = "DLLInjector path updated";
            }
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task ValidateApiKey()
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                _notificationService.ShowWarning("Please enter an API key");
                return;
            }

            if (!_manifestApiService.ValidateApiKey(ApiKey))
            {
                _notificationService.ShowWarning("API key must start with 'smm'");
                return;
            }

            StatusMessage = "Testing API key...";

            try
            {
                var isValid = await _manifestApiService.TestApiKeyAsync(ApiKey);

                if (isValid)
                {
                    StatusMessage = "API key is valid";
                    _notificationService.ShowSuccess("API key is valid!");
                    _settingsService.AddApiKeyToHistory(ApiKey);
                    LoadSettings(); // Refresh history
                }
                else
                {
                    StatusMessage = "API key is invalid";
                    _notificationService.ShowError("API key is invalid or expired");
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                _notificationService.ShowError($"Failed to validate API key: {ex.Message}");
            }
        }

        [RelayCommand]
        private void DetectSteam()
        {
            var path = _steamService.GetSteamPath();

            if (!string.IsNullOrEmpty(path))
            {
                SteamPath = path;
                StatusMessage = "Steam detected successfully";
                _notificationService.ShowSuccess($"Steam found at: {path}");

                // Refresh library folders
                var folders = _steamLibraryService.GetLibraryFolders();
                LibraryFolders = new ObservableCollection<string>(folders);
                if (LibraryFolders.Any())
                {
                    SelectedLibraryFolder = LibraryFolders.First();
                }
            }
            else
            {
                StatusMessage = "Steam not found";
                _notificationService.ShowWarning("Could not detect Steam installation.\n\nPlease select Steam path manually.");
            }
        }

        [RelayCommand]
        private void UseHistoryKey()
        {
            if (!string.IsNullOrEmpty(SelectedHistoryKey))
            {
                ApiKey = SelectedHistoryKey;
                StatusMessage = "API key loaded from history";
            }
        }

        [RelayCommand]
        private void RemoveHistoryKey()
        {
            if (!string.IsNullOrEmpty(SelectedHistoryKey))
            {
                Settings.ApiKeyHistory.Remove(SelectedHistoryKey);
                _settingsService.SaveSettings(Settings);
                ApiKeyHistory.Remove(SelectedHistoryKey);
                StatusMessage = "API key removed from history";
            }
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task CreateBackup()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Backup",
                Filter = "JSON Files|*.json",
                FileName = $"SolusBackup_{System.DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    StatusMessage = "Creating backup...";
                    var backupPath = await _backupService.CreateBackupAsync(Path.GetDirectoryName(dialog.FileName)!);
                    StatusMessage = "Backup created successfully";
                    _notificationService.ShowSuccess($"Backup created: {Path.GetFileName(backupPath)}");
                }
                catch (System.Exception ex)
                {
                    StatusMessage = $"Backup failed: {ex.Message}";
                    _notificationService.ShowError($"Failed to create backup: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private async System.Threading.Tasks.Task RestoreBackup()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Backup File",
                Filter = "JSON Files|*.json|All Files|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    StatusMessage = "Loading backup...";
                    var backup = await _backupService.LoadBackupAsync(dialog.FileName);

                    var result = MessageBoxHelper.Show(
                        $"Backup Date: {backup.BackupDate}\n" +
                        $"Lua: {backup.InstalledModAppIds.Count}\n\n" +
                        $"Restore settings and lua list?",
                        "Restore Backup",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        var restoreResult = await _backupService.RestoreBackupAsync(backup, true);
                        StatusMessage = restoreResult.Message;

                        if (restoreResult.Success)
                        {
                            LoadSettings();
                            _notificationService.ShowSuccess(restoreResult.Message);
                        }
                        else
                        {
                            _notificationService.ShowError(restoreResult.Message);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    StatusMessage = $"Restore failed: {ex.Message}";
                    _notificationService.ShowError($"Failed to restore backup: {ex.Message}");
                }
            }
        }

        [RelayCommand]
        private void ClearCache()
        {
            var result = MessageBoxHelper.Show(
                "This will delete all cached icons and data.\n\nContinue?",
                "Clear Cache",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _cacheService.ClearAllCache();
                UpdateCacheSize();
                _notificationService.ShowSuccess("Cache cleared successfully");
            }
        }

        private void UpdateCacheSize()
        {
            CacheSize = _cacheService.GetCacheSize();
        }

        public string GetCacheSizeFormatted()
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = CacheSize;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
