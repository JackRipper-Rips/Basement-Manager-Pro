using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SolusManifestApp.Services
{
    public class LuaFileManager
    {
        private readonly string _stpluginPath;

        public LuaFileManager(string stpluginPath)
        {
            _stpluginPath = stpluginPath;
        }

        public (List<string> luaFiles, List<string> disabledFiles) FindLuaFiles()
        {
            var luaFiles = new List<string>();
            var disabledFiles = new List<string>();

            try
            {
                if (!Directory.Exists(_stpluginPath))
                    return (luaFiles, disabledFiles);

                foreach (var file in Directory.GetFiles(_stpluginPath))
                {
                    var fileName = Path.GetFileName(file);
                    var extension = Path.GetExtension(file);

                    // Check for .lua files (not steamtools.lua)
                    if (extension == ".lua" && fileName.Count(c => c == '.') == 1)
                    {
                        if (!fileName.Equals("steamtools.lua", StringComparison.OrdinalIgnoreCase))
                        {
                            luaFiles.Add(file);
                        }
                    }
                    // Check for .lua.disabled files
                    else if (fileName.EndsWith(".lua.disabled", StringComparison.OrdinalIgnoreCase) && fileName.Count(c => c == '.') == 2)
                    {
                        if (!fileName.Equals("steamtools.lua.disabled", StringComparison.OrdinalIgnoreCase))
                        {
                            disabledFiles.Add(file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error reading stplug-in directory: {ex.Message}", ex);
            }

            return (luaFiles, disabledFiles);
        }

        public string ExtractAppId(string filePath)
        {
            var filename = Path.GetFileName(filePath);
            return filename.Replace(".lua", "").Replace(".disabled", "");
        }

        public string PatchLuaFile(string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var lines = content.Split('\n').ToList();

                // Check if updates are already disabled
                if (lines.Any(line => line.Contains("-- LUATOOLS: UPDATES DISABLED!")))
                {
                    var modified = false;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        var trimmed = lines[i].Trim();
                        if (trimmed.StartsWith("--setManifestid"))
                        {
                            lines[i] = lines[i].Substring(2); // Remove --
                            modified = true;
                        }
                    }

                    if (modified)
                    {
                        File.WriteAllText(filePath, string.Join("\n", lines));
                        return "updates_disabled_modified";
                    }
                    return "updates_disabled";
                }

                // Check if file has addappid
                var hasAddAppId = lines.Any(line => line.ToLower().Contains("addappid"));
                if (!hasAddAppId)
                {
                    return "no_addappid";
                }

                // Patch setManifestid lines
                var wasModified = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("setManifestid"))
                    {
                        lines[i] = "--" + lines[i];
                        wasModified = true;
                    }
                }

                if (wasModified)
                {
                    File.WriteAllText(filePath, string.Join("\n", lines));
                    return "patched";
                }

                return "no_changes";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error patching {filePath}: {ex.Message}", ex);
            }
        }

        public (bool success, string message) DisableGame(string appId)
        {
            var luaFile = Path.Combine(_stpluginPath, $"{appId}.lua");
            if (!File.Exists(luaFile))
            {
                return (false, $"Lua file not found for {appId}");
            }

            var disabledFile = Path.Combine(_stpluginPath, $"{appId}.lua.disabled");
            if (File.Exists(disabledFile))
            {
                return (false, $"Disabled file already exists for {appId}");
            }

            try
            {
                File.Move(luaFile, disabledFile);
                return (true, $"Successfully disabled {appId}");
            }
            catch (Exception ex)
            {
                return (false, $"Error disabling game: {ex.Message}");
            }
        }

        public (bool success, string message) EnableGame(string appId)
        {
            var disabledFile = Path.Combine(_stpluginPath, $"{appId}.lua.disabled");
            if (!File.Exists(disabledFile))
            {
                return (false, $"Disabled file not found for {appId}");
            }

            var luaFile = Path.Combine(_stpluginPath, $"{appId}.lua");
            if (File.Exists(luaFile))
            {
                return (false, $"Lua file already exists for {appId}");
            }

            try
            {
                File.Move(disabledFile, luaFile);
                return (true, $"Successfully enabled {appId}");
            }
            catch (Exception ex)
            {
                return (false, $"Error enabling game: {ex.Message}");
            }
        }

        public (bool success, string message) DeleteLuaFile(string appId)
        {
            var luaFile = Path.Combine(_stpluginPath, $"{appId}.lua");
            var disabledFile = Path.Combine(_stpluginPath, $"{appId}.lua.disabled");

            string? fileToDelete = null;

            if (File.Exists(luaFile))
            {
                fileToDelete = luaFile;
            }
            else if (File.Exists(disabledFile))
            {
                fileToDelete = disabledFile;
            }
            else
            {
                return (false, $"Lua file not found for {appId}");
            }

            try
            {
                File.Delete(fileToDelete);
                return (true, $"Successfully deleted {appId}");
            }
            catch (Exception ex)
            {
                return (false, $"Error deleting game: {ex.Message}");
            }
        }

        public (bool success, string message) DisableUpdatesForApp(string appId)
        {
            var luaFilePath = Path.Combine(_stpluginPath, $"{appId}.lua");
            if (!File.Exists(luaFilePath))
            {
                return (false, $"Could not find {appId}.lua file");
            }

            try
            {
                var content = File.ReadAllText(luaFilePath);
                var lines = content.Split('\n').ToList();

                if (content.Contains("-- LUATOOLS: UPDATES DISABLED!"))
                {
                    return (false, $"Updates for {appId} are already disabled");
                }

                lines.Insert(0, "-- LUATOOLS: UPDATES DISABLED!");

                for (int i = 0; i < lines.Count; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("--setManifestid"))
                    {
                        lines[i] = lines[i].Substring(2); // Remove --
                    }
                }

                File.WriteAllText(luaFilePath, string.Join("\n", lines));
                return (true, $"Successfully disabled updates for {appId}");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to disable updates: {ex.Message}");
            }
        }

        public (bool success, string message) EnableUpdatesForApp(string appId, string filePath)
        {
            try
            {
                var content = File.ReadAllText(filePath);
                var lines = content.Split('\n').ToList();

                lines.RemoveAll(line => line.Contains("-- LUATOOLS: UPDATES DISABLED!"));

                for (int i = 0; i < lines.Count; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (trimmed.StartsWith("setManifestid"))
                    {
                        lines[i] = "--" + lines[i];
                    }
                }

                File.WriteAllText(filePath, string.Join("\n", lines));
                return (true, $"Successfully enabled updates for {appId}");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to enable updates: {ex.Message}");
            }
        }

        public (bool success, string message) EnableAutoUpdatesForApp(string appId)
        {
            var luaFilePath = Path.Combine(_stpluginPath, $"{appId}.lua");
            if (!File.Exists(luaFilePath))
            {
                return (false, $"Could not find {appId}.lua file");
            }

            try
            {
                var content = File.ReadAllText(luaFilePath);
                var lines = content.Split('\n').ToList();
                bool modified = false;

                for (int i = 0; i < lines.Count; i++)
                {
                    var trimmed = lines[i].Trim();
                    // If setManifestid is not commented, comment it out
                    if (trimmed.StartsWith("setManifestid") && !trimmed.StartsWith("--"))
                    {
                        lines[i] = "--" + lines[i];
                        modified = true;
                    }
                }

                if (modified)
                {
                    File.WriteAllText(luaFilePath, string.Join("\n", lines));
                    return (true, $"Successfully enabled auto-updates for {appId}");
                }

                return (true, $"No changes needed for {appId}");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to enable auto-updates for {appId}: {ex.Message}");
            }
        }

        public (bool success, string message) EnableAutoUpdatesForAll()
        {
            try
            {
                var (luaFiles, _) = FindLuaFiles();
                if (luaFiles.Count == 0)
                {
                    return (false, "No .lua files found");
                }

                int processedCount = 0;
                foreach (var luaFile in luaFiles)
                {
                    var content = File.ReadAllText(luaFile);
                    var lines = content.Split('\n').ToList();
                    bool modified = false;

                    for (int i = 0; i < lines.Count; i++)
                    {
                        var trimmed = lines[i].Trim();
                        // If setManifestid is not commented, comment it out
                        if (trimmed.StartsWith("setManifestid") && !trimmed.StartsWith("--"))
                        {
                            lines[i] = "--" + lines[i];
                            modified = true;
                        }
                    }

                    if (modified)
                    {
                        File.WriteAllText(luaFile, string.Join("\n", lines));
                        processedCount++;
                    }
                }

                return (true, $"Successfully enabled auto-updates for {processedCount} file(s)");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to enable auto-updates: {ex.Message}");
            }
        }

        public List<string> GetDisabledUpdatesAppIds()
        {
            var disabledAppIds = new List<string>();

            try
            {
                var (luaFiles, _) = FindLuaFiles();

                foreach (var luaFile in luaFiles)
                {
                    var content = File.ReadAllText(luaFile);
                    if (content.Contains("-- LUATOOLS: UPDATES DISABLED!"))
                    {
                        var appId = ExtractAppId(luaFile);
                        disabledAppIds.Add(appId);
                    }
                }
            }
            catch
            {
                // Ignore errors
            }

            return disabledAppIds;
        }
    }
}
