using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SolusManifestApp.Interfaces;
using SolusManifestApp.Models;

namespace SolusManifestApp.Services
{
    public class FixApplierService
    {
        private readonly HttpClient _httpClient;
        private readonly ISteamService _steamService;
        private readonly ISettingsService _settingsService;

        public FixApplierService(HttpClient httpClient, ISteamService steamService, ISettingsService settingsService)
        {
            _httpClient = httpClient;
            _steamService = steamService;
            _settingsService = settingsService;
        }

        private string GetFixUrl(int appId)
        {
            var settings = _settingsService.LoadSettings();
            var baseUrl = settings.FixBaseUrl;
            return baseUrl.Replace("{appid}", appId.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> CheckForFix(int appId)
        {
            try
            {
                var url = GetFixUrl(appId);
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetAppName(int appId)
        {
            try
            {
                var url = $"https://store.steampowered.com/api/appdetails?appids={appId}";
                var response = await _httpClient.GetStringAsync(url);

                using (JsonDocument doc = JsonDocument.Parse(response))
                {
                    JsonElement root = doc.RootElement;
                    if (root.TryGetProperty(appId.ToString(), out JsonElement appData) &&
                        appData.TryGetProperty("success", out JsonElement success) &&
                        success.GetBoolean() &&
                        appData.TryGetProperty("data", out JsonElement data) &&
                        data.TryGetProperty("name", out JsonElement name))
                    {
                        return name.GetString() ?? $"Unknown Game ({appId})";
                    }
                }
            }
            catch { }
            return $"Unknown Game ({appId})";
        }

        public async Task ApplyFixFromUrl(int appId, string installPath, Action<string> log, Func<string?> getPasswordCallback)
        {
            var url = GetFixUrl(appId);
            log($"Downloading fix from {url}...");

            var tempFile = Path.GetTempFileName();
            try
            {
                using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }

                log("Download complete. Extracting...");
                await ApplyFixFromFile(tempFile, appId, installPath, log, url, getPasswordCallback);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        public async Task ApplyFixFromFile(string archivePath, int appId, string installPath, Action<string> log, string downloadUrl = "local file", Func<string?>? getPasswordCallback = null)
        {
            await Task.Run(() => {
                string? password = null;
                bool isEncrypted = false;

                try
                {
                    using (var archive = ArchiveFactory.Open(archivePath))
                    {
                        if (archive.Entries.Any(e => e.IsEncrypted))
                        {
                             isEncrypted = true;
                        }
                    }
                }
                catch (SharpCompress.Common.CryptographicException) { isEncrypted = true; }
                catch (InvalidFormatException ex) when (ex.Message.ToLower().Contains("password")) { isEncrypted = true; }

                if (isEncrypted)
                {
                    log("Archive is password protected.");
                    password = getPasswordCallback?.Invoke();
                    if (string.IsNullOrEmpty(password))
                    {
                        log("No password provided. Aborting.");
                        throw new Exception("Password not provided for protected archive.");
                    }
                    log("Password provided, proceeding with extraction.");
                }

                try
                {
                    ExtractArchive(archivePath, appId, installPath, log, downloadUrl, password);
                }
                catch(SharpCompress.Common.CryptographicException ex)
                {
                    log($"Failed to extract archive: Invalid password? {ex.Message}");
                    throw new Exception("Extraction failed, likely due to an invalid password.", ex);
                }
                catch (Exception ex)
                {
                    log($"Failed to extract archive: {ex.Message}");
                    throw;
                }
            });
        }

        private string FindCommonBasePath(IArchive archive)
        {
            var filePaths = archive.Entries.Where(e => !e.IsDirectory).Select(e => e.Key).ToList();
            if (filePaths.Count < 2)
            {
                return "";
            }

            var firstPathParts = filePaths[0].Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            var commonPrefixParts = new List<string>(firstPathParts.Take(firstPathParts.Length - 1));

            foreach (var path in filePaths.Skip(1))
            {
                if (commonPrefixParts.Count == 0) break;

                var pathParts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                int j = 0;
                while (j < commonPrefixParts.Count && j < pathParts.Length && commonPrefixParts[j] == pathParts[j])
                {
                    j++;
                }

                if (j < commonPrefixParts.Count)
                {
                    commonPrefixParts.RemoveRange(j, commonPrefixParts.Count - j);
                }
            }

            if (commonPrefixParts.Count > 0)
            {
                return string.Join("/", commonPrefixParts) + "/";
            }

            return "";
        }

        private void ExtractArchive(string archivePath, int appId, string installPath, Action<string> log, string downloadUrl, string? password)
        {
            var extractedFiles = new List<string>();
            var backedUpFiles = new List<string>();

            string backupPath = Path.Combine(installPath, "fixbackup", DateTime.Now.ToString("yyyyMMddHHmmss"));
            Directory.CreateDirectory(backupPath);

            var readerOptions = new ReaderOptions() { Password = password };

            using (var archive = ArchiveFactory.Open(archivePath, readerOptions))
            {
                 // Test password by trying to read the first entry
                if (archive.Entries.Any(e=> e.IsEncrypted))
                {
                    try
                    {
                        using var stream = archive.Entries.First(e => !e.IsDirectory).OpenEntryStream();
                    }
                    catch (Exception)
                    {
                        throw new SharpCompress.Common.CryptographicException("Invalid password.");
                    }
                }

                string basePathInZip = FindCommonBasePath(archive);
                if (!string.IsNullOrEmpty(basePathInZip))
                {
                    log($"Detected common base path in zip: '{basePathInZip}'. Will be stripped.");
                }

                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    var relativePath = entry.Key;
                    if (!string.IsNullOrEmpty(basePathInZip) && relativePath.StartsWith(basePathInZip))
                    {
                        relativePath = relativePath.Substring(basePathInZip.Length);
                    }
                    if (string.IsNullOrEmpty(relativePath)) continue;

                    var destinationPath = Path.Combine(installPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(destinationPath))
                    {
                        var backupFilePath = Path.Combine(backupPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(backupFilePath)!);
                        File.Copy(destinationPath, backupFilePath, true);
                        backedUpFiles.Add(relativePath.Replace('\\', '/'));
                        log($"Backed up: {relativePath}");
                    }
                }

                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    var relativePath = entry.Key;
                    if (!string.IsNullOrEmpty(basePathInZip) && relativePath.StartsWith(basePathInZip))
                    {
                        relativePath = relativePath.Substring(basePathInZip.Length);
                    }
                    if (string.IsNullOrEmpty(relativePath)) continue;

                    var destinationPath = Path.Combine(installPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    var destinationDir = Path.GetDirectoryName(destinationPath);
                    Directory.CreateDirectory(destinationDir!);

                    entry.WriteToFile(destinationPath, new ExtractionOptions() { Overwrite = true });

                    extractedFiles.Add(relativePath.Replace('\\', '/'));
                    log($"Extracted: {relativePath}");
                }
            }

            log("Extraction complete. Creating log file...");
            CreateLogFile(appId, installPath, extractedFiles, backedUpFiles, backupPath, downloadUrl, log);
            log("Fix applied successfully!");
        }

        private void CreateLogFile(int appId, string installPath, List<string> extractedFiles, List<string> backedUpFiles, string backupPath, string downloadUrl, Action<string> log)
        {
            try
            {
                var logFilePath = Path.Combine(installPath, $"basement-fix-log-{appId}.log");
                var gameName = GetAppName(appId).Result;

                var sb = new StringBuilder();
                if (File.Exists(logFilePath))
                {
                    sb.Append(File.ReadAllText(logFilePath));
                    if (!sb.ToString().EndsWith("\n"))
                    {
                        sb.AppendLine();
                    }
                    sb.AppendLine("\n---");
                }

                sb.AppendLine("[FIX]");
                sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Game: {gameName}");
                sb.AppendLine("Fix Type: Generic");
                sb.AppendLine($"Download URL: {downloadUrl}");
                sb.AppendLine($"Backup Path: {backupPath}");
                sb.AppendLine("Files:");
                foreach (var file in extractedFiles)
                {
                    sb.AppendLine(file);
                }

                if (backedUpFiles.Count > 0)
                {
                    sb.AppendLine("BackedUp:");
                    foreach (var file in backedUpFiles)
                    {
                        sb.AppendLine(file);
                    }
                }

                sb.AppendLine("[/FIX]");

                File.WriteAllText(logFilePath, sb.ToString());
                log("Log file created.");

                FixHistoryManager.AddEntry(new FixHistoryEntry
                {
                    AppId = appId,
                    GameName = gameName,
                    InstallPath = installPath,
                    FixDate = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                log($"Error creating log file: {ex.Message}");
            }
        }

        public async Task Unfix(int appId, string installPath, Action<string> log)
        {
            await Task.Run(() =>
            {
                var logFilePath = Path.Combine(installPath, $"basement-fix-log-{appId}.log");
                var logs = ParseLogFile(logFilePath);

                if (logs.Count == 0)
                {
                    log("No fix log found. Cannot un-fix.");
                    return;
                }

                var lastFix = logs.OrderByDescending(l => l.Date).First();

                log($"Reverting fix from {lastFix.Date}");

                foreach (var file in lastFix.BackedUpFiles)
                {
                    var backupFilePath = Path.Combine(lastFix.BackupPath, file);
                    var destinationPath = Path.Combine(installPath, file);
                    if (File.Exists(backupFilePath))
                    {
                        File.Copy(backupFilePath, destinationPath, true);
                        log($"Restored: {file}");
                    }
                }

                foreach (var file in lastFix.ExtractedFiles)
                {
                    var filePath = Path.Combine(installPath, file);
                    if (File.Exists(filePath) && !lastFix.BackedUpFiles.Contains(file.Replace('\\', '/')))
                    {
                        File.Delete(filePath);
                        log($"Deleted: {file}");
                    }
                }

                var content = File.ReadAllText(logFilePath);
                var blockToRemove = "[FIX]" + lastFix.OriginalBlock;
                content = content.Replace(blockToRemove, "");
                File.WriteAllText(logFilePath, content.Trim());

                try
                {
                    var backupPath = lastFix.BackupPath;
                    if (Directory.Exists(backupPath))
                    {
                        Directory.Delete(backupPath, true);
                        log($"Deleted backup folder: {backupPath}");

                        var parentDir = Directory.GetParent(backupPath)?.FullName;
                        if(parentDir != null && Directory.Exists(parentDir) && !Directory.EnumerateFileSystemEntries(parentDir).Any())
                        {
                            Directory.Delete(parentDir);
                            log($"Deleted empty parent backup folder: {parentDir}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    log($"Could not delete backup folder: {ex.Message}");
                }

                log("Unfix complete.");
                FixHistoryManager.RemoveEntry(appId);
            });
        }

        private class FixLog
        {
            public DateTime Date { get; set; }
            public string Game { get; set; } = "";
            public string FixType { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public string BackupPath { get; set; } = "";
            public List<string> ExtractedFiles { get; set; } = new List<string>();
            public List<string> BackedUpFiles { get; set; } = new List<string>();
            public string OriginalBlock { get; set; } = "";
        }

        private List<FixLog> ParseLogFile(string logFilePath)
        {
            var logs = new List<FixLog>();
            if (!File.Exists(logFilePath)) return logs;

            var content = File.ReadAllText(logFilePath);
            var fixBlocks = content.Split(new[] { "[FIX]" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var block in fixBlocks)
            {
                var log = new FixLog { OriginalBlock = block };
                var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                string? currentSection = null;

                foreach(var line in lines)
                {
                    if (line.StartsWith("Date:"))
                        log.Date = DateTime.ParseExact(line.Substring(5).Trim(), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    else if (line.StartsWith("Game:"))
                        log.Game = line.Substring(5).Trim();
                    else if (line.StartsWith("Fix Type:"))
                        log.FixType = line.Substring(9).Trim();
                    else if (line.StartsWith("Download URL:"))
                        log.DownloadUrl = line.Substring(13).Trim();
                    else if (line.StartsWith("Backup Path:"))
                        log.BackupPath = line.Substring(12).Trim();
                    else if (line.Trim() == "Files:")
                        currentSection = "Files";
                    else if (line.Trim() == "BackedUp:")
                        currentSection = "BackedUp";
                    else if (line.Trim() == "[/FIX]")
                        currentSection = null;
                    else if (currentSection == "Files")
                        log.ExtractedFiles.Add(line.Trim());
                    else if (currentSection == "BackedUp")
                        log.BackedUpFiles.Add(line.Trim());
                }
                logs.Add(log);
            }
            return logs;
        }

        public string? GetGameInstallPath(int appId)
        {
            var steamPath = _steamService.GetSteamPath();
            if (string.IsNullOrEmpty(steamPath))
            {
                return null;
            }

            return _steamService.GetGameInstallPath(appId);
        }
    }
}
