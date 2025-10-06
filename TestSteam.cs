using SolusManifestApp.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SolusManifestApp
{
    public class TestSteam
    {
        public static void RunTest()
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "SteamDebug.txt");

            try
            {
                using (var writer = new StreamWriter(logPath))
                {
                    writer.WriteLine("=== Testing Steam Detection ===");
                    writer.WriteLine($"Time: {DateTime.Now}");

                    var steamService = new SteamService();
                    var steamPath = steamService.GetSteamPath();

                    writer.WriteLine($"Steam Path: {steamPath ?? "NULL"}");

                    if (string.IsNullOrEmpty(steamPath))
                    {
                        writer.WriteLine("ERROR: Steam path not detected!");
                        return;
                    }

                    // Test library folders parsing
                    var libraryFoldersFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(libraryFoldersFile))
                    {
                        libraryFoldersFile = Path.Combine(steamPath, "config", "libraryfolders.vdf");
                    }

                    writer.WriteLine($"Using libraryfolders.vdf: {libraryFoldersFile}");
                    writer.WriteLine($"  Exists: {File.Exists(libraryFoldersFile)}");

                    if (File.Exists(libraryFoldersFile))
                    {
                        writer.WriteLine("\nParsing VDF file...");
                        try
                        {
                            var vdfData = SolusManifestApp.Helpers.VdfParser.Parse(libraryFoldersFile);
                            writer.WriteLine($"  Root keys: {string.Join(", ", vdfData.Keys)}");

                            var libFoldersObj = SolusManifestApp.Helpers.VdfParser.GetObject(vdfData, "libraryfolders");
                            if (libFoldersObj != null)
                            {
                                writer.WriteLine($"  libraryfolders keys: {string.Join(", ", libFoldersObj.Keys)}");

                                foreach (var kvp in libFoldersObj)
                                {
                                    writer.WriteLine($"  Key '{kvp.Key}': {kvp.Value?.GetType().Name ?? "null"}");
                                    if (kvp.Value is Dictionary<string, object> folderData)
                                    {
                                        var path = SolusManifestApp.Helpers.VdfParser.GetValue(folderData, "path");
                                        writer.WriteLine($"    path = {path}");
                                    }
                                }
                            }
                            else
                            {
                                writer.WriteLine("  ERROR: libraryfolders object is NULL");
                            }
                        }
                        catch (Exception ex)
                        {
                            writer.WriteLine($"  ERROR parsing VDF: {ex.Message}");
                            writer.WriteLine($"  Stack: {ex.StackTrace}");
                        }
                    }

                    var steamappsPath = Path.Combine(steamPath, "steamapps");
                    writer.WriteLine($"Steamapps folder: {steamappsPath}");
                    writer.WriteLine($"  Exists: {Directory.Exists(steamappsPath)}");

                    if (Directory.Exists(steamappsPath))
                    {
                        var manifests = Directory.GetFiles(steamappsPath, "appmanifest_*.acf");
                        writer.WriteLine($"  Manifest files found: {manifests.Length}");

                        if (manifests.Length > 0)
                        {
                            writer.WriteLine($"\nTesting first manifest: {Path.GetFileName(manifests[0])}");
                            try
                            {
                                var testContent = File.ReadAllText(manifests[0]);
                                writer.WriteLine($"  File size: {testContent.Length} bytes");
                                writer.WriteLine($"  First 200 chars: {testContent.Substring(0, Math.Min(200, testContent.Length))}");
                            }
                            catch (Exception ex)
                            {
                                writer.WriteLine($"  Error reading: {ex.Message}");
                            }
                        }
                    }

                    var steamGamesService = new SteamGamesService(steamService);
                    var games = steamGamesService.GetInstalledGames();

                    writer.WriteLine($"\nFound {games.Count} games:");
                    foreach (var game in games.Take(10))
                    {
                        writer.WriteLine($"  - {game.Name} (AppID: {game.AppId}, Size: {game.SizeOnDisk})");
                    }

                    if (games.Count > 10)
                    {
                        writer.WriteLine($"  ... and {games.Count - 10} more");
                    }
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText(logPath, $"ERROR: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
