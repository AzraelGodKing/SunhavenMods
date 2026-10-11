using System;
using System.Reflection;
using SunhavenMods.Shared;
using UnityEngine;
using Wish;

namespace SunHavenMuseumUtilityTracker.Patches
{
    /// <summary>
    /// Patches for detecting player/character loading.
    /// </summary>
    public static class PlayerPatches
    {
        private static bool _isDataLoaded = false;
        private static string _loadedCharacterName = null;
        private static string _lastCharacterSourceLog = null;
        private static string _lastCharacterNameLog = null;
        private static float _lastSaveAndResetRealtime = -100f;
        private const float SaveAndResetDedupSeconds = 1.5f;

        public static bool IsDataLoaded => _isDataLoaded;

        /// <summary>
        /// Called when player is initialized in-game.
        /// </summary>
        public static void OnPlayerInitialized(Player __instance)
        {
            try
            {
                Plugin.EnsureUIComponentsExist();

                string characterName = GetCurrentCharacterName();

                if (string.IsNullOrEmpty(characterName) || characterName == "default")
                {
                    Plugin.Log?.LogWarning("Could not determine character name on player init");
                    return;
                }

                Plugin.Log?.LogInfo($"Player initialized: {characterName}");

                if (_isDataLoaded && _loadedCharacterName == characterName)
                {
                    Plugin.Log?.LogInfo($"Data already loaded for {characterName}");
                    return;
                }

                if (_isDataLoaded && _loadedCharacterName != characterName)
                {
                    Plugin.Log?.LogInfo($"Switching from {_loadedCharacterName} to {characterName}");
                    Plugin.SaveData();
                    ResetState();
                }

                LoadDataForCharacter(characterName);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in OnPlayerInitialized: {ex.Message}");
            }
        }

        private static void LoadDataForCharacter(string characterName)
        {
            try
            {
                Plugin.Log?.LogInfo($"Loading donation data for: {characterName}");

                Plugin.LoadDataForPlayer(characterName);

                _isDataLoaded = true;
                _loadedCharacterName = characterName;

                SunhavenMods.Shared.IconCache.LoadAllIcons();
                MuseumWishlistOverlayPatches.SubscribeDonationRefresh();

                Plugin.Log?.LogInfo($"Donation data loaded successfully for {characterName}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error loading donation data: {ex.Message}");
            }
        }

        private static string GetCurrentCharacterName()
        {
            try
            {
                // Use the character name from LoadCharacter
                string lastLoadedName = GameSavePatches.LastLoadedCharacterName;
                if (!string.IsNullOrEmpty(lastLoadedName))
                {
                    string sanitizedName = CharacterSaveStore.SanitizeFileName(lastLoadedName, "default");
                    LogCharacterSourceOnce("lastLoaded", sanitizedName);
                    return sanitizedName;
                }

                // Fallback to CurrentCharacter
                var currentChar = GameSave.CurrentCharacter;
                if (currentChar != null && !string.IsNullOrEmpty(currentChar.characterName))
                {
                    string nameFromCurrent = CharacterSaveStore.SanitizeFileName(currentChar.characterName, "default");
                    Plugin.Log?.LogWarning($"GetCurrentCharacterName: FALLBACK to CurrentCharacter = '{nameFromCurrent}'");
                    return nameFromCurrent;
                }

                Plugin.Log?.LogWarning("GetCurrentCharacterName: Could not determine character name");
                return "default";
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error getting character name: {ex.Message}");
                return "default";
            }
        }

        private static void LogCharacterSourceOnce(string source, string characterName)
        {
            if (string.Equals(_lastCharacterSourceLog, source, StringComparison.Ordinal) &&
                string.Equals(_lastCharacterNameLog, characterName, StringComparison.Ordinal))
            {
                return;
            }

            _lastCharacterSourceLog = source;
            _lastCharacterNameLog = characterName;
            Plugin.Log?.LogInfo($"GetCurrentCharacterName: Using {source} character name = '{characterName}'");
        }

        public static void ResetState()
        {
            Plugin.Log?.LogInfo("Resetting data state");
            _isDataLoaded = false;
            _loadedCharacterName = null;
            _lastCharacterSourceLog = null;
            _lastCharacterNameLog = null;
            GameSavePatches.ResetLastLoadedSlot();
        }

        public static void SaveAndReset()
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                if (now - _lastSaveAndResetRealtime < SaveAndResetDedupSeconds)
                {
                    Plugin.Log?.LogDebug($"SaveAndReset deduplicated (last run {now - _lastSaveAndResetRealtime:0.00}s ago)");
                    return;
                }
                _lastSaveAndResetRealtime = now;

                if (_isDataLoaded)
                {
                    Plugin.Log?.LogInfo($"Saving data for {_loadedCharacterName} before menu");
                    Plugin.SaveData();
                }
                ResetState();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in SaveAndReset: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Tracks character loading for proper name extraction.
    /// </summary>
    public static class GameSavePatches
    {
        public static int LastLoadedSlot { get; private set; } = -1;
        public static string LastLoadedCharacterName { get; private set; } = null;

        public static void ResetLastLoadedSlot()
        {
            LastLoadedSlot = -1;
            LastLoadedCharacterName = null;
            Plugin.Log?.LogInfo("GameSavePatches: Reset LastLoadedSlot and LastLoadedCharacterName");
        }

        public static void OnLoadCharacter(int characterNumber)
        {
            try
            {
                Plugin.Log?.LogInfo($"GameSave.LoadCharacter: slot {characterNumber}");
                LastLoadedSlot = characterNumber;

                if (GameSave.Instance?.Saves != null &&
                    characterNumber >= 0 &&
                    characterNumber < GameSave.Instance.Saves.Count)
                {
                    var charData = GameSave.Instance.Saves[characterNumber];
                    if (charData != null)
                    {
                        string charName = GetCharacterNameFromSaveData(charData);
                        if (!string.IsNullOrEmpty(charName))
                        {
                            LastLoadedCharacterName = charName;
                            Plugin.Log?.LogInfo($"GameSavePatches: Extracted character name '{charName}' from slot {characterNumber}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in OnLoadCharacter: {ex.Message}");
            }
        }

        private static string GetCharacterNameFromSaveData(object saveData)
        {
            if (saveData == null) return null;

            var type = saveData.GetType();
            string[] possibleNames = { "characterName", "CharacterName", "name", "Name", "playerName", "PlayerName" };

            // Try properties
            foreach (var propName in possibleNames)
            {
                var prop = type.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.PropertyType == typeof(string))
                {
                    var value = prop.GetValue(saveData) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }

            // Try characterData nested property
            var charDataProp = type.GetProperty("characterData", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (charDataProp != null)
            {
                var charDataObj = charDataProp.GetValue(saveData);
                if (charDataObj != null)
                {
                    var charDataType = charDataObj.GetType();
                    foreach (var propName in possibleNames)
                    {
                        var nameProp = charDataType.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (nameProp != null && nameProp.PropertyType == typeof(string))
                        {
                            var value = nameProp.GetValue(charDataObj) as string;
                            if (!string.IsNullOrEmpty(value))
                                return value;
                        }
                    }
                }
            }

            return null;
        }
    }
}
