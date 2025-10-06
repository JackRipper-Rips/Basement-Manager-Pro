using SolusManifestApp.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolusManifestApp.Models;
using SolusManifestApp.Services;
using SolusManifestApp.Views.Dialogs;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace SolusManifestApp.ViewModels
{
    public partial class StoreViewModel : ObservableObject
    {
        private readonly ManifestApiService _manifestApiService;
        private readonly DownloadService _downloadService;
        private readonly SettingsService _settingsService;
        private readonly SteamApiService _steamApiService;
        private readonly CacheService _cacheService;
        private readonly DepotDownloadService _depotDownloadService;
        private readonly FileInstallService _fileInstallService;
        private readonly SteamService _steamService;

        [ObservableProperty]
        private ObservableCollection<Manifest> _availableGames = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Search for games using Steam Store";

        public StoreViewModel(
            ManifestApiService manifestApiService,
            DownloadService downloadService,
            SettingsService settingsService,
            CacheService cacheService,
            DepotDownloadService depotDownloadService,
            FileInstallService fileInstallService,
            SteamService steamService)
        {
            _manifestApiService = manifestApiService;
            _downloadService = downloadService;
            _settingsService = settingsService;
            _cacheService = cacheService;
            _steamApiService = new SteamApiService(_cacheService);
            _depotDownloadService = depotDownloadService;
            _fileInstallService = fileInstallService;
            _steamService = steamService;
        }

        [RelayCommand]
        private async Task LoadGames()
        {
            var settings = _settingsService.LoadSettings();

            if (string.IsNullOrEmpty(settings.ApiKey))
            {
                StatusMessage = "Please enter API key in settings";
                return;
            }

            IsLoading = true;
            StatusMessage = "Loading games...";

            try
            {
                var games = await _manifestApiService.GetAllGamesAsync(settings.ApiKey);
                if (games != null)
                {
                    AvailableGames = new ObservableCollection<Manifest>(games);
                    StatusMessage = $"{games.Count} game(s) available";
                }
                else
                {
                    StatusMessage = "No games found";
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBoxHelper.Show(
                    $"Failed to load games: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SearchGames()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                AvailableGames.Clear();
                StatusMessage = "Enter a search term";
                return;
            }

            IsLoading = true;
            StatusMessage = "Searching Steam Store...";

            try
            {
                // Search using Steam Store API
                var steamResults = await _steamApiService.SearchStoreWithCacheAsync(SearchQuery, 25);

                if (steamResults != null && steamResults.Items.Count > 0)
                {
                    // Convert Steam search results to Manifest objects for display
                    var manifests = steamResults.Items.Select(item =>
                    {
                        var description = $"Type: {item.Type}";
                        if (!string.IsNullOrEmpty(item.Metascore))
                        {
                            description += $" | Metascore: {item.Metascore}";
                        }

                        return new Manifest
                        {
                            AppId = item.Id.ToString(),
                            Name = item.Name,
                            Description = description,
                            IconUrl = !string.IsNullOrEmpty(item.TinyImage) ? item.TinyImage : "",
                            Size = 0, // Unknown from Steam search
                            DownloadUrl = $"https://manifest.morrenus.xyz/api/v1/manifest/{item.Id}"
                        };
                    }).ToList();

                    AvailableGames = new ObservableCollection<Manifest>(manifests);
                    StatusMessage = $"Found {steamResults.Items.Count} game(s) - Total: {steamResults.Total}";

                    // Load icons in background
                    foreach (var manifest in manifests)
                    {
                        if (!string.IsNullOrEmpty(manifest.IconUrl))
                        {
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    manifest.CachedIconPath = await _cacheService.GetIconAsync(manifest.AppId, manifest.IconUrl);
                                }
                                catch { }
                            });
                        }
                    }
                }
                else
                {
                    AvailableGames.Clear();
                    StatusMessage = "No results found";
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                MessageBoxHelper.Show(
                    $"Search failed: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DownloadGame(Manifest manifest)
        {
            var settings = _settingsService.LoadSettings();

            if (string.IsNullOrEmpty(settings.ApiKey))
            {
                MessageBoxHelper.Show(
                    "Please enter API key in settings",
                    "API Key Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Check if game exists first
                StatusMessage = $"Checking game status: {manifest.Name}";
                var gameStatus = await _manifestApiService.GetGameStatusAsync(manifest.AppId, settings.ApiKey);

                if (gameStatus == null || gameStatus.Status != "available")
                {
                    MessageBoxHelper.Show(
                        $"Game '{manifest.Name}' is not available for download.\n\nStatus: {gameStatus?.Status ?? "unknown"}",
                        "Game Not Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    StatusMessage = $"Game not available: {manifest.Name}";
                    return;
                }

                // Update manifest with actual file size from status
                manifest.Size = gameStatus.FileSize;

                // Download the zip file only
                StatusMessage = $"Downloading: {manifest.Name}";
                var zipFilePath = await _downloadService.DownloadGameFileOnlyAsync(manifest, settings.DownloadsPath, settings.ApiKey);

                StatusMessage = $"{manifest.Name} downloaded successfully";

                MessageBoxHelper.Show(
                    $"{manifest.Name} has been downloaded!\n\nGo to the Downloads page to install it.",
                    "Download Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Download failed: {ex.Message}";
                MessageBoxHelper.Show(
                    $"Failed to download {manifest.Name}: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
