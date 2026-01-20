using SolusManifestApp.Interfaces;
using SolusManifestApp.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SolusManifestApp.Services
{
    /// <summary>
    /// Factory service that delegates to the correct store API implementation based on user settings
    /// </summary>
    public class StoreApiFactory : IManifestApiService
    {
        private readonly ManifestApiService _morrenusService;
        private readonly BasementApiService _basementService;
        private readonly SettingsService _settingsService;

        public StoreApiFactory(
            ManifestApiService morrenusService,
            BasementApiService basementService,
            SettingsService settingsService)
        {
            _morrenusService = morrenusService;
            _basementService = basementService;
            _settingsService = settingsService;
        }

        private IManifestApiService GetActiveService()
        {
            var settings = _settingsService.LoadSettings();
            return settings.SelectedStore == StoreProvider.Basement
                ? _basementService
                : _morrenusService;
        }

        public string GetActiveApiKey()
        {
            var settings = _settingsService.LoadSettings();
            return settings.SelectedStore == StoreProvider.Basement
                ? settings.BasementApiKey
                : settings.ApiKey;
        }

        public Task<Manifest?> GetManifestAsync(string appId, string apiKey)
        {
            return GetActiveService().GetManifestAsync(appId, apiKey);
        }

        public Task<List<Manifest>?> SearchGamesAsync(string query, string apiKey)
        {
            return GetActiveService().SearchGamesAsync(query, apiKey);
        }

        public Task<List<Manifest>?> GetAllGamesAsync(string apiKey)
        {
            return GetActiveService().GetAllGamesAsync(apiKey);
        }

        public bool ValidateApiKey(string apiKey)
        {
            return GetActiveService().ValidateApiKey(apiKey);
        }

        public Task<bool> TestApiKeyAsync(string apiKey)
        {
            return GetActiveService().TestApiKeyAsync(apiKey);
        }

        public Task<GameStatus?> GetGameStatusAsync(string appId, string apiKey)
        {
            return GetActiveService().GetGameStatusAsync(appId, apiKey);
        }

        public Task<LibraryResponse?> GetLibraryAsync(string apiKey, int limit = 100, int offset = 0, string? search = null, string sortBy = "updated")
        {
            return GetActiveService().GetLibraryAsync(apiKey, limit, offset, search, sortBy);
        }

        public Task<SearchResponse?> SearchLibraryAsync(string query, string apiKey, int limit = 50)
        {
            return GetActiveService().SearchLibraryAsync(query, apiKey, limit);
        }
    }
}
