using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace HavenDevTools.Services
{
    /// <summary>
    /// Service for inspecting museum bundles (integrates with S.M.U.T.)
    /// </summary>
    public class BundleInspector
    {
        public BundleInspector()
        {
            Plugin.Log?.LogDebug("[BundleInspector] Initialized");
        }

        /// <summary>
        /// Get all museum sections (requires S.M.U.T.)
        /// </summary>
        public List<MuseumSectionInfo> GetAllSections()
        {
            var result = new List<MuseumSectionInfo>();

            if (!Plugin.HasSMUT) return result;

            try
            {
                var smutAssembly = GetSMUTAssembly();
                if (smutAssembly == null) return result;

                var museumContentType = smutAssembly.GetType("SunHavenMuseumUtilityTracker.Data.MuseumContent");
                if (museumContentType == null) return result;

                var getSectionsMethod = museumContentType.GetMethod("GetAllSections", BindingFlags.Public | BindingFlags.Static);
                if (getSectionsMethod == null) return result;

                var sections = getSectionsMethod.Invoke(null, null) as System.Collections.IList;
                if (sections == null) return result;

                foreach (var section in sections)
                {
                    var sectionInfo = new MuseumSectionInfo
                    {
                        Name = section.GetType().GetProperty("Name")?.GetValue(section)?.ToString() ?? "Unknown",
                        Bundles = new List<MuseumBundleInfo>()
                    };

                    var bundlesProp = section.GetType().GetProperty("Bundles");
                    if (bundlesProp != null)
                    {
                        var bundles = bundlesProp.GetValue(section) as System.Collections.IList;
                        if (bundles != null)
                        {
                            foreach (var bundle in bundles)
                            {
                                var bundleInfo = new MuseumBundleInfo
                                {
                                    Name = bundle.GetType().GetProperty("Name")?.GetValue(bundle)?.ToString() ?? "Unknown",
                                    Items = new List<MuseumItemInfo>()
                                };

                                var bundleId = bundle.GetType().GetProperty("Id")?.GetValue(bundle)?.ToString() ?? "";
                                var sectionId = section.GetType().GetProperty("Id")?.GetValue(section)?.ToString() ?? "";

                                // For aquarium bundles, try S.M.U.T. resolved fish IDs (from game ItemInfoDatabase + FishData)
                                var resolvedItems = TryGetResolvedAquariumItems(smutAssembly, bundleId);
                                if (resolvedItems != null && resolvedItems.Count > 0)
                                {
                                    foreach (var (gameItemId, name) in resolvedItems)
                                    {
                                        bundleInfo.Items.Add(new MuseumItemInfo
                                        {
                                            Id = $"{bundleId}_fish_{gameItemId}",
                                            Name = name ?? $"Item {gameItemId}",
                                            GameItemId = gameItemId,
                                            Quantity = 1
                                        });
                                    }
                                }
                                else
                                {
                                    var itemsProp = bundle.GetType().GetProperty("Items");
                                    if (itemsProp != null)
                                    {
                                        var items = itemsProp.GetValue(bundle) as System.Collections.IList;
                                        if (items != null)
                                        {
                                            foreach (var item in items)
                                            {
                                                string itemName = item.GetType().GetProperty("Name")?.GetValue(item)?.ToString() ?? "Unknown";
                                                var itemInfo = new MuseumItemInfo
                                                {
                                                    Id = item.GetType().GetProperty("Id")?.GetValue(item)?.ToString() ?? "",
                                                    Name = itemName,
                                                    GameItemId = Convert.ToInt32(item.GetType().GetProperty("GameItemId")?.GetValue(item) ?? 0),
                                                    Quantity = MuseumItemInfo.ParseQuantityFromName(itemName)
                                                };
                                                bundleInfo.Items.Add(itemInfo);
                                            }
                                        }
                                    }
                                }

                                sectionInfo.Bundles.Add(bundleInfo);
                            }
                        }
                    }

                    result.Add(sectionInfo);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[BundleInspector] Error getting sections: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get donation status (requires S.M.U.T.)
        /// </summary>
        public DonationStats GetDonationStats()
        {
            var stats = new DonationStats();

            if (!Plugin.HasSMUT) return stats;

            try
            {
                var smutAssembly = GetSMUTAssembly();
                if (smutAssembly == null) return stats;

                var pluginType = smutAssembly.GetType("SunHavenMuseumUtilityTracker.Plugin");
                if (pluginType == null) return stats;

                var getDonationManagerMethod = pluginType.GetMethod("GetDonationManager", BindingFlags.Public | BindingFlags.Static);
                if (getDonationManagerMethod == null) return stats;

                var donationManager = getDonationManagerMethod.Invoke(null, null);
                if (donationManager == null) return stats;

                // Check if loaded
                var isLoadedProp = donationManager.GetType().GetProperty("IsLoaded");
                if (isLoadedProp != null && !(bool)isLoadedProp.GetValue(donationManager))
                {
                    return stats;
                }

                // Get overall stats
                var getOverallStatsMethod = donationManager.GetType().GetMethod("GetOverallStats");
                if (getOverallStatsMethod != null)
                {
                    var overallStats = getOverallStatsMethod.Invoke(donationManager, null);
                    if (overallStats != null)
                    {
                        var donatedField = overallStats.GetType().GetField("donated") ??
                                           overallStats.GetType().GetField("Item1");
                        var totalField = overallStats.GetType().GetField("total") ??
                                         overallStats.GetType().GetField("Item2");

                        if (donatedField != null)
                            stats.TotalDonated = Convert.ToInt32(donatedField.GetValue(overallStats));
                        if (totalField != null)
                            stats.TotalItems = Convert.ToInt32(totalField.GetValue(overallStats));
                    }
                }

                // Get completion percent
                var getPercentMethod = donationManager.GetType().GetMethod("GetOverallCompletionPercent");
                if (getPercentMethod != null)
                {
                    stats.CompletionPercent = (float)getPercentMethod.Invoke(donationManager, null);
                }

                // Get current character
                var charProp = donationManager.GetType().GetProperty("CurrentCharacter");
                if (charProp != null)
                {
                    stats.CharacterName = charProp.GetValue(donationManager)?.ToString() ?? "Unknown";
                }

                stats.IsLoaded = true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[BundleInspector] Error getting donation stats: {ex.Message}");
            }

            return stats;
        }

        /// <summary>
        /// Check if an item has been donated (requires S.M.U.T.)
        /// </summary>
        public bool HasDonated(string itemId)
        {
            if (!Plugin.HasSMUT) return false;

            try
            {
                var smutAssembly = GetSMUTAssembly();
                if (smutAssembly == null) return false;

                var pluginType = smutAssembly.GetType("SunHavenMuseumUtilityTracker.Plugin");
                var getDonationManagerMethod = pluginType?.GetMethod("GetDonationManager", BindingFlags.Public | BindingFlags.Static);
                var donationManager = getDonationManagerMethod?.Invoke(null, null);

                if (donationManager == null) return false;

                var hasDonatedMethod = donationManager.GetType().GetMethod("HasDonated", new[] { typeof(string) });
                if (hasDonatedMethod != null)
                {
                    return (bool)hasDonatedMethod.Invoke(donationManager, new object[] { itemId });
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[BundleInspector] Error checking donation: {ex.Message}");
            }

            return false;
        }

        private Assembly GetSMUTAssembly()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "SunHavenMuseumUtilityTracker")
                {
                    return assembly;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets resolved aquarium fish IDs from S.M.U.T. (ItemInfoDatabase + FishData by season).
        /// Returns null if S.M.U.T. doesn't have resolved data for this bundle.
        /// </summary>
        private static List<(int GameItemId, string Name)> TryGetResolvedAquariumItems(Assembly smutAssembly, string bundleId)
        {
            if (smutAssembly == null || string.IsNullOrEmpty(bundleId)) return null;

            try
            {
                var museumContentType = smutAssembly.GetType("SunHavenMuseumUtilityTracker.Data.MuseumContent");
                var method = museumContentType?.GetMethod("GetResolvedAquariumItems", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                var result = method?.Invoke(null, new object[] { bundleId });
                if (result is System.Collections.IList list && list.Count > 0)
                {
                    var typed = new List<(int, string)>();
                    foreach (var entry in list)
                    {
                        var t = entry.GetType();
                        var item1Field = t.GetField("Item1", BindingFlags.Public | BindingFlags.Instance);
                        var item2Field = t.GetField("Item2", BindingFlags.Public | BindingFlags.Instance);
                        int id = item1Field != null ? Convert.ToInt32(item1Field.GetValue(entry)) : 0;
                        string name = item2Field != null ? item2Field.GetValue(entry)?.ToString() : null;
                        typed.Add((id, name ?? $"Item {id}"));
                    }
                    return typed;
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }
    }

    public class MuseumSectionInfo
    {
        public string Name { get; set; }
        public List<MuseumBundleInfo> Bundles { get; set; }
    }

    public class MuseumBundleInfo
    {
        public string Name { get; set; }
        public List<MuseumItemInfo> Items { get; set; }
    }

    public class MuseumItemInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int GameItemId { get; set; }
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Parse quantity from item name patterns like "(x25,000)" or "(x20)"
        /// </summary>
        public static int ParseQuantityFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return 1;

            // Match patterns like "(x25,000)", "(x20)", "(x1,000,000)"
            var match = Regex.Match(name, @"\(x([\d,]+)\)");
            if (match.Success)
            {
                // Remove commas and parse
                string numberStr = match.Groups[1].Value.Replace(",", "");
                if (int.TryParse(numberStr, out int qty))
                {
                    return qty;
                }
            }

            return 1;
        }
    }

    public class DonationStats
    {
        public bool IsLoaded { get; set; }
        public string CharacterName { get; set; }
        public int TotalDonated { get; set; }
        public int TotalItems { get; set; }
        public float CompletionPercent { get; set; }
    }
}
