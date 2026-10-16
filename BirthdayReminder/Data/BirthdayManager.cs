using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BirthdayReminder.Data
{
    /// <summary>
    /// Manages birthday detection and gift tracking.
    /// Uses game's NPCGiftTable data when available, falls back to BirthdayCache.
    /// </summary>
    public class BirthdayManager
    {
        private GiftTrackingData _giftTracking;
        private List<BirthdayDisplayInfo> _todaysBirthdays = new List<BirthdayDisplayInfo>();
        private string _currentCharacter;
        private FavoriteGiftStore _favoriteGiftStore;

        // Cached date for HUD display (avoids logging spam from per-frame calls)
        private string _cachedDateString = "";

        // Staleness check: track the date we last checked so we can detect day changes
        private string _lastCheckedDateKey = "";
        private float _stalenessCheckTimer = 0f;
        private const float STALENESS_CHECK_INTERVAL = 10f; // Check every 10 seconds

        private static readonly System.Random _random = new System.Random();

        // Cached reflection data for game API access
        private static bool _reflectionInitialized = false;
        private static Type _npcManagerType;
        private static Type _npcGiftTableType;
        private static PropertyInfo _npcManagerInstanceProp;
        private static FieldInfo _npcsDictField;
        private static FieldInfo _birthDayField;
        private static FieldInfo _birthMonthField;
        private static FieldInfo _love2Field;
        private static FieldInfo _like2Field;
        private static FieldInfo _gaveGiftForDayField;
        private static FieldInfo _giftTableField; // NPCAI.giftTable is a field, not a property
        private static bool _useGameData = false;
        private static MethodInfo _itemSellDictContainsKey;
        private static MethodInfo _itemSellDictGetItem;

        public event Action OnBirthdaysUpdated;

        public List<BirthdayDisplayInfo> TodaysBirthdays => _todaysBirthdays;
        public bool HasBirthdays => _todaysBirthdays.Count > 0;
        public bool HasUngiftedBirthdays => _todaysBirthdays.Any(b => !b.HasBeenGifted);

        // Status message for notifications (e.g., "Refreshed!")
        private string _statusMessage = "";
        private float _statusMessageTimer = 0f;
        private const float STATUS_MESSAGE_DURATION = 3f;

        public string StatusMessage => _statusMessage;
        public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage) && _statusMessageTimer > 0;

        /// <summary>
        /// Update status message timer (call from Update)
        /// </summary>
        public void UpdateStatusMessage(float deltaTime)
        {
            if (_statusMessageTimer > 0)
            {
                _statusMessageTimer -= deltaTime;
                if (_statusMessageTimer <= 0)
                {
                    _statusMessage = "";
                }
            }
        }

        /// <summary>
        /// Periodic check: if the in-game date has changed since we last checked,
        /// automatically refresh birthday data. Call from Update().
        /// </summary>
        public void CheckForDateChange(float deltaTime)
        {
            _stalenessCheckTimer += deltaTime;
            if (_stalenessCheckTimer < STALENESS_CHECK_INTERVAL)
                return;
            _stalenessCheckTimer = 0f;

            try
            {
                var (year, season, day) = GetCurrentDate();
                if (string.IsNullOrEmpty(season))
                    return;

                string dateKey = $"{year}_{season}_{day}";
                if (dateKey != _lastCheckedDateKey)
                {
                    Plugin.Log?.LogInfo($"[BirthdayManager] Date changed to {season} {day} — refreshing birthdays");
                    _lastCheckedDateKey = dateKey;
                    CheckTodaysBirthdays();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[BirthdayManager] Date check: {ex.Message}");
            }
        }

        /// <summary>
        /// Set a temporary status message
        /// </summary>
        public void SetStatusMessage(string message)
        {
            _statusMessage = message;
            _statusMessageTimer = STATUS_MESSAGE_DURATION;
        }

        /// <summary>
        /// Initialize reflection cache for accessing game's NPC data
        /// </summary>
        private void InitializeReflectionCache()
        {
            if (_reflectionInitialized) return;
            _reflectionInitialized = true;

            try
            {
                // Get NPCManager type and instance
                _npcManagerType = AccessTools.TypeByName("Wish.NPCManager");
                if (_npcManagerType == null)
                {
                    Plugin.Log?.LogDebug("[BirthdayManager] NPCManager type not found - using cache");
                    return;
                }

                // Get NPCManager.Instance
                var singletonType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                if (singletonType != null)
                {
                    var genericType = singletonType.MakeGenericType(_npcManagerType);
                    _npcManagerInstanceProp = genericType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                }

                // Get _npcs dictionary field
                _npcsDictField = AccessTools.Field(_npcManagerType, "_npcs")
                                 ?? AccessTools.Field(_npcManagerType, "npcs");

                // Get NPCGiftTable type for birthday and gift data
                _npcGiftTableType = AccessTools.TypeByName("Wish.NPCGiftTable");
                if (_npcGiftTableType != null)
                {
                    _birthDayField = AccessTools.Field(_npcGiftTableType, "birthDay");
                    _birthMonthField = AccessTools.Field(_npcGiftTableType, "birthMonth");
                    _love2Field = AccessTools.Field(_npcGiftTableType, "love2");
                    _like2Field = AccessTools.Field(_npcGiftTableType, "like2");
                }

                // Get NPCAI type for gaveGiftForDay and giftTable
                var npcaiType = AccessTools.TypeByName("Wish.NPCAI");
                if (npcaiType != null)
                {
                    _gaveGiftForDayField = AccessTools.Field(npcaiType, "gaveGiftForDay");
                    _giftTableField = AccessTools.Field(npcaiType, "giftTable"); // private field in NPCAI
                }

                // Check if we have enough to use game data
                _useGameData = _npcManagerInstanceProp != null &&
                               _npcsDictField != null &&
                               _birthDayField != null &&
                               _birthMonthField != null &&
                               _giftTableField != null;

                if (_useGameData)
                {
                    Plugin.Log?.LogInfo("[BirthdayManager] Game data API initialized - using NPCGiftTable for birthdays");
                }
                else
                {
                    if (_npcManagerInstanceProp == null)
                        Plugin.Log?.LogWarning("[BirthdayManager][Reflect] NPCManager.Instance property not found - check game version");
                    if (_npcsDictField == null)
                        Plugin.Log?.LogWarning("[BirthdayManager][Reflect] NPCManager._npcs field not found - check game version");
                    if (_birthDayField == null)
                        Plugin.Log?.LogWarning("[BirthdayManager][Reflect] NPCGiftTable.birthDay field not found - check game version");
                    if (_birthMonthField == null)
                        Plugin.Log?.LogWarning("[BirthdayManager][Reflect] NPCGiftTable.birthMonth field not found - check game version");
                    if (_giftTableField == null)
                        Plugin.Log?.LogWarning("[BirthdayManager][Reflect] NPCAI.giftTable field not found - check game version");
                    Plugin.Log?.LogWarning("[BirthdayManager] Falling back to hardcoded birthday cache (game API unavailable)");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[BirthdayManager] Error initializing game data API: {ex.Message}");
                _useGameData = false;
            }
        }

        /// <summary>
        /// Manual refresh - used by debug hotkey
        /// </summary>
        public void ManualRefresh()
        {
            Plugin.Log?.LogDebug("[BirthdayManager] Manual refresh triggered");
            CheckTodaysBirthdays();
            SetStatusMessage($"Refreshed! Found {_todaysBirthdays.Count} birthday(s)");
        }

        /// <summary>
        /// Get the current date formatted as "Season XX" (uses cached value to avoid per-frame logging)
        /// </summary>
        public string CurrentDateFormatted => _cachedDateString;

        /// <summary>
        /// Check for birthdays on the current day
        /// </summary>
        public void CheckTodaysBirthdays()
        {
            _todaysBirthdays.Clear();

            try
            {
                var (year, season, day) = GetCurrentDate(logDate: true);
                if (string.IsNullOrEmpty(season)) return;

                // Update cached date string for HUD display
                _cachedDateString = $"{season} {day:D2}";
                _lastCheckedDateKey = $"{year}_{season}_{day}";

                // Reset gift tracking if it's a new day
                var characterName = GetCurrentCharacterName();
                if (_giftTracking == null || !_giftTracking.IsSameDay(year, season, day) || _currentCharacter != characterName)
                {
                    _giftTracking = new GiftTrackingData(characterName, year, season, day);
                    _currentCharacter = characterName;
                    _favoriteGiftStore = FavoriteGiftStore.Load(characterName);
                }

                // Get all NPCs and check birthdays
                var npcBirthdays = GetNPCsWithBirthdayToday(season, day);
                var seenDisplayNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var npc in npcBirthdays)
                {
                    string rawNpcName = npc.NPCName ?? "";
                    string displayNpcName = GiftPatches.NormalizeNpcName(rawNpcName);
                    if (string.IsNullOrEmpty(displayNpcName))
                        continue;

                    // Fuzzy-resolve against the canonical cache names so that
                    // modded or aliased game names still map to a known identity.
                    displayNpcName = FuzzyMatchNpcName(
                        displayNpcName,
                        BirthdayCache.AllBirthdays.Select(b => b.NPCName));

                    // Some game NPC entries use aliases/duplicates (e.g. "Darius+Darius"). Keep one display row.
                    if (!seenDisplayNames.Add(displayNpcName))
                        continue;

                    // Check gift status - prefer game's gaveGiftForDay, fall back to our tracking
                    bool hasGifted = false;
                    if (_gaveGiftForDayField != null && _useGameData)
                    {
                        hasGifted = CheckGaveGiftForDay(rawNpcName) || CheckGaveGiftForDay(displayNpcName);
                    }
                    if (!hasGifted)
                    {
                        hasGifted = _giftTracking.HasGifted(displayNpcName) || _giftTracking.HasGifted(rawNpcName);
                    }

                    // Get gift suggestions - prefer NPC's own gifts from game data, fall back to cache
                    string giftHint;
                    if (npc.LovedGifts.Count > 0)
                    {
                        // Use the NPC's loved gifts from game data (or cache); pick up to 3 at random without Guid/LINQ allocations
                        var randomGifts = TakeRandom(npc.LovedGifts, 3);
                        giftHint = $"Loves: {string.Join(", ", randomGifts)}";
                    }
                    else
                    {
                        // Fall back to cache suggestions
                        var randomGifts = BirthdayCache.GetRandomGiftSuggestions(npc.NPCName, 3);
                        giftHint = randomGifts.Count > 0 ? $"Loves: {string.Join(", ", randomGifts)}" : "";
                    }

                    if (_favoriteGiftStore != null && _favoriteGiftStore.TryGetFavorite(displayNpcName, out string favoriteGift))
                    {
                        giftHint = string.IsNullOrWhiteSpace(giftHint)
                            ? $"Favorite: {favoriteGift}"
                            : $"Favorite: {favoriteGift} | {giftHint}";
                    }

                    // Pass full gift lists for expanded view
                    _todaysBirthdays.Add(new BirthdayDisplayInfo(
                        displayNpcName,
                        hasGifted,
                        giftHint,
                        npc.LovedGifts.ToList(),
                        npc.LikedGifts.ToList()
                    ));
                }

                Plugin.Log?.LogInfo($"[BirthdayManager] Found {_todaysBirthdays.Count} birthdays on {season} {day}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[BirthdayManager] Error checking birthdays: {ex.Message}");
            }

            OnBirthdaysUpdated?.Invoke();
        }

        /// <summary>
        /// Returns up to <paramref name="count"/> items chosen at random using Fisher-Yates shuffle.
        /// Avoids Guid and LINQ allocations.
        /// </summary>
        private static List<string> TakeRandom(IList<string> source, int count)
        {
            if (source == null || source.Count == 0) return new List<string>();
            var list = new List<string>(source);
            if (list.Count <= count) return list;
            // Fisher-Yates shuffle, then take first count
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
            list.RemoveRange(count, list.Count - count);
            return list;
        }

        /// <summary>
        /// Mark an NPC as gifted
        /// </summary>
        public void MarkGifted(string npcName)
        {
            if (_giftTracking == null) return;
            npcName = GiftPatches.NormalizeNpcName(npcName);
            if (string.IsNullOrEmpty(npcName)) return;

            _giftTracking.MarkGifted(npcName);

            var birthday = _todaysBirthdays.FirstOrDefault(b => b.NPCName == npcName);
            if (birthday != null)
            {
                birthday.HasBeenGifted = true;
            }

            OnBirthdaysUpdated?.Invoke();
        }

        public void TryRecordLovedGift(string npcName, object giftedItem)
        {
            if (giftedItem == null || _favoriteGiftStore == null || _todaysBirthdays == null)
                return;

            string normalizedName = GiftPatches.NormalizeNpcName(npcName);
            if (string.IsNullOrEmpty(normalizedName))
                return;

            var birthday = _todaysBirthdays.FirstOrDefault(b => string.Equals(b.NPCName, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (birthday == null || birthday.AllLovedGifts == null || birthday.AllLovedGifts.Count == 0)
                return;

            string giftedName = GetItemName(giftedItem);
            if (string.IsNullOrWhiteSpace(giftedName))
                return;

            bool isLoved = birthday.AllLovedGifts.Any(g =>
                !string.IsNullOrWhiteSpace(g) && string.Equals(g.Trim(), giftedName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!isLoved)
                return;

            _favoriteGiftStore.SetFavorite(normalizedName, giftedName);
            _favoriteGiftStore.Save();
            Plugin.Log?.LogInfo($"[BirthdayManager] Recorded favorite gift for {normalizedName}: {giftedName}");
        }

        /// <summary>
        /// Finds the best-matching known NPC name for a given raw name using
        /// case-insensitive comparison and Levenshtein distance as a fallback.
        /// Returns the matched known name, or the normalized input if no close
        /// match is found. Used so modded or aliased NPC names can still resolve
        /// to their birthday entry.
        /// </summary>
        private string FuzzyMatchNpcName(string rawName, IEnumerable<string> knownNames)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return rawName;

            var normalized = GiftPatches.NormalizeNpcName(rawName);
            // Exact case-insensitive match first
            var exact = knownNames.FirstOrDefault(k =>
                string.Equals(k, normalized, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            // Partial match: raw name contains or is contained by a known name
            var partial = knownNames.FirstOrDefault(k =>
                k.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0
                || normalized.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0);
            if (partial != null) return partial;

            // Levenshtein distance fallback. Keep stricter matching for short names
            // to avoid accidental collisions (e.g., 3-4 character NPC names).
            string bestMatch = null;
            int bestDist = int.MaxValue;
            foreach (var known in knownNames)
            {
                int dist = LevenshteinDistance(normalized, known);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestMatch = known;
                }
            }
            int minLen = bestMatch != null ? Math.Min(normalized.Length, bestMatch.Length) : normalized.Length;
            int maxDist = minLen <= 4 ? 1 : 2;
            return (bestDist <= maxDist && bestMatch != null) ? bestMatch : normalized;
        }

        private static int LevenshteinDistance(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
            if (string.IsNullOrEmpty(b)) return a.Length;

            var al = a.ToLowerInvariant();
            var bl = b.ToLowerInvariant();
            int[,] dp = new int[al.Length + 1, bl.Length + 1];
            for (int i = 0; i <= al.Length; i++) dp[i, 0] = i;
            for (int j = 0; j <= bl.Length; j++) dp[0, j] = j;
            for (int i = 1; i <= al.Length; i++)
                for (int j = 1; j <= bl.Length; j++)
                    dp[i, j] = al[i - 1] == bl[j - 1]
                        ? dp[i - 1, j - 1]
                        : 1 + Math.Min(dp[i - 1, j - 1], Math.Min(dp[i - 1, j], dp[i, j - 1]));
            return dp[al.Length, bl.Length];
        }

        /// <summary>
        /// Reset data when switching to a new character/save
        /// </summary>
        public void ResetForNewCharacter(string newCharacterName)
        {
            Plugin.Log?.LogInfo($"[BirthdayManager] Resetting for new character: {newCharacterName}");

            _currentCharacter = newCharacterName;
            _giftTracking = null;
            _favoriteGiftStore = FavoriteGiftStore.Load(newCharacterName);
            _todaysBirthdays.Clear();

            OnBirthdaysUpdated?.Invoke();
        }

        #region Debug Methods

        /// <summary>
        /// Debug: Add a test birthday for UI testing
        /// </summary>
        public void DebugAddTestBirthday(string npcName, string giftHint = "")
        {
            Plugin.Log?.LogDebug($"[DEBUG] Adding test birthday for: {npcName}");

            // Don't add duplicates
            if (_todaysBirthdays.Any(b => b.NPCName == npcName))
            {
                Plugin.Log?.LogDebug($"[DEBUG] {npcName} already has a birthday entry");
                return;
            }

            _todaysBirthdays.Add(new BirthdayDisplayInfo(npcName, false, giftHint));

            // Initialize gift tracking if needed
            if (_giftTracking == null)
            {
                var (year, season, day) = GetCurrentDate();
                _giftTracking = new GiftTrackingData("Debug", year, season, day);
            }

            OnBirthdaysUpdated?.Invoke();
        }

        /// <summary>
        /// Debug: Clear all birthday entries
        /// </summary>
        public void DebugClearBirthdays()
        {
            Plugin.Log?.LogDebug("[DEBUG] Clearing all birthdays");
            _todaysBirthdays.Clear();
            _giftTracking = null;
            OnBirthdaysUpdated?.Invoke();
        }

        /// <summary>
        /// Debug: Load ALL NPC birthdays from cache (not just today's)
        /// </summary>
        public void DebugLoadAllBirthdays()
        {
            Plugin.Log?.LogDebug("[DEBUG] Loading ALL NPC birthdays from cache...");
            _todaysBirthdays.Clear();

            foreach (var birthday in BirthdayCache.AllBirthdays)
            {
                string displayName = $"{birthday.NPCName} ({birthday.Season} {birthday.Day})";
                _todaysBirthdays.Add(new BirthdayDisplayInfo(displayName, false, ""));
            }

            Plugin.Log?.LogDebug($"[DEBUG] Loaded {_todaysBirthdays.Count} NPC birthdays from cache");
            OnBirthdaysUpdated?.Invoke();
        }

        /// <summary>
        /// Debug: Dump ALL info about a specific NPC (e.g., "Lynn")
        /// </summary>
        public void DebugDumpNPCInfo(string targetNpcName = "Lynn")
        {
            Plugin.Log?.LogDebug($"[DEBUG] ========== DUMPING NPC INFO: {targetNpcName} ==========");

            try
            {
                var npcManagerType = AccessTools.TypeByName("Wish.NPCManager");
                if (npcManagerType == null)
                {
                    Plugin.Log?.LogWarning("[DEBUG] NPCManager type not found");
                    return;
                }

                var npcManager = GetSingletonInstance(npcManagerType);
                if (npcManager == null)
                {
                    Plugin.Log?.LogWarning("[DEBUG] NPCManager instance not found");
                    return;
                }

                var npcsField = AccessTools.Field(npcManagerType, "_npcs")
                                ?? AccessTools.Field(npcManagerType, "npcs");
                var npcsDict = npcsField?.GetValue(npcManager);
                if (npcsDict == null)
                {
                    Plugin.Log?.LogWarning("[DEBUG] NPCs dictionary not found");
                    return;
                }

                var valuesProperty = npcsDict.GetType().GetProperty("Values");
                var values = valuesProperty?.GetValue(npcsDict) as System.Collections.IEnumerable;
                if (values == null) return;

                foreach (var npc in values)
                {
                    if (npc == null) continue;

                    var npcType = npc.GetType();
                    var nameProp = AccessTools.Property(npcType, "NPCName")
                                   ?? AccessTools.Property(npcType, "npcName")
                                   ?? AccessTools.Property(npcType, "Name");
                    string npcName = nameProp?.GetValue(npc)?.ToString();

                    if (string.IsNullOrEmpty(npcName) || !npcName.Equals(targetNpcName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Plugin.Log?.LogDebug($"[DEBUG] Found {npcName}! Type: {npcType.FullName}");
                    Plugin.Log?.LogDebug($"[DEBUG] --- PROPERTIES ---");

                    // Dump all properties
                    foreach (var prop in npcType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    {
                        try
                        {
                            var value = prop.GetValue(npc);
                            string valueStr = value?.ToString() ?? "null";
                            if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                            Plugin.Log?.LogDebug($"[DEBUG]   {prop.Name} ({prop.PropertyType.Name}) = {valueStr}");
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log?.LogDebug($"[DEBUG]   {prop.Name} ({prop.PropertyType.Name}) = ERROR: {ex.Message}");
                        }
                    }

                    Plugin.Log?.LogDebug($"[DEBUG] --- FIELDS ---");

                    // Dump all fields
                    foreach (var field in npcType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    {
                        try
                        {
                            var value = field.GetValue(npc);
                            string valueStr = value?.ToString() ?? "null";
                            if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                            Plugin.Log?.LogDebug($"[DEBUG]   {field.Name} ({field.FieldType.Name}) = {valueStr}");
                        }
                        catch (Exception ex)
                        {
                            Plugin.Log?.LogDebug($"[DEBUG]   {field.Name} ({field.FieldType.Name}) = ERROR: {ex.Message}");
                        }
                    }

                    // Note: NPCAI does not have characterData/CharacterData - those are on GameSave.CurrentSave.
                    // NPCAI has giftTable (field) for birthday/gift data.
                    var giftTableField = AccessTools.Field(npcType, "giftTable");
                    if (giftTableField != null)
                    {
                        var gt = giftTableField.GetValue(npc);
                        Plugin.Log?.LogDebug($"[DEBUG] giftTable (field) = {gt?.GetType().Name ?? "null"}");
                    }

                    Plugin.Log?.LogDebug($"[DEBUG] ========== END {npcName} ==========");
                    return;
                }

                Plugin.Log?.LogWarning($"[DEBUG] NPC '{targetNpcName}' not found in NPCManager");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[DEBUG] Error dumping NPC info: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// Get the current in-game date using DayCycle
        /// </summary>
        /// <param name="logDate">If true, logs the date (only use for actual birthday checks, not per-frame HUD updates)</param>
        private (int year, string season, int day) GetCurrentDate(bool logDate = false)
        {
            try
            {
                // Skip date lookup on LoadScreen - GameSave.CurrentWorld may be null
                var gameSaveType = AccessTools.TypeByName("Wish.GameSave");
                if (gameSaveType != null)
                {
                    var instanceProp = AccessTools.Property(gameSaveType, "Instance");
                    var instance = instanceProp?.GetValue(null);
                    if (instance != null)
                    {
                        var currentSaveProp = AccessTools.Property(gameSaveType, "CurrentSave");
                        var currentSave = currentSaveProp?.GetValue(instance);
                        if (currentSave == null)
                            return (0, "", 0); // No save loaded (e.g. main menu / LoadScreen)
                    }
                }

                var dayCycleType = AccessTools.TypeByName("Wish.DayCycle");
                if (dayCycleType != null)
                {
                    var yearProp = AccessTools.Property(dayCycleType, "Year");
                    var monthDayProp = AccessTools.Property(dayCycleType, "MonthDay");

                    int year = yearProp != null ? (int)yearProp.GetValue(null) : 1;
                    int day = monthDayProp != null ? (int)monthDayProp.GetValue(null) : 1;

                    var dayCycleInstance = GetSingletonInstance(dayCycleType);
                    string season = "Spring";
                    if (dayCycleInstance != null)
                    {
                        var seasonProp = AccessTools.Property(dayCycleType, "Season");
                        if (seasonProp != null)
                        {
                            var seasonValue = seasonProp.GetValue(dayCycleInstance);
                            season = seasonValue?.ToString() ?? "Spring";
                        }
                    }

                    if (logDate)
                        Plugin.Log?.LogDebug($"[BirthdayManager] Got date from DayCycle: Year {year}, {season} {day}");
                    return (year, season, day);
                }
            }
            catch (Exception ex)
            {
                // Expected on LoadScreen/main menu when DayCycle/CurrentWorld not ready
                Plugin.Log?.LogDebug($"[BirthdayManager] Date unavailable (may be on LoadScreen): {ex.Message}");
            }

            return (0, "", 0);
        }

        /// <summary>
        /// Get singleton instance using reflection
        /// </summary>
        private object GetSingletonInstance(Type targetType)
        {
            try
            {
                // Try to find SingletonBehaviour<T> base class
                var singletonBaseType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                if (singletonBaseType != null)
                {
                    var genericType = singletonBaseType.MakeGenericType(targetType);
                    var instanceProp = AccessTools.Property(genericType, "Instance");
                    return instanceProp?.GetValue(null);
                }

                // Alternative: look for static Instance property directly
                var directInstanceProp = AccessTools.Property(targetType, "Instance");
                return directInstanceProp?.GetValue(null);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[BirthdayManager] GetSingletonInstance: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the current character name
        /// </summary>
        private string GetCurrentCharacterName()
        {
            try
            {
                var gameSaveType = AccessTools.TypeByName("Wish.GameSave");
                if (gameSaveType == null)
                    return "Unknown";

                var gameSave = GetSingletonInstance(gameSaveType);
                if (gameSave == null)
                    return "Unknown";

                var currentCharProp = AccessTools.Property(gameSaveType, "CurrentCharacter");
                var currentChar = currentCharProp?.GetValue(gameSave);
                if (currentChar == null)
                    return "Unknown";

                var nameProp = AccessTools.Property(currentChar.GetType(), "characterName");
                var name = nameProp?.GetValue(currentChar) as string;
                return string.IsNullOrEmpty(name) ? "Unknown" : name;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[BirthdayManager] Error getting character name: {ex.Message}");
            }

            return "Unknown";
        }

        /// <summary>
        /// Find NPCs whose birthday is today.
        /// Uses game's NPCGiftTable data when available, falls back to hardcoded cache.
        /// </summary>
        private List<NPCBirthday> GetNPCsWithBirthdayToday(string season, int day)
        {
            // Initialize reflection cache on first call
            InitializeReflectionCache();

            // Try game data first
            if (_useGameData)
            {
                var gameBirthdays = GetBirthdaysFromGameData(season, day);
                if (gameBirthdays.Count > 0)
                {
                    foreach (var birthday in gameBirthdays)
                    {
                        Plugin.Log?.LogDebug($"[BirthdayManager] Found birthday (game data): {birthday.NPCName} ({birthday.Season} {birthday.Day})");
                    }
                    return gameBirthdays;
                }
            }

            // Fall back to hardcoded cache
            var cacheBirthdays = BirthdayCache.GetBirthdaysForDate(season, day);
            foreach (var birthday in cacheBirthdays)
            {
                Plugin.Log?.LogDebug($"[BirthdayManager] Found birthday (cache): {birthday.NPCName} ({birthday.Season} {birthday.Day})");
            }
            return cacheBirthdays;
        }

        /// <summary>
        /// Get birthdays from game's NPCManager._npcs dictionary using NPCGiftTable data.
        /// </summary>
        private List<NPCBirthday> GetBirthdaysFromGameData(string season, int day)
        {
            var birthdays = new List<NPCBirthday>();

            try
            {
                // Get NPCManager instance
                var npcManager = _npcManagerInstanceProp?.GetValue(null);
                if (npcManager == null)
                {
                    Plugin.Log?.LogDebug("[BirthdayManager] NPCManager instance is null");
                    return birthdays;
                }

                // Get _npcs dictionary
                var npcsDict = _npcsDictField?.GetValue(npcManager);
                if (npcsDict == null)
                {
                    Plugin.Log?.LogDebug("[BirthdayManager] NPCs dictionary is null");
                    return birthdays;
                }

                // Iterate through NPCs
                var valuesProperty = npcsDict.GetType().GetProperty("Values");
                var values = valuesProperty?.GetValue(npcsDict) as System.Collections.IEnumerable;
                if (values == null) return birthdays;

                foreach (var npc in values)
                {
                    if (npc == null) continue;

                    try
                    {
                        // Get NPC name
                        var npcType = npc.GetType();
                        var nameProp = AccessTools.Property(npcType, "NPCName")
                                       ?? AccessTools.Property(npcType, "ActualNPCName")
                                       ?? AccessTools.Property(npcType, "OriginalName")
                                       ?? AccessTools.Property(npcType, "npcName")
                                       ?? AccessTools.Property(npcType, "Name");
                        string npcName = nameProp?.GetValue(npc)?.ToString();
                        if (string.IsNullOrEmpty(npcName)) continue;

                        // Get NPCGiftTable - NPCAI.giftTable is a private field (not characterData/CharacterData)
                        var giftTable = _giftTableField?.GetValue(npc);
                        if (giftTable == null) continue;

                        // Get birthDay and birthMonth from NPCGiftTable
                        int birthDay = 0;
                        object birthMonth = null;

                        if (_birthDayField != null)
                            birthDay = (int)_birthDayField.GetValue(giftTable);

                        if (_birthMonthField != null)
                            birthMonth = _birthMonthField.GetValue(giftTable);

                        string birthSeason = birthMonth?.ToString() ?? "";

                        // Check if birthday matches
                        if (birthDay == day && string.Equals(birthSeason, season, StringComparison.OrdinalIgnoreCase))
                        {
                            var birthday = new NPCBirthday(npcName, birthSeason, birthDay);

                            // Get gift preferences from NPCGiftTable
                            GetGiftsFromGameData(giftTable, birthday);

                            birthdays.Add(birthday);
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogDebug($"[BirthdayManager] Error checking NPC: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[BirthdayManager] Error getting birthdays from game: {ex.Message}");
            }

            return birthdays;
        }

        /// <summary>
        /// Get loved and liked gifts from NPCGiftTable's love2 and like2 fields.
        /// </summary>
        private void GetGiftsFromGameData(object giftTable, NPCBirthday birthday)
        {
            try
            {
                // Get love2 list (loved items)
                if (_love2Field != null)
                {
                    var love2 = _love2Field.GetValue(giftTable) as System.Collections.IList;
                    if (love2 != null)
                    {
                        foreach (var itemId in love2)
                        {
                            string itemName = GetItemNameFromId(itemId);
                            if (!string.IsNullOrEmpty(itemName))
                            {
                                birthday.LovedGifts.Add(itemName);
                            }
                        }
                    }
                }

                // Get like2 list (liked items)
                if (_like2Field != null)
                {
                    var like2 = _like2Field.GetValue(giftTable) as System.Collections.IList;
                    if (like2 != null)
                    {
                        foreach (var itemId in like2)
                        {
                            string itemName = GetItemNameFromId(itemId);
                            if (!string.IsNullOrEmpty(itemName))
                            {
                                birthday.LikedGifts.Add(itemName);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[BirthdayManager] Error getting gifts from game data: {ex.Message}");
            }
        }

        /// <summary>
        /// Get item name from item ID. NPCGiftTable love2/like2 contain SerializedItemData (field 'id'), not raw ints.
        /// Uses ItemInfoDatabase for sync lookup (avoids PSS.Database async callbacks).
        /// </summary>
        private string GetItemNameFromId(object itemIdOrObj)
        {
            try
            {
                int itemId;
                if (itemIdOrObj is int id)
                {
                    itemId = id;
                }
                else if (itemIdOrObj != null)
                {
                    var t = itemIdOrObj.GetType();
                    // SerializedItemData has 'id' as a FIELD, not property
                    var idField = t.GetField("id", BindingFlags.Public | BindingFlags.Instance);
                    var idProp = idField == null ? (t.GetProperty("id") ?? t.GetProperty("ID")) : null;
                    if (idField != null)
                    {
                        itemId = (int)idField.GetValue(itemIdOrObj);
                    }
                    else if (idProp != null)
                    {
                        var v = idProp.GetValue(itemIdOrObj);
                        itemId = v is int i ? i : Convert.ToInt32(v);
                    }
                    else
                    {
                        itemId = Convert.ToInt32(itemIdOrObj);
                    }
                }
                else
                {
                    return null;
                }

                // Use ItemInfoDatabase (sync) - allItemSellInfos: Dictionary<int, ItemSellInfo> with .name field
                var itemInfoDbType = AccessTools.TypeByName("Wish.ItemInfoDatabase");
                if (itemInfoDbType != null)
                {
                    var instanceProp = itemInfoDbType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    var instance = instanceProp?.GetValue(null);
                    if (instance != null)
                    {
                        var allItemSellInfosField = AccessTools.Field(itemInfoDbType, "allItemSellInfos");
                        var dict = allItemSellInfosField?.GetValue(instance);
                        if (dict != null)
                        {
                            if (_itemSellDictContainsKey == null || _itemSellDictGetItem == null)
                            {
                                _itemSellDictContainsKey = dict.GetType().GetMethod("ContainsKey", new[] { typeof(int) });
                                _itemSellDictGetItem = dict.GetType().GetMethod("get_Item", new[] { typeof(int) });
                            }

                            if (_itemSellDictContainsKey != null && _itemSellDictGetItem != null
                                && (bool)_itemSellDictContainsKey.Invoke(dict, new object[] { itemId }))
                            {
                                var info = _itemSellDictGetItem.Invoke(dict, new object[] { itemId });
                                if (info != null)
                                {
                                    var nameField = info.GetType().GetField("name", BindingFlags.Public | BindingFlags.Instance);
                                    var nameProp = nameField == null ? info.GetType().GetProperty("name") ?? info.GetType().GetProperty("Name") : null;
                                    if (nameField != null)
                                        return nameField.GetValue(info) as string;
                                    if (nameProp != null)
                                        return nameProp.GetValue(info) as string;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[BirthdayManager] Error getting item name: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Check if an NPC has been gifted today using game's gaveGiftForDay field.
        /// </summary>
        private bool CheckGaveGiftForDay(string npcName)
        {
            try
            {
                if (_gaveGiftForDayField == null) return false;

                // Get NPC instance
                var npcManager = _npcManagerInstanceProp?.GetValue(null);
                if (npcManager == null) return false;

                var npcsDict = _npcsDictField?.GetValue(npcManager);
                if (npcsDict == null) return false;

                // Try to get the NPC by name from dictionary
                var tryGetValueMethod = npcsDict.GetType().GetMethod("TryGetValue");
                if (tryGetValueMethod != null)
                {
                    var args = new object[] { npcName, null };
                    bool found = (bool)tryGetValueMethod.Invoke(npcsDict, args);
                    if (found && args[1] != null)
                    {
                        var npc = args[1];
                        return (bool)_gaveGiftForDayField.GetValue(npc);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[BirthdayManager] Error checking gift status for {npcName}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Get item name from item object or ID
        /// </summary>
        private string GetItemName(object item)
        {
            if (item == null) return null;

            // If it's a string, return it directly
            if (item is string str) return str;

            // If it's an int (item ID), use ItemInfoDatabase via GetItemNameFromId
            if (item is int itemId)
            {
                var name = GetItemNameFromId(itemId);
                return !string.IsNullOrEmpty(name) ? name : $"Item #{itemId}";
            }

            // Try to get name property
            var itemNameProp = AccessTools.Property(item.GetType(), "name")
                               ?? AccessTools.Property(item.GetType(), "Name");
            return itemNameProp?.GetValue(item)?.ToString();
        }
    }
}
