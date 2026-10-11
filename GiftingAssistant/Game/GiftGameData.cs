using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace GiftingAssistant.Game
{
    /// <summary>
    /// One NPC's gift identity as read from the game: display name plus loved/liked item IDs.
    /// </summary>
    public sealed class GiftNpcInfo
    {
        public string Name { get; }
        public bool Romanceable { get; set; }
        public List<int> LovedItemIds { get; } = new List<int>();
        public List<int> LikedItemIds { get; } = new List<int>();

        public GiftNpcInfo(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Reflection bridge to Sun Haven's NPC/gift data. Mirrors BirthdayReminder's BirthdayManager:
    /// NPCManager._npcs -> NPCAI, NPCAI.giftTable -> NPCGiftTable.love2/like2 (SerializedItemData.id),
    /// and NPCAI.gaveGiftForDay for "gifted today".
    /// </summary>
    public static class GiftGameData
    {
        private static bool _reflectionInitialized;
        private static Type _npcManagerType;
        private static PropertyInfo _npcManagerInstanceProp;
        private static FieldInfo _npcsDictField;
        private static FieldInfo _love2Field;
        private static FieldInfo _like2Field;
        private static FieldInfo _giftTableField;
        private static FieldInfo _gaveGiftForDayField;
        private static PropertyInfo _romanceableProp;
        private static bool _useGameData;

        private static MethodInfo _tryGetValueMethod;
        private static Type _tryGetValueDictType;
        private static PropertyInfo[] _npcNameProps;

        private static List<GiftNpcInfo> _cachedNpcs;
        private static Dictionary<string, GiftNpcInfo> _npcByNormalizedName;

        private static Dictionary<string, bool> _giftedTodayCache;
        private static bool _giftedTodayCacheValid;

        private static Dictionary<string, float> _relationshipCache;
        private static bool _relationshipCacheValid;

        private static PropertyInfo _gameSaveCurrentCharacterProp;
        private static PropertyInfo _gameSaveInstanceProp;
        private static PropertyInfo _gameSaveCurrentSaveProp;
        private static PropertyInfo _saveCharacterDataProp;
        private static PropertyInfo _characterDataRelationshipsProp;
        private static bool _relationshipApiInitialized;

        private static bool _npcCacheBuildScheduled;

        /// <summary>True after a successful full NPC/gift-table scan.</summary>
        public static bool IsNpcCacheReady => _cachedNpcs != null && _cachedNpcs.Count > 0;

        /// <summary>Schedule a deferred NPC cache build (next ProcessDeferredWork tick).</summary>
        public static void ScheduleNpcCacheWarm()
        {
            _npcCacheBuildScheduled = true;
        }

        /// <summary>Build NPC cache if scheduled or not yet ready. Call off the IMGUI click path.</summary>
        public static void ProcessDeferredNpcCache()
        {
            if (!_npcCacheBuildScheduled && IsNpcCacheReady)
                return;
            _npcCacheBuildScheduled = false;
            BuildNpcCache();
        }

        /// <summary>Synchronous build when cache is required immediately (deferred worker only).</summary>
        public static void EnsureNpcCacheReady()
        {
            if (!IsNpcCacheReady)
                BuildNpcCache();
        }

        private static void InitializeReflection()
        {
            if (_reflectionInitialized)
                return;
            _reflectionInitialized = true;

            try
            {
                _npcManagerType = AccessTools.TypeByName("Wish.NPCManager");
                if (_npcManagerType == null)
                {
                    Plugin.Log?.LogWarning("[GiftGameData] Wish.NPCManager not found - NPC data unavailable");
                    return;
                }

                var singletonType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                if (singletonType != null)
                {
                    var genericType = singletonType.MakeGenericType(_npcManagerType);
                    _npcManagerInstanceProp = genericType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                }

                _npcsDictField = AccessTools.Field(_npcManagerType, "_npcs")
                                 ?? AccessTools.Field(_npcManagerType, "npcs");

                var npcGiftTableType = AccessTools.TypeByName("Wish.NPCGiftTable");
                if (npcGiftTableType != null)
                {
                    _love2Field = AccessTools.Field(npcGiftTableType, "love2");
                    _like2Field = AccessTools.Field(npcGiftTableType, "like2");
                }

                var npcaiType = AccessTools.TypeByName("Wish.NPCAI");
                if (npcaiType != null)
                {
                    _giftTableField = AccessTools.Field(npcaiType, "giftTable");
                    _gaveGiftForDayField = AccessTools.Field(npcaiType, "gaveGiftForDay");
                    _romanceableProp = AccessTools.Property(npcaiType, "Romanceable")
                                       ?? AccessTools.Property(npcaiType, "romanceable");
                    _npcNameProps = new[]
                    {
                        npcaiType.GetProperty("OriginalName", BindingFlags.Public | BindingFlags.Instance),
                        npcaiType.GetProperty("NPCName", BindingFlags.Public | BindingFlags.Instance),
                        npcaiType.GetProperty("ActualNPCName", BindingFlags.Public | BindingFlags.Instance),
                        npcaiType.GetProperty("npcName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                        npcaiType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance)
                    };
                }

                InitializeRelationshipReflection();

                _useGameData = _npcManagerInstanceProp != null &&
                               _npcsDictField != null &&
                               _giftTableField != null;

                if (_useGameData)
                    Plugin.Log?.LogInfo("[GiftGameData] Game NPC/gift data API initialized");
                else
                    Plugin.Log?.LogWarning("[GiftGameData] Game NPC/gift data API unavailable - check game version");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[GiftGameData] Error initializing reflection: {ex.Message}");
                _useGameData = false;
            }
        }

        /// <summary>Drops the cached NPC list so the next call re-reads from the game (e.g. on character load).</summary>
        /// <remarks>Reflection handles are initialized once per process and intentionally retained here.</remarks>
        public static void InvalidateCache()
        {
            _cachedNpcs = null;
            _npcByNormalizedName = null;
            InvalidateGiftedTodayCache();
            InvalidateRelationshipCache();
        }

        private static void InitializeRelationshipReflection()
        {
            if (_relationshipApiInitialized)
                return;
            _relationshipApiInitialized = true;

            try
            {
                var gameSaveType = AccessTools.TypeByName("Wish.GameSave");
                if (gameSaveType == null)
                    return;

                _gameSaveCurrentCharacterProp = AccessTools.Property(gameSaveType, "CurrentCharacter");

                var singletonType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                if (singletonType != null)
                {
                    var genericType = singletonType.MakeGenericType(gameSaveType);
                    _gameSaveInstanceProp = genericType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                }

                _gameSaveCurrentSaveProp = AccessTools.Property(gameSaveType, "CurrentSave");

                var characterDataType = AccessTools.TypeByName("Wish.CharacterData");
                if (characterDataType != null)
                    _characterDataRelationshipsProp = AccessTools.Property(characterDataType, "Relationships");

                var saveDataType = _gameSaveCurrentSaveProp?.PropertyType;
                if (saveDataType != null)
                    _saveCharacterDataProp = AccessTools.Property(saveDataType, "characterData");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[GiftGameData] Relationship reflection init: {ex.Message}");
            }
        }

        /// <summary>Marks relationship cache stale (e.g. character load).</summary>
        public static void InvalidateRelationshipCache()
        {
            _relationshipCacheValid = false;
        }

        /// <summary>
        /// Rebuilds relationship points from GameSave character data. Call on window open — not every OnGUI frame.
        /// Keys are normalized NPC names (same as roster entries); values come from CharacterData.Relationships.
        /// </summary>
        public static void RefreshRelationshipCache()
        {
            InitializeReflection();
            InitializeRelationshipReflection();

            if (_relationshipCache == null)
                _relationshipCache = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            else
                _relationshipCache.Clear();

            var relationships = ReadRelationshipsDictionary();
            if (relationships == null)
            {
                _relationshipCacheValid = true;
                return;
            }

            try
            {
                foreach (var entry in relationships)
                {
                    string key = NormalizeNpcName(entry.Key);
                    if (string.IsNullOrEmpty(key))
                        continue;
                    _relationshipCache[key] = entry.Value;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[GiftGameData] RefreshRelationshipCache: {ex.Message}");
            }

            _relationshipCacheValid = true;
        }

        /// <summary>
        /// Returns cached relationship points for an NPC, or null when unknown / not in save data.
        /// </summary>
        public static float? GetRelationshipPoints(string npcName)
        {
            if (string.IsNullOrEmpty(npcName))
                return null;

            string normalized = NormalizeNpcName(npcName);
            if (!_relationshipCacheValid || _relationshipCache == null ||
                !_relationshipCache.TryGetValue(normalized, out float points))
                return null;

            return points;
        }

        /// <summary>Marks the gifted-today cache stale (e.g. new in-game day).</summary>
        public static void InvalidateGiftedTodayCache()
        {
            _giftedTodayCacheValid = false;
        }

        /// <summary>
        /// Rebuilds gifted-today flags in one pass over NPCManager._npcs. Call on window open,
        /// day change, and after a gift — not every OnGUI frame.
        /// </summary>
        public static void RefreshGiftedTodayCache()
        {
            InitializeReflection();
            if (_giftedTodayCache == null)
                _giftedTodayCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            else
                _giftedTodayCache.Clear();

            if (!_useGameData || _gaveGiftForDayField == null)
            {
                _giftedTodayCacheValid = true;
                return;
            }

            try
            {
                var npcManager = _npcManagerInstanceProp?.GetValue(null);
                var npcsDict = npcManager != null ? _npcsDictField?.GetValue(npcManager) : null;
                if (npcsDict == null)
                {
                    _giftedTodayCacheValid = true;
                    return;
                }

                var valuesProp = npcsDict.GetType().GetProperty("Values");
                var values = valuesProp?.GetValue(npcsDict) as IEnumerable;
                if (values == null)
                {
                    _giftedTodayCacheValid = true;
                    return;
                }

                foreach (var npc in values)
                {
                    if (npc == null)
                        continue;

                    try
                    {
                        string rawName = ReadNpcName(npc);
                        if (string.IsNullOrEmpty(rawName))
                            continue;

                        string displayName = NormalizeNpcName(rawName);
                        if (string.IsNullOrEmpty(displayName))
                            continue;

                        bool gifted = (bool)_gaveGiftForDayField.GetValue(npc);
                        _giftedTodayCache[displayName] = gifted;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogDebug($"[GiftGameData] RefreshGiftedTodayCache NPC: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[GiftGameData] RefreshGiftedTodayCache: {ex.Message}");
            }

            _giftedTodayCacheValid = true;
        }

        /// <summary>Updates one NPC's gifted-today flag after an in-game gift (avoids full refresh).</summary>
        public static void SetGiftedToday(string npcName, bool gifted)
        {
            if (string.IsNullOrEmpty(npcName))
                return;

            string normalized = NormalizeNpcName(npcName);
            if (string.IsNullOrEmpty(normalized))
                return;

            if (_giftedTodayCache == null)
                _giftedTodayCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            _giftedTodayCache[normalized] = gifted;
            _giftedTodayCacheValid = true;
        }

        /// <summary>
        /// Returns all NPCs with their gift tables. Cached after first successful read.
        /// Does not trigger a scan when cache is empty — use EnsureNpcCacheReady from deferred work.
        /// </summary>
        public static IReadOnlyList<GiftNpcInfo> GetAllNpcs()
        {
            if (_cachedNpcs != null)
                return _cachedNpcs;
            return Array.Empty<GiftNpcInfo>();
        }

        /// <summary>
        /// Returns the cached gift table for one NPC (matched by normalized name), or null.
        /// </summary>
        public static GiftNpcInfo GetNpcInfo(string npcName)
        {
            if (string.IsNullOrEmpty(npcName))
                return null;

            string normalized = NormalizeNpcName(npcName);
            if (_npcByNormalizedName != null &&
                _npcByNormalizedName.TryGetValue(normalized, out var info))
                return info;

            return null;
        }

        public static bool IsRomanceable(string npcName)
        {
            return GetNpcInfo(npcName)?.Romanceable == true;
        }

        private static void BuildNpcCache()
        {
            InitializeReflection();
            var result = new List<GiftNpcInfo>();
            var lookup = new Dictionary<string, GiftNpcInfo>(StringComparer.OrdinalIgnoreCase);

            if (!_useGameData)
            {
                _cachedNpcs = result;
                _npcByNormalizedName = lookup;
                return;
            }

            try
            {
                var npcManager = _npcManagerInstanceProp?.GetValue(null);
                if (npcManager == null)
                {
                    _cachedNpcs = result;
                    _npcByNormalizedName = lookup;
                    return;
                }

                var npcsDict = _npcsDictField?.GetValue(npcManager);
                if (npcsDict == null)
                {
                    _cachedNpcs = result;
                    _npcByNormalizedName = lookup;
                    return;
                }

                var valuesProp = npcsDict.GetType().GetProperty("Values");
                var values = valuesProp?.GetValue(npcsDict) as IEnumerable;
                if (values == null)
                {
                    _cachedNpcs = result;
                    _npcByNormalizedName = lookup;
                    return;
                }

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var npc in values)
                {
                    if (npc == null)
                        continue;

                    try
                    {
                        string rawName = ReadNpcName(npc);
                        if (string.IsNullOrEmpty(rawName))
                            continue;

                        string displayName = NormalizeNpcName(rawName);
                        if (string.IsNullOrEmpty(displayName) || !seen.Add(displayName))
                            continue;

                        var giftTable = _giftTableField?.GetValue(npc);
                        if (giftTable == null)
                            continue;

                        var info = new GiftNpcInfo(displayName);
                        if (_romanceableProp?.GetValue(npc) is bool romanceable)
                            info.Romanceable = romanceable;
                        AddItemIds(_love2Field?.GetValue(giftTable) as IList, info.LovedItemIds);
                        AddItemIds(_like2Field?.GetValue(giftTable) as IList, info.LikedItemIds);
                        result.Add(info);
                        lookup[displayName] = info;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogDebug($"[GiftGameData] Error reading NPC: {ex.Message}");
                    }
                }

                result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[GiftGameData] Error reading NPC list: {ex.Message}");
            }

            _cachedNpcs = result;
            _npcByNormalizedName = lookup;
        }

        /// <summary>
        /// True if the named NPC has already received a gift today (game's NPCAI.gaveGiftForDay).
        /// </summary>
        public static bool HasGivenGiftToday(string npcName)
        {
            if (string.IsNullOrEmpty(npcName))
                return false;

            string normalized = NormalizeNpcName(npcName);
            if (!_giftedTodayCacheValid)
                RefreshGiftedTodayCache();

            if (_giftedTodayCache != null &&
                _giftedTodayCache.TryGetValue(normalized, out bool cached))
                return cached;

            return false;
        }

        /// <summary>
        /// Collapses duplicate composite names from the game (e.g. "Darius+Darius" -> "Darius").
        /// </summary>
        public static string NormalizeNpcName(string npcName)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return "";

            var parts = npcName
                .Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (parts.Count == 0)
                return npcName.Trim();
            if (parts.Count == 1)
                return parts[0];

            return string.Join("+", parts);
        }

        private static string ReadNpcName(object npc)
        {
            if (npc == null)
                return null;

            if (_npcNameProps != null)
            {
                foreach (var prop in _npcNameProps)
                {
                    if (prop == null)
                        continue;
                    var value = prop.GetValue(npc);
                    if (value != null)
                        return value.ToString();
                }
            }

            var npcType = npc.GetType();
            var nameProp = AccessTools.Property(npcType, "NPCName")
                           ?? AccessTools.Property(npcType, "ActualNPCName")
                           ?? AccessTools.Property(npcType, "OriginalName")
                           ?? AccessTools.Property(npcType, "npcName")
                           ?? AccessTools.Property(npcType, "Name");
            return nameProp?.GetValue(npc)?.ToString();
        }

        private static MethodInfo GetTryGetValueMethod(object npcsDict)
        {
            if (npcsDict == null)
                return null;

            var dictType = npcsDict.GetType();
            if (_tryGetValueMethod != null && _tryGetValueDictType == dictType)
                return _tryGetValueMethod;

            _tryGetValueDictType = dictType;
            _tryGetValueMethod = dictType.GetMethod("TryGetValue");
            return _tryGetValueMethod;
        }

        /// <summary>
        /// Resolves an NPC instance from NPCManager._npcs by raw or normalized name.
        /// Dictionary keys may use composite names while roster entries use single names.
        /// </summary>
        private static object FindNpc(object npcsDict, string name)
        {
            if (npcsDict == null || string.IsNullOrEmpty(name))
                return null;

            string normalized = NormalizeNpcName(name);
            var tryGetValue = GetTryGetValueMethod(npcsDict);
            if (tryGetValue != null)
            {
                foreach (var candidate in new[] { name, normalized })
                {
                    if (string.IsNullOrEmpty(candidate))
                        continue;

                    var args = new object[] { candidate, null };
                    if ((bool)tryGetValue.Invoke(npcsDict, args) && args[1] != null)
                        return args[1];
                }
            }

            if (npcsDict is IDictionary dict)
            {
                foreach (DictionaryEntry entry in dict)
                {
                    string key = entry.Key?.ToString();
                    if (!string.IsNullOrEmpty(key) &&
                        string.Equals(NormalizeNpcName(key), normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }

                    string valueName = ReadNpcName(entry.Value);
                    if (!string.IsNullOrEmpty(valueName) &&
                        string.Equals(NormalizeNpcName(valueName), normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.Value;
                    }
                }
            }

            return null;
        }

        private static void AddItemIds(IList source, List<int> destination)
        {
            if (source == null)
                return;

            foreach (var element in source)
            {
                int id = ExtractItemId(element);
                if (id > 0 && !destination.Contains(id))
                    destination.Add(id);
            }
        }

        /// <summary>
        /// love2/like2 entries are SerializedItemData with an int field 'id' (not raw ints).
        /// </summary>
        private static int ExtractItemId(object element)
        {
            try
            {
                if (element == null)
                    return -1;
                if (element is int direct)
                    return direct;

                var type = element.GetType();
                var idField = type.GetField("id", BindingFlags.Public | BindingFlags.Instance);
                if (idField != null)
                {
                    var value = idField.GetValue(element);
                    return value is int i ? i : Convert.ToInt32(value);
                }

                var idProp = type.GetProperty("id") ?? type.GetProperty("ID");
                if (idProp != null)
                {
                    var value = idProp.GetValue(element);
                    return value is int i ? i : Convert.ToInt32(value);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[GiftGameData] ExtractItemId: {ex.Message}");
            }

            return -1;
        }

        private static Dictionary<string, float> ReadRelationshipsDictionary()
        {
            if (_characterDataRelationshipsProp == null)
                return null;

            try
            {
                object characterData = null;

                var currentCharacter = _gameSaveCurrentCharacterProp?.GetValue(null);
                if (currentCharacter != null)
                    characterData = currentCharacter;
                else
                {
                    var gameSave = _gameSaveInstanceProp?.GetValue(null);
                    var currentSave = gameSave != null ? _gameSaveCurrentSaveProp?.GetValue(gameSave) : null;
                    characterData = currentSave != null ? _saveCharacterDataProp?.GetValue(currentSave) : null;
                }

                if (characterData == null)
                    return null;

                var relationships = _characterDataRelationshipsProp.GetValue(characterData);
                if (relationships == null)
                    return null;

                if (relationships is Dictionary<string, float> typed)
                    return typed;

                if (relationships is IDictionary dict)
                {
                    var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                    foreach (DictionaryEntry entry in dict)
                    {
                        string key = entry.Key?.ToString();
                        if (string.IsNullOrEmpty(key) || entry.Value == null)
                            continue;
                        result[key] = entry.Value is float f ? f : Convert.ToSingle(entry.Value);
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[GiftGameData] ReadRelationshipsDictionary: {ex.Message}");
            }

            return null;
        }
    }
}
