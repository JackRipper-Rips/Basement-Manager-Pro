using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SolusManifestApp.Services
{
    public class LuaDepotInfo
    {
        public string DepotId { get; set; } = "";
        public string Name { get; set; } = "";
        public long Size { get; set; }
        public bool IsTokenBased { get; set; }
    }

    public class LuaParser
    {
        /// <summary>
        /// Extracts all AppIDs that have addtoken() calls
        /// </summary>
        public HashSet<string> ParseTokenAppIds(string luaContent)
        {
            var tokenAppIds = new HashSet<string>();
            var lines = luaContent.Split('\n');

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Match: addtoken(3282720, "186020997252537705")
                var tokenMatch = Regex.Match(trimmedLine, @"addtoken\((\d+)");
                if (tokenMatch.Success)
                {
                    var appId = tokenMatch.Groups[1].Value;
                    tokenAppIds.Add(appId);
                }
            }

            return tokenAppIds;
        }

        public List<LuaDepotInfo> ParseDepotsFromLua(string luaContent)
        {
            var depots = new List<LuaDepotInfo>();
            var lines = luaContent.Split('\n');
            var depotMap = new Dictionary<string, LuaDepotInfo>();

            // Get token-based AppIDs to filter them out
            var tokenAppIds = ParseTokenAppIds(luaContent);

            // Track current DLC context by looking for DLC section comments
            string? currentDlcId = null;

            // First pass: Parse addappid lines to get depot IDs and names
            for (int i = 0; i < lines.Length; i++)
            {
                var trimmedLine = lines[i].Trim();

                // Check for DLC section comments like "-- SILENT HILL f - Bonus Content (AppID: 3282720)"
                var dlcCommentMatch = Regex.Match(trimmedLine, @"--.*\(AppID:\s*(\d+)\)");
                if (dlcCommentMatch.Success)
                {
                    var dlcId = dlcCommentMatch.Groups[1].Value;
                    // Check if this DLC has a token in the next few lines
                    currentDlcId = null;
                    for (int j = i + 1; j < Math.Min(i + 5, lines.Length); j++)
                    {
                        if (lines[j].Contains($"addtoken({dlcId}"))
                        {
                            currentDlcId = dlcId; // This DLC section has a token
                            break;
                        }
                    }
                    continue;
                }

                // Match: addappid(285311, 1, "hash") -- Rollercoaster Tycoon Content
                var addAppIdMatch = Regex.Match(trimmedLine, @"addappid\((\d+)(?:,.*?)?\)\s*--\s*(.+)");
                if (addAppIdMatch.Success)
                {
                    var depotId = addAppIdMatch.Groups[1].Value;
                    var depotName = addAppIdMatch.Groups[2].Value.Trim();

                    // Check if this depot is token-based
                    bool isTokenBased = tokenAppIds.Contains(depotId) ||
                                       (currentDlcId != null && tokenAppIds.Contains(currentDlcId));

                    if (!depotMap.ContainsKey(depotId))
                    {
                        depotMap[depotId] = new LuaDepotInfo
                        {
                            DepotId = depotId,
                            Name = depotName,
                            Size = 0,
                            IsTokenBased = isTokenBased
                        };
                    }
                }
            }

            // Second pass: Parse setManifestid lines to get sizes
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Match: setManifestid(285311, "2914580416607481530", 856171654)
                var setManifestMatch = Regex.Match(trimmedLine, @"setManifestid\((\d+),\s*""[^""]*"",\s*(\d+)\)");
                if (setManifestMatch.Success)
                {
                    var depotId = setManifestMatch.Groups[1].Value;
                    var size = long.Parse(setManifestMatch.Groups[2].Value);

                    // Check if this depot is token-based
                    bool isTokenBased = tokenAppIds.Contains(depotId);

                    if (depotMap.ContainsKey(depotId))
                    {
                        depotMap[depotId].Size = size;
                    }
                    else
                    {
                        // Depot has manifest but wasn't in addappid (might be shared depot)
                        depotMap[depotId] = new LuaDepotInfo
                        {
                            DepotId = depotId,
                            Name = $"Depot {depotId}",
                            Size = size,
                            IsTokenBased = isTokenBased
                        };
                    }
                }
            }

            // Add depots to list (prefer ones with manifests, but also include DLCs without manifests)
            foreach (var kvp in depotMap)
            {
                depots.Add(kvp.Value);
            }

            return depots;
        }

        public List<LuaDepotInfo> ParseDepotsFromLuaFile(string luaFilePath)
        {
            if (!File.Exists(luaFilePath))
            {
                return new List<LuaDepotInfo>();
            }

            var content = File.ReadAllText(luaFilePath);
            return ParseDepotsFromLua(content);
        }
    }
}
