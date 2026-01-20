using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using SolusManifestApp.Models;

namespace SolusManifestApp.Services
{
    public class FixHistoryManager
    {
        private static string GetHistoryPath()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BasementManagerPro"
            );
            Directory.CreateDirectory(appDataPath);
            return Path.Combine(appDataPath, "fixhistory.json");
        }

        public static List<FixHistoryEntry> LoadHistory()
        {
            var historyPath = GetHistoryPath();
            if (File.Exists(historyPath))
            {
                try
                {
                    var json = File.ReadAllText(historyPath);
                    return JsonSerializer.Deserialize<List<FixHistoryEntry>>(json) ?? new List<FixHistoryEntry>();
                }
                catch
                {
                    // In case of corrupt file, return empty list
                    return new List<FixHistoryEntry>();
                }
            }
            return new List<FixHistoryEntry>();
        }

        public static void AddEntry(FixHistoryEntry entry)
        {
            var history = LoadHistory();
            // Remove existing entry for the same appid to keep it on top
            history.RemoveAll(e => e.AppId == entry.AppId);
            history.Insert(0, entry);
            SaveHistory(history);
        }

        public static void RemoveEntry(int appId)
        {
            var history = LoadHistory();
            history.RemoveAll(e => e.AppId == appId);
            SaveHistory(history);
        }

        private static void SaveHistory(List<FixHistoryEntry> history)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(history, options);
            File.WriteAllText(GetHistoryPath(), json);
        }
    }
}
