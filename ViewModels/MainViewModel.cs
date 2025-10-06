using SolusManifestApp.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolusManifestApp.Services;
using System;
using System.Windows;

namespace SolusManifestApp.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly SteamService _steamService;
        private readonly SettingsService _settingsService;
        private readonly UpdateService _updateService;
        private readonly NotificationService _notificationService;

        [ObservableProperty]
        private object? _currentPage;

        [ObservableProperty]
        private string _currentPageName = "Home";

        public HomeViewModel HomeViewModel { get; }
        public LuaInstallerViewModel LuaInstallerViewModel { get; }
        public LibraryViewModel LibraryViewModel { get; }
        public StoreViewModel StoreViewModel { get; }
        public DownloadsViewModel DownloadsViewModel { get; }
        public ToolsViewModel ToolsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }

        public MainViewModel(
            SteamService steamService,
            SettingsService settingsService,
            UpdateService updateService,
            NotificationService notificationService,
            HomeViewModel homeViewModel,
            LuaInstallerViewModel luaInstallerViewModel,
            LibraryViewModel libraryViewModel,
            StoreViewModel storeViewModel,
            DownloadsViewModel downloadsViewModel,
            ToolsViewModel toolsViewModel,
            SettingsViewModel settingsViewModel)
        {
            _steamService = steamService;
            _settingsService = settingsService;
            _updateService = updateService;
            _notificationService = notificationService;

            HomeViewModel = homeViewModel;
            LuaInstallerViewModel = luaInstallerViewModel;
            LibraryViewModel = libraryViewModel;
            StoreViewModel = storeViewModel;
            DownloadsViewModel = downloadsViewModel;
            ToolsViewModel = toolsViewModel;
            SettingsViewModel = settingsViewModel;

            // Start at Home page
            CurrentPage = HomeViewModel;
            CurrentPageName = "Home";
            HomeViewModel.RefreshMode();
        }

        private bool CanNavigateAway()
        {
            // Check if we're currently on settings page and have unsaved changes
            if (CurrentPage == SettingsViewModel && SettingsViewModel.HasUnsavedChanges)
            {
                var result = MessageBoxHelper.Show(
                    "You have unsaved changes. Do you want to leave without saving?",
                    "Unsaved Changes",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                return result == MessageBoxResult.Yes;
            }
            return true;
        }

        [RelayCommand]
        private void NavigateToHome()
        {
            if (!CanNavigateAway()) return;

            CurrentPage = HomeViewModel;
            CurrentPageName = "Home";
            HomeViewModel.RefreshMode();
        }

        [RelayCommand]
        private void NavigateToInstaller()
        {
            if (!CanNavigateAway()) return;

            CurrentPage = LuaInstallerViewModel;
            CurrentPageName = "Installer";
            LuaInstallerViewModel.RefreshMode();
        }

        [RelayCommand]
        private void NavigateToLibrary()
        {
            if (!CanNavigateAway()) return;

            CurrentPage = LibraryViewModel;
            CurrentPageName = "Library";
            LibraryViewModel.RefreshLibrary();
        }

        [RelayCommand]
        private void NavigateToStore()
        {
            if (!CanNavigateAway()) return;

            CurrentPage = StoreViewModel;
            CurrentPageName = "Store";
        }

        [RelayCommand]
        private void NavigateToDownloads()
        {
            if (!CanNavigateAway()) return;

            CurrentPage = DownloadsViewModel;
            CurrentPageName = "Downloads";
        }

        [RelayCommand]
        private void NavigateToTools()
        {
            if (!CanNavigateAway()) return;

            CurrentPage = ToolsViewModel;
            CurrentPageName = "Tools";
        }

        [RelayCommand]
        private void NavigateToSettings()
        {
            if (!CanNavigateAway()) return;

            CurrentPage = SettingsViewModel;
            CurrentPageName = "Settings";
        }

        [RelayCommand]
        private void MinimizeWindow(Window window)
        {
            window.WindowState = WindowState.Minimized;
        }

        [RelayCommand]
        private void MaximizeWindow(Window window)
        {
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        [RelayCommand]
        private void CloseWindow(Window window)
        {
            window.Close();
        }

        [RelayCommand]
        private void RestartSteam()
        {
            try
            {
                _steamService.RestartSteam();
                _notificationService.ShowSuccess("Steam is restarting...");
            }
            catch (Exception ex)
            {
                _notificationService.ShowError($"Failed to restart Steam: {ex.Message}");
            }
        }

        public async void CheckForUpdates()
        {
            var (hasUpdate, updateInfo) = await _updateService.CheckForUpdatesAsync();
            if (hasUpdate && updateInfo != null)
            {
                var result = MessageBoxHelper.Show(
                    $"A new version ({updateInfo.TagName}) is available!\n\n{updateInfo.Body}\n\nWould you like to download it?",
                    "Update Available",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = updateInfo.HtmlUrl,
                        UseShellExecute = true
                    });
                }
            }
        }
    }
}
