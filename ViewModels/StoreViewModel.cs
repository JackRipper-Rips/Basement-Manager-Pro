using SolusManifestApp.Helpers;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SolusManifestApp.Interfaces;
using SolusManifestApp.Models;
using SolusManifestApp.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SolusManifestApp.ViewModels
{
    public partial class StoreViewModel : ObservableObject
    {
        private readonly IManifestApiService _manifestApiService;
        private readonly DownloadService _downloadService;
        private readonly SettingsService _settingsService;
        private readonly CacheService _cacheService;
        private readonly NotificationService _notificationService;
        private readonly ManifestStorageService _manifestStorageService;
        private readonly AppListCacheService _appListCacheService;
        private readonly SemaphoreSlim _iconLoadSemaphore = new SemaphoreSlim(10, 10); // Max 10 concurrent downloads
        private CancellationTokenSource? _debounceTokenSource;
        private List<LibraryGame> _unfilteredGames = new(); // Store unfiltered games for re-filtering
        private bool _isUpdatingFilters = false; // Prevent infinite loops when updating filter states

        [ObservableProperty]
        private ObservableCollection<LibraryGame> _games = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusMessage = "Browse available games from the library";

        [ObservableProperty]
        private string _sortBy = "updated"; // "updated" or "name"

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _currentOffset;

        [ObservableProperty]
        private bool _hasMore;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalPages;

        [ObservableProperty]
        private bool _canGoNext;

        [ObservableProperty]
        private bool _canGoPrevious;

        [ObservableProperty]
        private bool _isListView;

        [ObservableProperty]
        private string _goToPageText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<int> _pageNumbers = new();

        [ObservableProperty]
        private string _currentStore = string.Empty;

        [ObservableProperty]
        private ObservableCollection<AppListEntry> _suggestions = new();

        [ObservableProperty]
        private bool _showSuggestions;

        [ObservableProperty]
        private AppListEntry? _selectedSuggestion;

        [ObservableProperty]
        private bool _hideSoundtrack;

        [ObservableProperty]
        private bool _hideDemo;

        [ObservableProperty]
        private bool _hidePlaytest;

        [ObservableProperty]
        private bool _hideTrailer;

        [ObservableProperty]
        private bool _hideArtbook;

        [ObservableProperty]
        private bool _hideSdk;

        [ObservableProperty]
        private bool _selectAllFilters;

        private int PageSize => _settingsService.LoadSettings().StorePageSize;

        public Action? ScrollToTopAction { get; set; }

        /// <summary>
        /// Gets the correct API key based on the selected store provider
        /// </summary>
        private string GetActiveApiKey()
        {
            var settings = _settingsService.LoadSettings();
            return settings.SelectedStore == StoreProvider.Basement
                ? settings.BasementApiKey
                : settings.ApiKey;
        }

        /// <summary>
        /// Gets the correct base URL based on the selected store provider
        /// </summary>
        private string GetActiveBaseUrl()
        {
            var settings = _settingsService.LoadSettings();
            return settings.SelectedStore == StoreProvider.Basement
                ? settings.BasementApiUrl
                : "https://manifest.morrenus.xyz/api/v1";
        }

        /// <summary>
        /// Updates the current store display name
        /// </summary>
        private void UpdateCurrentStore()
        {
            var settings = _settingsService.LoadSettings();
            CurrentStore = settings.SelectedStore == StoreProvider.Basement
                ? "Basement Store"
                : "Morrenus Store";
        }

        public StoreViewModel(
            IManifestApiService manifestApiService,
            DownloadService downloadService,
            SettingsService settingsService,
            CacheService cacheService,
            NotificationService notificationService,
            ManifestStorageService manifestStorageService,
            AppListCacheService appListCacheService)
        {
            _manifestApiService = manifestApiService;
            _downloadService = downloadService;
            _settingsService = settingsService;
            _cacheService = cacheService;
            _notificationService = notificationService;
            _manifestStorageService = manifestStorageService;
            _appListCacheService = appListCacheService;

            // Auto-load games on startup
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var settings = _settingsService.LoadSettings();
            IsListView = settings.StoreListView;
            UpdateCurrentStore();
            var apiKey = GetActiveApiKey();
            if (!string.IsNullOrEmpty(apiKey))
            {
                await LoadGamesAsync();
            }
            else
            {
                StatusMessage = "API key required - Please configure in Settings";
            }
        }

        public void OnNavigatedTo()
        {
            UpdateCurrentStore();
            var apiKey = GetActiveApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                // Show warning popup when user navigates to Store without API key
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBoxHelper.Show(
                        "An API key is required to use the Store.\n\nPlease go to Settings and enter your API key to browse and download games from the library.",
                        "API Key Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                });
            }
        }

        [RelayCommand]
        private void ToggleView()
        {
            IsListView = !IsListView;
            var settings = _settingsService.LoadSettings();
            settings.StoreListView = IsListView;
            _settingsService.SaveSettings(settings);
        }

        partial void OnSearchQueryChanged(string value)
        {
            // Handle autocomplete suggestions
            if (string.IsNullOrWhiteSpace(value))
            {
                ShowSuggestions = false;
                Suggestions.Clear();
                // Auto-search when query is cleared
                if (Games.Count > 0)
                {
                    _ = LoadGamesAsync();
                }
                return;
            }

            // Debounce autocomplete search
            _debounceTokenSource?.Cancel();
            _debounceTokenSource = new CancellationTokenSource();
            var token = _debounceTokenSource.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(150, token);
                    if (token.IsCancellationRequested) return;

                    var settings = _settingsService.LoadSettings();
                    var suggestionLimit = settings.StoreSuggestionLimit;
                    var results = _appListCacheService.Search(value, suggestionLimit);

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Suggestions.Clear();
                        foreach (var result in results)
                        {
                            Suggestions.Add(result);
                        }
                        ShowSuggestions = Suggestions.Count > 0;
                    });
                }
                catch (TaskCanceledException)
                {
                }
            }, token);
        }

        [RelayCommand]
        private void SelectSuggestion(AppListEntry? suggestion)
        {
            if (suggestion == null) return;

            SearchQuery = suggestion.AppId.ToString();
            ShowSuggestions = false;
            _ = SearchGames();
        }

        [RelayCommand]
        private void HideSuggestions()
        {
            ShowSuggestions = false;
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchQuery = string.Empty;
            ShowSuggestions = false;
            Suggestions.Clear();
            // Reload games to show all
            _ = LoadGamesAsync();
        }

        partial void OnHideSoundtrackChanged(bool value)
        {
            if (!_isUpdatingFilters)
            {
                UpdateSelectAllState();
                ReapplyFilters();
            }
        }

        partial void OnHideDemoChanged(bool value)
        {
            if (!_isUpdatingFilters)
            {
                UpdateSelectAllState();
                ReapplyFilters();
            }
        }

        partial void OnHidePlaytestChanged(bool value)
        {
            if (!_isUpdatingFilters)
            {
                UpdateSelectAllState();
                ReapplyFilters();
            }
        }

        partial void OnHideTrailerChanged(bool value)
        {
            if (!_isUpdatingFilters)
            {
                UpdateSelectAllState();
                ReapplyFilters();
            }
        }

        partial void OnHideArtbookChanged(bool value)
        {
            if (!_isUpdatingFilters)
            {
                UpdateSelectAllState();
                ReapplyFilters();
            }
        }

        partial void OnHideSdkChanged(bool value)
        {
            if (!_isUpdatingFilters)
            {
                UpdateSelectAllState();
                ReapplyFilters();
            }
        }

        partial void OnSelectAllFiltersChanged(bool value)
        {
            if (_isUpdatingFilters) return;

            _isUpdatingFilters = true;
            try
            {
                // Select or deselect all filters
                HideSoundtrack = value;
                HideDemo = value;
                HidePlaytest = value;
                HideTrailer = value;
                HideArtbook = value;
                HideSdk = value;
            }
            finally
            {
                _isUpdatingFilters = false;
            }

            // Apply filters once after all changes
            ReapplyFilters();
        }

        private void UpdateSelectAllState()
        {
            if (_isUpdatingFilters) return;

            _isUpdatingFilters = true;
            try
            {
                // If all filters are checked, check SelectAllFilters
                // If any filter is unchecked, uncheck SelectAllFilters
                SelectAllFilters = HideSoundtrack && HideDemo && HidePlaytest &&
                                   HideTrailer && HideArtbook && HideSdk;
            }
            finally
            {
                _isUpdatingFilters = false;
            }
        }

        private void ReapplyFilters()
        {
            // Re-apply filters to stored unfiltered games without reloading from API
            if (_unfilteredGames.Count == 0)
                return;

            var filteredGames = ApplyFilters(_unfilteredGames);

            Games.Clear();
            foreach (var game in filteredGames)
            {
                Games.Add(game);
            }
        }

        [RelayCommand]
        private async Task LoadGames()
        {
            var apiKey = GetActiveApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                StatusMessage = "Please enter API key in settings";
                MessageBoxHelper.Show(
                    "An API key is required to use the Store.\n\nPlease go to Settings and enter your API key to browse and download games from the library.",
                    "API Key Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // Reset to first page
            CurrentPage = 1;
            CurrentOffset = 0;
            Games.Clear();

            await LoadGamesAsync();
        }

        [RelayCommand]
        private async Task NextPage()
        {
            if (!CanGoNext || IsLoading) return;

            CurrentPage++;
            CurrentOffset = (CurrentPage - 1) * PageSize;
            Games.Clear();
            await LoadGamesAsync();
            ScrollToTopAction?.Invoke();
        }

        [RelayCommand]
        private async Task PreviousPage()
        {
            if (!CanGoPrevious || IsLoading) return;

            CurrentPage--;
            CurrentOffset = (CurrentPage - 1) * PageSize;
            Games.Clear();
            await LoadGamesAsync();
            ScrollToTopAction?.Invoke();
        }

        [RelayCommand]
        private async Task GoToPage(int pageNumber)
        {
            if (pageNumber < 1 || pageNumber > TotalPages || pageNumber == CurrentPage || IsLoading) return;

            CurrentPage = pageNumber;
            CurrentOffset = (CurrentPage - 1) * PageSize;
            Games.Clear();
            await LoadGamesAsync();
            ScrollToTopAction?.Invoke();
        }

        [RelayCommand]
        private async Task GoToPageFromText()
        {
            if (string.IsNullOrWhiteSpace(GoToPageText) || IsLoading) return;

            if (int.TryParse(GoToPageText, out int pageNumber))
            {
                if (pageNumber >= 1 && pageNumber <= TotalPages && pageNumber != CurrentPage)
                {
                    CurrentPage = pageNumber;
                    CurrentOffset = (CurrentPage - 1) * PageSize;
                    Games.Clear();
                    await LoadGamesAsync();
                    ScrollToTopAction?.Invoke();
                }
            }
            GoToPageText = string.Empty;
        }

        private void UpdatePageNumbers()
        {
            PageNumbers.Clear();
            if (TotalPages <= 0) return;

            int maxVisiblePages = 7;
            int startPage = 1;
            int endPage = TotalPages;

            if (TotalPages > maxVisiblePages)
            {
                int halfVisible = maxVisiblePages / 2;
                startPage = System.Math.Max(1, CurrentPage - halfVisible);
                endPage = System.Math.Min(TotalPages, startPage + maxVisiblePages - 1);

                if (endPage - startPage < maxVisiblePages - 1)
                {
                    startPage = System.Math.Max(1, endPage - maxVisiblePages + 1);
                }
            }

            for (int i = startPage; i <= endPage; i++)
            {
                PageNumbers.Add(i);
            }
        }

        [RelayCommand]
        private async Task SearchGames()
        {
            // Close suggestions popup
            ShowSuggestions = false;

            var apiKey = GetActiveApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                StatusMessage = "Please enter API key in settings";
                return;
            }

            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                // If search is empty, load library normally
                await LoadGames();
                return;
            }

            if (SearchQuery.Length < 2)
            {
                StatusMessage = "Enter at least 2 characters to search";
                return;
            }

            IsLoading = true;
            StatusMessage = "Searching...";
            Games.Clear();

            try
            {
                var result = await _manifestApiService.SearchLibraryAsync(SearchQuery, apiKey, 100);

                if (result != null && result.Results.Count > 0)
                {
                    // Store unfiltered games for re-filtering later
                    _unfilteredGames = new List<LibraryGame>(result.Results);

                    // Apply filters
                    var filteredGames = ApplyFilters(result.Results);

                    foreach (var game in filteredGames)
                    {
                        Games.Add(game);
                    }

                    TotalCount = result.TotalMatches;
                    CurrentPage = 1;
                    TotalPages = 1;
                    CanGoPrevious = false;
                    CanGoNext = false;
                    StatusMessage = $"Found {result.ReturnedCount} of {result.TotalMatches} matching games";

                    // Check installation status
                    UpdateInstallationStatus(result.Results);

                    // Load all icons in parallel
                    _ = LoadAllGameIconsAsync(result.Results);
                }
                else
                {
                    StatusMessage = "No games found";
                    TotalCount = 0;
                    CurrentPage = 1;
                    TotalPages = 0;
                    CanGoPrevious = false;
                    CanGoNext = false;
                }
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Search failed: {ex.Message}";
                MessageBoxHelper.Show(
                    $"Failed to search: {ex.Message}",
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
        private async Task ChangeSortBy(string sortBy)
        {
            if (SortBy == sortBy) return;

            SortBy = sortBy;
            CurrentOffset = 0;
            Games.Clear();
            await LoadGamesAsync();
        }

        private async Task LoadGamesAsync()
        {
            var settings = _settingsService.LoadSettings();
            var apiKey = GetActiveApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                StatusMessage = "Please enter API key in settings";
                return;
            }

            IsLoading = true;
            StatusMessage = "Loading games...";

            try
            {
                var result = await _manifestApiService.GetLibraryAsync(
                    apiKey,
                    limit: PageSize,
                    offset: CurrentOffset,
                    sortBy: SortBy);

                if (result != null && result.Games.Count > 0)
                {
                    Games.Clear();

                    // Store unfiltered games for re-filtering later
                    _unfilteredGames = new List<LibraryGame>(result.Games);

                    // Apply filters
                    var filteredGames = ApplyFilters(result.Games);

                    foreach (var game in filteredGames)
                    {
                        Games.Add(game);
                    }

                    TotalCount = result.TotalCount;
                    TotalPages = (int)System.Math.Ceiling((double)TotalCount / PageSize);

                    CanGoPrevious = CurrentPage > 1;
                    CanGoNext = CurrentPage < TotalPages;
                    UpdatePageNumbers();

                    var startIndex = CurrentOffset + 1;
                    var endIndex = System.Math.Min(CurrentOffset + result.Games.Count, TotalCount);
                    StatusMessage = $"Showing {startIndex}-{endIndex} of {TotalCount} games (Page {CurrentPage} of {TotalPages})";

                    // Check installation status
                    UpdateInstallationStatus(result.Games);

                    // Load all icons in parallel
                    _ = LoadAllGameIconsAsync(result.Games);
                }
                else
                {
                    StatusMessage = "No games available";
                    TotalCount = 0;
                    TotalPages = 0;
                    CanGoPrevious = false;
                    CanGoNext = false;
                    UpdatePageNumbers();
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

        private bool ShouldFilterGame(LibraryGame game)
        {
            var name = game.GameName.ToLower();

            if (HideSoundtrack)
            {
                // Match "soundtrack" or "soundtracks" as whole words
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bsoundtracks?\b"))
                    return true;
                // Match " OST" or "OST " or " OST " as whole word
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bost\b"))
                    return true;
            }

            if (HideDemo)
            {
                // Match "demo" as a whole word (not "demolition")
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bdemo\b"))
                    return true;
            }

            if (HidePlaytest)
            {
                // Match "playtest" as whole words
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bplaytest\b"))
                    return true;
            }

            if (HideTrailer)
            {
                // Match "trailer" as whole words
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\btrailer\b"))
                    return true;
            }

            if (HideArtbook)
            {
                // Match "artbook" as whole words
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bartbook\b"))
                    return true;
            }

            if (HideSdk)
            {
                // Match "sdk" as whole word
                if (System.Text.RegularExpressions.Regex.IsMatch(name, @"\bsdk\b"))
                    return true;
            }

            return false;
        }

        private List<LibraryGame> ApplyFilters(List<LibraryGame> games)
        {
            if (!HideSoundtrack && !HideDemo && !HidePlaytest && !HideTrailer && !HideArtbook && !HideSdk)
                return games;

            return games.Where(game => !ShouldFilterGame(game)).ToList();
        }

        private async Task LoadAllGameIconsAsync(List<LibraryGame> games)
        {
            // Create tasks for all games
            var iconTasks = games
                .Where(g => !string.IsNullOrEmpty(g.HeaderImage))
                .Select(game => LoadGameIconAsync(game))
                .ToList();

            // Wait for all to complete (with semaphore limiting concurrency)
            await Task.WhenAll(iconTasks);
        }

        private async Task LoadGameIconAsync(LibraryGame game)
        {
            await _iconLoadSemaphore.WaitAsync();
            try
            {
                var iconPath = await _cacheService.GetIconAsync(game.GameId, game.HeaderImage);

                // Update on UI thread
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    game.CachedIconPath = iconPath;
                });
            }
            catch
            {
                // Silently fail for individual icons
            }
            finally
            {
                _iconLoadSemaphore.Release();
            }
        }

        [RelayCommand]
        private async Task DownloadGame(LibraryGame game)
        {
            var settings = _settingsService.LoadSettings();
            var apiKey = GetActiveApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBoxHelper.Show(
                    "Please enter API key in settings",
                    "API Key Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!game.ManifestAvailable)
            {
                MessageBoxHelper.Show(
                    $"Manifest for '{game.GameName}' is not available yet.",
                    "Not Available",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                var baseUrl = GetActiveBaseUrl();

                // Create a manifest object for download
                var manifest = new Manifest
                {
                    AppId = game.GameId,
                    Name = game.GameName,
                    IconUrl = game.HeaderImage,
                    Size = game.ManifestSize ?? 0,
                    DownloadUrl = $"{baseUrl}/manifest/{game.GameId}"
                };

                StatusMessage = $"Downloading: {game.GameName}";
                var zipFilePath = await _downloadService.DownloadGameFileOnlyAsync(manifest, settings.DownloadsPath, apiKey);

                StatusMessage = $"{game.GameName} downloaded successfully";

                MessageBoxHelper.Show(
                    $"{game.GameName} has been downloaded!\n\nGo to the Downloads page to install it.",
                    "Download Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Download failed: {ex.Message}";
                MessageBoxHelper.Show(
                    $"Failed to download {game.GameName}: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task UpdateGame(LibraryGame game)
        {
            var settings = _settingsService.LoadSettings();
            var apiKey = GetActiveApiKey();

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBoxHelper.Show(
                    "Please enter API key in settings",
                    "API Key Required",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!game.ManifestAvailable)
            {
                MessageBoxHelper.Show(
                    $"Manifest for '{game.GameName}' is not available yet.",
                    "Not Available",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var installedInfo = _manifestStorageService.GetInstalledManifest(game.GameId);
            if (installedInfo == null)
            {
                MessageBoxHelper.Show(
                    $"No installation info found for '{game.GameName}'.\nPlease download and install the game first.",
                    "Not Installed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                var baseUrl = GetActiveBaseUrl();

                var manifest = new Manifest
                {
                    AppId = game.GameId,
                    Name = game.GameName,
                    IconUrl = game.HeaderImage,
                    Size = game.ManifestSize ?? 0,
                    DownloadUrl = $"{baseUrl}/manifest/{game.GameId}"
                };

                StatusMessage = $"Downloading update: {game.GameName}";
                var zipFilePath = await _downloadService.DownloadGameFileOnlyAsync(manifest, settings.DownloadsPath, apiKey);

                StatusMessage = $"{game.GameName} update downloaded";

                MessageBoxHelper.Show(
                    $"Update for {game.GameName} has been downloaded!\n\nGo to the Downloads page to install the update.\n\nNote: Delta downloading will only download changed files.",
                    "Update Downloaded",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                game.HasUpdate = false;
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Update download failed: {ex.Message}";
                MessageBoxHelper.Show(
                    $"Failed to download update for {game.GameName}: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void UpdateInstallationStatus(List<LibraryGame> games)
        {
            foreach (var game in games)
            {
                var installedInfo = _manifestStorageService.GetInstalledManifest(game.GameId);
                game.IsInstalled = installedInfo != null;
                game.HasUpdate = false;
            }
        }
    }
}
