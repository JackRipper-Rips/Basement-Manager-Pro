using SolusManifestApp.Models;
using Newtonsoft.Json;
using System;
using System.IO;

namespace SolusManifestApp.Services
{
    public class SettingsService
    {
        private readonly string _settingsPath;
        private AppSettings? _settings;

        public SettingsService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appData, "SolusManifestApp");
            Directory.CreateDirectory(appFolder);
            _settingsPath = Path.Combine(appFolder, "settings.json");
        }

        public AppSettings LoadSettings()
        {
            if (_settings != null)
                return _settings;

            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    _settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    _settings = new AppSettings();
                    SaveSettings(_settings);
                }
            }
            catch
            {
                _settings = new AppSettings();
            }

            // Set default downloads path if empty
            if (string.IsNullOrEmpty(_settings.DownloadsPath))
            {
                _settings.DownloadsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "SolusManifestApp",
                    "Downloads"
                );
                Directory.CreateDirectory(_settings.DownloadsPath);
            }

            return _settings;
        }

        public void SaveSettings(AppSettings settings)
        {
            try
            {
                _settings = settings;
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save settings: {ex.Message}", ex);
            }
        }

        public void AddApiKeyToHistory(string apiKey)
        {
            var settings = LoadSettings();
            if (!settings.ApiKeyHistory.Contains(apiKey))
            {
                settings.ApiKeyHistory.Add(apiKey);
                if (settings.ApiKeyHistory.Count > 10)
                {
                    settings.ApiKeyHistory.RemoveAt(0);
                }
                SaveSettings(settings);
            }
        }
    }
}
