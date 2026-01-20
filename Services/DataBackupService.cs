using SolusManifestApp.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace SolusManifestApp.Services
{
    public class DataBackupService
    {
        private readonly SteamService _steamService;
        private readonly LoggerService _logger;

        public DataBackupService(SteamService steamService, LoggerService logger)
        {
            _steamService = steamService;
            _logger = logger;
        }

        /// <summary>
        /// Gets the current Steam user by reading loginusers.vdf
        /// </summary>
        public SteamUser? GetCurrentSteamUser()
        {
            try
            {
                var steamPath = _steamService.GetSteamPath();
                if (string.IsNullOrEmpty(steamPath))
                {
                    _logger.Log("ERROR", "Steam path not found");
                    return null;
                }

                var loginUsersPath = Path.Combine(steamPath, "config", "loginusers.vdf");
                if (!File.Exists(loginUsersPath))
                {
                    _logger.Log("ERROR", $"loginusers.vdf not found at {loginUsersPath}");
                    return null;
                }

                var vdfContent = File.ReadAllText(loginUsersPath);
                var users = ParseLoginUsers(vdfContent);

                if (users.Count == 0)
                {
                    _logger.Log("ERROR", "No users found in loginusers.vdf");
                    return null;
                }

                // Find user with MostRecent = "1"
                var currentUser = users.FirstOrDefault(u => u.IsMostRecent);

                // If no MostRecent flag, use the one with newest timestamp
                if (currentUser == null)
                {
                    currentUser = users.OrderByDescending(u => u.Timestamp).FirstOrDefault();
                }

                if (currentUser != null)
                {
                    _logger.Log("INFO", $"Found current Steam user: {currentUser.PersonaName} (AccountId: {currentUser.AccountId})");
                }

                return currentUser;
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to get current Steam user: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses loginusers.vdf and extracts user information
        /// </summary>
        private List<SteamUser> ParseLoginUsers(string vdfContent)
        {
            var users = new List<SteamUser>();

            try
            {
                var lines = vdfContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                SteamUser? currentUser = null;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();

                    // Check if this is a SteamID64 (17-digit number in quotes)
                    if (trimmed.StartsWith("\"") && trimmed.Contains("\""))
                    {
                        var key = ExtractQuotedString(trimmed);
                        if (!string.IsNullOrEmpty(key) && key.Length == 17 && long.TryParse(key, out var steamId64))
                        {
                            // This is a SteamID64, start a new user
                            currentUser = new SteamUser
                            {
                                SteamId64 = key,
                                AccountId = ConvertSteamId64ToAccountId(steamId64).ToString()
                            };
                            users.Add(currentUser);
                        }
                    }

                    if (currentUser != null)
                    {
                        if (trimmed.Contains("\"AccountName\""))
                        {
                            currentUser.AccountName = ExtractQuotedValue(trimmed);
                        }
                        else if (trimmed.Contains("\"PersonaName\""))
                        {
                            currentUser.PersonaName = ExtractQuotedValue(trimmed);
                        }
                        else if (trimmed.Contains("\"mostrecent\"") || trimmed.Contains("\"MostRecent\""))
                        {
                            var value = ExtractQuotedValue(trimmed);
                            currentUser.IsMostRecent = value == "1";
                        }
                        else if (trimmed.Contains("\"Timestamp\""))
                        {
                            var value = ExtractQuotedValue(trimmed);
                            if (long.TryParse(value, out var timestamp))
                            {
                                currentUser.Timestamp = timestamp;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to parse loginusers.vdf: {ex.Message}");
            }

            return users;
        }

        /// <summary>
        /// Extracts the first quoted string from a line
        /// </summary>
        private string ExtractQuotedString(string line)
        {
            var firstQuote = line.IndexOf('"');
            if (firstQuote == -1) return string.Empty;

            var secondQuote = line.IndexOf('"', firstQuote + 1);
            if (secondQuote == -1) return string.Empty;

            return line.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
        }

        /// <summary>
        /// Extracts the second quoted string (value) from a line like "key" "value"
        /// </summary>
        private string ExtractQuotedValue(string line)
        {
            var firstQuote = line.IndexOf('"');
            if (firstQuote == -1) return string.Empty;

            var secondQuote = line.IndexOf('"', firstQuote + 1);
            if (secondQuote == -1) return string.Empty;

            var thirdQuote = line.IndexOf('"', secondQuote + 1);
            if (thirdQuote == -1) return string.Empty;

            var fourthQuote = line.IndexOf('"', thirdQuote + 1);
            if (fourthQuote == -1) return string.Empty;

            return line.Substring(thirdQuote + 1, fourthQuote - thirdQuote - 1);
        }

        /// <summary>
        /// Converts SteamID64 to AccountId (SteamID3)
        /// </summary>
        private long ConvertSteamId64ToAccountId(long steamId64)
        {
            return steamId64 - 76561197960265728L;
        }

        /// <summary>
        /// Backs up playtime (localconfig.vdf) to a ZIP file
        /// </summary>
        public bool BackupPlaytime(string outputDirectory)
        {
            try
            {
                var steamPath = _steamService.GetSteamPath();
                var user = GetCurrentSteamUser();

                if (string.IsNullOrEmpty(steamPath) || user == null)
                    return false;

                var localConfigPath = Path.Combine(steamPath, "userdata", user.AccountId, "config", "localconfig.vdf");
                if (!File.Exists(localConfigPath))
                {
                    _logger.Log("ERROR", $"localconfig.vdf not found at {localConfigPath}");
                    return false;
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var zipFileName = $"{user.AccountId}_playtime_backup_{timestamp}.zip";
                var zipPath = Path.Combine(outputDirectory, zipFileName);

                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    var entryName = Path.Combine("userdata", user.AccountId, "config", "localconfig.vdf");
                    archive.CreateEntryFromFile(localConfigPath, entryName);
                }

                _logger.Log("INFO", $"Playtime backup created: {zipPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to backup playtime: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Backs up achievements to a ZIP file
        /// </summary>
        public bool BackupAchievements(string outputDirectory)
        {
            try
            {
                var steamPath = _steamService.GetSteamPath();
                var user = GetCurrentSteamUser();

                if (string.IsNullOrEmpty(steamPath) || user == null)
                    return false;

                var statsPath = Path.Combine(steamPath, "appcache", "stats");
                if (!Directory.Exists(statsPath))
                {
                    _logger.Log("ERROR", $"Stats directory not found at {statsPath}");
                    return false;
                }

                // Find all UserGameStats files for this user
                var pattern = $"UserGameStats_{user.AccountId}_*.bin";
                var statsFiles = Directory.GetFiles(statsPath, pattern);

                if (statsFiles.Length == 0)
                {
                    _logger.Log("ERROR", $"No achievement files found for user {user.AccountId}");
                    return false;
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var zipFileName = $"{user.AccountId}_achievements_backup_{timestamp}.zip";
                var zipPath = Path.Combine(outputDirectory, zipFileName);

                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var file in statsFiles)
                    {
                        var fileName = Path.GetFileName(file);
                        var entryName = Path.Combine("appcache", "stats", fileName);
                        archive.CreateEntryFromFile(file, entryName);
                    }
                }

                _logger.Log("INFO", $"Achievements backup created: {zipPath} ({statsFiles.Length} files)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to backup achievements: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Backs up added games (stplug-in folder) to a ZIP file
        /// </summary>
        public bool BackupAddedGames(string outputDirectory)
        {
            try
            {
                var steamPath = _steamService.GetSteamPath();
                var user = GetCurrentSteamUser();

                if (string.IsNullOrEmpty(steamPath) || user == null)
                    return false;

                var stPluginPath = Path.Combine(steamPath, "config", "stplug-in");
                if (!Directory.Exists(stPluginPath))
                {
                    _logger.Log("ERROR", $"stplug-in directory not found at {stPluginPath}");
                    return false;
                }

                var luaFiles = Directory.GetFiles(stPluginPath, "*.lua");
                if (luaFiles.Length == 0)
                {
                    _logger.Log("ERROR", "No .lua files found in stplug-in folder");
                    return false;
                }

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var zipFileName = $"{user.AccountId}_st_games_backup_{timestamp}.zip";
                var zipPath = Path.Combine(outputDirectory, zipFileName);

                using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                {
                    foreach (var file in luaFiles)
                    {
                        var fileName = Path.GetFileName(file);
                        var entryName = Path.Combine("config", "stplug-in", fileName);
                        archive.CreateEntryFromFile(file, entryName);
                    }
                }

                _logger.Log("INFO", $"Added games backup created: {zipPath} ({luaFiles.Length} files)");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to backup added games: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores playtime from a backup ZIP file
        /// </summary>
        public bool RestorePlaytime(string zipFilePath)
        {
            try
            {
                var steamPath = _steamService.GetSteamPath();
                if (string.IsNullOrEmpty(steamPath))
                    return false;

                if (!File.Exists(zipFilePath))
                {
                    _logger.Log("ERROR", $"Backup file not found: {zipFilePath}");
                    return false;
                }

                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("localconfig.vdf", StringComparison.OrdinalIgnoreCase))
                        {
                            // Extract to Steam directory, preserving structure
                            var destinationPath = Path.Combine(steamPath, entry.FullName);
                            var destinationDir = Path.GetDirectoryName(destinationPath);

                            if (!string.IsNullOrEmpty(destinationDir))
                            {
                                Directory.CreateDirectory(destinationDir);
                            }

                            entry.ExtractToFile(destinationPath, overwrite: true);
                            _logger.Log("INFO", $"Restored playtime to {destinationPath}");
                            return true;
                        }
                    }
                }

                _logger.Log("ERROR", "No localconfig.vdf found in backup");
                return false;
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to restore playtime: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores achievements from a backup ZIP file
        /// </summary>
        public bool RestoreAchievements(string zipFilePath)
        {
            try
            {
                var steamPath = _steamService.GetSteamPath();
                if (string.IsNullOrEmpty(steamPath))
                    return false;

                if (!File.Exists(zipFilePath))
                {
                    _logger.Log("ERROR", $"Backup file not found: {zipFilePath}");
                    return false;
                }

                int restoredCount = 0;
                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.Contains("UserGameStats_") && entry.FullName.EndsWith(".bin"))
                        {
                            var destinationPath = Path.Combine(steamPath, entry.FullName);
                            var destinationDir = Path.GetDirectoryName(destinationPath);

                            if (!string.IsNullOrEmpty(destinationDir))
                            {
                                Directory.CreateDirectory(destinationDir);
                            }

                            entry.ExtractToFile(destinationPath, overwrite: true);
                            restoredCount++;
                        }
                    }
                }

                _logger.Log("INFO", $"Restored {restoredCount} achievement files");
                return restoredCount > 0;
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to restore achievements: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Restores added games from a backup ZIP file
        /// </summary>
        public bool RestoreAddedGames(string zipFilePath)
        {
            try
            {
                var steamPath = _steamService.GetSteamPath();
                if (string.IsNullOrEmpty(steamPath))
                    return false;

                if (!File.Exists(zipFilePath))
                {
                    _logger.Log("ERROR", $"Backup file not found: {zipFilePath}");
                    return false;
                }

                int restoredCount = 0;
                using (var archive = ZipFile.OpenRead(zipFilePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                        {
                            var destinationPath = Path.Combine(steamPath, entry.FullName);
                            var destinationDir = Path.GetDirectoryName(destinationPath);

                            if (!string.IsNullOrEmpty(destinationDir))
                            {
                                Directory.CreateDirectory(destinationDir);
                            }

                            entry.ExtractToFile(destinationPath, overwrite: true);
                            restoredCount++;
                        }
                    }
                }

                _logger.Log("INFO", $"Restored {restoredCount} .lua files");
                return restoredCount > 0;
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to restore added games: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets list of games that have achievement data
        /// </summary>
        public List<Game> GetGamesWithAchievements()
        {
            var games = new List<Game>();

            try
            {
                var steamPath = _steamService.GetSteamPath();
                var user = GetCurrentSteamUser();

                if (string.IsNullOrEmpty(steamPath) || user == null)
                    return games;

                var statsPath = Path.Combine(steamPath, "appcache", "stats");
                if (!Directory.Exists(statsPath))
                    return games;

                var pattern = $"UserGameStats_{user.AccountId}_*.bin";
                var statsFiles = Directory.GetFiles(statsPath, pattern);

                foreach (var file in statsFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var parts = fileName.Split('_');

                    // Expected format: UserGameStats_<accountid>_<appid>
                    if (parts.Length >= 3 && parts[0] == "UserGameStats")
                    {
                        var appId = parts[2];
                        games.Add(new Game
                        {
                            AppId = appId,
                            Name = appId, // Will be resolved by ViewModel using SteamApiService
                            FilePath = file
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to get games with achievements: {ex.Message}");
            }

            return games;
        }

        /// <summary>
        /// Resets achievements for specified games
        /// </summary>
        public bool ResetAchievements(List<string> appIds)
        {
            try
            {
                var steamPath = _steamService.GetSteamPath();
                var user = GetCurrentSteamUser();

                if (string.IsNullOrEmpty(steamPath) || user == null)
                    return false;

                var statsPath = Path.Combine(steamPath, "appcache", "stats");
                if (!Directory.Exists(statsPath))
                    return false;

                int deletedCount = 0;
                foreach (var appId in appIds)
                {
                    var fileName = $"UserGameStats_{user.AccountId}_{appId}.bin";
                    var filePath = Path.Combine(statsPath, fileName);

                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        deletedCount++;
                        _logger.Log("INFO", $"Deleted achievement file for app {appId}");
                    }
                }

                _logger.Log("INFO", $"Reset achievements for {deletedCount} games");
                return deletedCount > 0;
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to reset achievements: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resets achievements for all games
        /// </summary>
        public bool ResetAllAchievements()
        {
            try
            {
                var steamPath = _steamService.GetSteamPath();
                var user = GetCurrentSteamUser();

                if (string.IsNullOrEmpty(steamPath) || user == null)
                    return false;

                var statsPath = Path.Combine(steamPath, "appcache", "stats");
                if (!Directory.Exists(statsPath))
                    return false;

                var pattern = $"UserGameStats_{user.AccountId}_*.bin";
                var statsFiles = Directory.GetFiles(statsPath, pattern);

                foreach (var file in statsFiles)
                {
                    File.Delete(file);
                }

                _logger.Log("INFO", $"Deleted all {statsFiles.Length} achievement files");
                return statsFiles.Length > 0;
            }
            catch (Exception ex)
            {
                _logger.Log("ERROR", $"Failed to reset all achievements: {ex.Message}");
                return false;
            }
        }
    }
}
