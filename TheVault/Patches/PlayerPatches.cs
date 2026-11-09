using System;
using SunhavenMods.Shared;
using Wish;

namespace TheVault.Patches
{
    /// <summary>
    /// Simple vault loading system.
    /// Loads vault when player enters game, resets when returning to menu.
    /// </summary>
    public static class PlayerPatches
    {
        private static bool _isVaultLoaded = false;
        private static string _loadedCharacterName = null;
        private static string _pendingCharacterName = null;
        private static string _lastCharacterSourceLog = null;
        private static string _lastCharacterNameLog = null;
        private static bool _warnedCurrentCharacterFallback;
        private static readonly object _contextLoadLock = new object();
        private static float _lastSaveAndResetRealtime = -100f;
        private const float SaveAndResetDedupSeconds = 1.5f;

        /// <summary>
        /// Returns true if a vault is currently loaded.
        /// </summary>
        public static bool IsVaultLoaded => _isVaultLoaded;

        /// <summary>
        /// Returns the name of the character whose vault is loaded.
        /// </summary>
        public static string LoadedCharacterName => _loadedCharacterName;

        /// <summary>
        /// Called when player is initialized in-game.
        /// This is our single trigger point for vault loading.
        /// </summary>
        public static void OnPlayerInitialized(Player __instance)
        {
            try
            {
                // Ensure UI components exist (recreate if destroyed by game's cleanup)
                Plugin.EnsureUIComponentsExist();

                // Get the current character name
                string characterName = GetCurrentCharacterName();

                if (string.IsNullOrEmpty(characterName) || characterName == "default")
                {
                    Plugin.Log?.LogWarning("Could not determine character name on player init");
                    return;
                }

                Plugin.Log?.LogInfo($"Player initialized: {characterName}");

                lock (_contextLoadLock)
                {
                    // If vault already loaded for this character, skip
                    if (_isVaultLoaded && _loadedCharacterName == characterName)
                    {
                        Plugin.Log?.LogDebug($"Vault already loaded for {characterName}");
                        return;
                    }

                    // If vault loaded for different character, save it first
                    if (_isVaultLoaded && _loadedCharacterName != characterName)
                    {
                        Plugin.Log?.LogInfo($"Switching from {_loadedCharacterName} to {characterName}");
                        Plugin.SaveVault();
                        ResetState();
                    }

                    // Load vault for this character
                    LoadVaultForCharacter(characterName);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in OnPlayerInitialized: {ex.Message}");
            }
        }

        /// <summary>
        /// Load vault data for a character.
        /// </summary>
        private static void LoadVaultForCharacter(string characterName)
        {
            try
            {
                Plugin.Log?.LogInfo($"Loading vault for: {characterName}");

                if (!Plugin.LoadVaultForPlayer(characterName))
                {
                    Plugin.Log?.LogWarning(
                        $"Vault load did not run for '{characterName}' (invalid name or save system missing). Previous vault state is unchanged — not marking as loaded.");
                    return;
                }

                // Update state (Load applied to VaultManager, including new/empty/migrated data)
                _isVaultLoaded = true;
                _loadedCharacterName = characterName;
                ClearPendingCharacterName();

                // Set player name in vault manager
                var vaultManager = Plugin.GetVaultManager();
                if (vaultManager == null)
                {
                    Plugin.Log?.LogWarning("VaultManager not available during character load");
                }
                else
                {
                    vaultManager.SetPlayerName(characterName);
                }

                // Load UI icons (shared cache — VaultUI/VaultHUD use the same instance)
                IconCache.LoadAllIcons();

                Plugin.Log?.LogInfo($"Vault loaded successfully for {characterName}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error loading vault: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the current character name.
        /// Primary source: LastLoadedCharacterName (extracted from slot during LoadCharacter)
        /// Fallback: GameSave.CurrentCharacter (can be stale on character switch)
        /// </summary>
        private static string GetCurrentCharacterName()
        {
            try
            {
                if (!string.IsNullOrEmpty(_pendingCharacterName))
                {
                    string pending = NormalizeCharacterNameForVault(_pendingCharacterName);
                    LogCharacterSourceOnce("pending", pending);
                    return pending;
                }

                // PRIMARY: Use the character name extracted during LoadCharacter
                // This bypasses the stale CurrentCharacter issue
                string lastLoadedName = GameSavePatches.LastLoadedCharacterName;
                if (!string.IsNullOrEmpty(lastLoadedName))
                {
                    string sanitizedName = NormalizeCharacterNameForVault(lastLoadedName);
                    LogCharacterSourceOnce("lastLoaded", sanitizedName);
                    return sanitizedName;
                }

                // FALLBACK: Use CurrentCharacter (may be stale on character switch)
                var currentChar = GameSave.CurrentCharacter;
                if (currentChar != null && !string.IsNullOrEmpty(currentChar.characterName))
                {
                    string nameFromCurrent = NormalizeCharacterNameForVault(currentChar.characterName);
                    if (!_warnedCurrentCharacterFallback)
                    {
                        Plugin.Log?.LogWarning($"GetCurrentCharacterName: FALLBACK to CurrentCharacter = '{nameFromCurrent}' (LastLoadedCharacterName was null)");
                        _warnedCurrentCharacterFallback = true;
                    }
                    else
                    {
                        Plugin.Log?.LogDebug($"GetCurrentCharacterName: FALLBACK to CurrentCharacter = '{nameFromCurrent}'");
                    }
                    return nameFromCurrent;
                }

                Plugin.Log?.LogWarning("GetCurrentCharacterName: Could not determine character name from any source");
                return "default";
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error getting character name: {ex.Message}");
                return "default";
            }
        }

        /// <summary>
        /// Sanitize a string for use as a filename.
        /// </summary>
        internal static string NormalizeCharacterNameForVault(string name) =>
            CharacterSaveStore.SanitizeFileName(name, "default");

        internal static void SetPendingCharacterName(string characterName)
        {
            _pendingCharacterName = NormalizeCharacterNameForVault(characterName);
            Plugin.Log?.LogDebug($"Set pending character name: '{_pendingCharacterName}'");
        }

        private static void ClearPendingCharacterName()
        {
            _pendingCharacterName = null;
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
            Plugin.Log?.LogDebug($"GetCurrentCharacterName: Using {source} character name = '{characterName}'");
        }

        /// <summary>
        /// Reset vault state. Called when returning to menu.
        /// </summary>
        public static void ResetState()
        {
            Plugin.Log?.LogInfo("Resetting vault state");
            _isVaultLoaded = false;
            _loadedCharacterName = null;
            _lastCharacterSourceLog = null;
            _lastCharacterNameLog = null;
            _warnedCurrentCharacterFallback = false;
            ClearPendingCharacterName();
            GameSavePatches.ResetLastLoadedSlot(); // Reset slot tracker so next character gets fresh data
            ItemPatches.ResetState();
            IconCache.Clear();
        }

        /// <summary>
        /// Save and reset. Called when exiting to menu.
        /// </summary>
        public static void SaveAndReset()
        {
            try
            {
                float now = UnityEngine.Time.realtimeSinceStartup;
                if (now - _lastSaveAndResetRealtime < SaveAndResetDedupSeconds)
                {
                    Plugin.Log?.LogDebug($"SaveAndReset deduplicated (last run {now - _lastSaveAndResetRealtime:0.00}s ago)");
                    return;
                }
                _lastSaveAndResetRealtime = now;

                if (_isVaultLoaded)
                {
                    Plugin.Log?.LogInfo($"Saving vault for {_loadedCharacterName} before menu");
                    Plugin.SaveVault();
                }
                ResetState();

                // Reset secret gift check for next character
                SecretGifts.ResetGiftCheck();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in SaveAndReset: {ex.Message}");
            }
        }

        /// <summary>
        /// External/manual vault load (e.g. integrations). Uses the same lock as <see cref="OnPlayerInitialized"/> to avoid races with sync/reload.
        /// </summary>
        public static void TriggerVaultLoad(string characterName)
        {
            try
            {
                string norm = NormalizeCharacterNameForVault(characterName);
                if (string.IsNullOrEmpty(norm) || string.Equals(norm, "default", StringComparison.OrdinalIgnoreCase))
                {
                    Plugin.Log?.LogWarning("TriggerVaultLoad: invalid character name");
                    return;
                }

                lock (_contextLoadLock)
                {
                    if (_isVaultLoaded && string.Equals(_loadedCharacterName, norm, StringComparison.Ordinal))
                    {
                        Plugin.Log?.LogDebug($"TriggerVaultLoad: vault already loaded for {norm}");
                        return;
                    }

                    if (_isVaultLoaded && !string.Equals(_loadedCharacterName, norm, StringComparison.Ordinal))
                    {
                        Plugin.Log?.LogInfo($"TriggerVaultLoad: switching from {_loadedCharacterName} to {norm}");
                        Plugin.SaveVault();
                        ResetState();
                    }

                    LoadVaultForCharacter(norm);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"TriggerVaultLoad: {ex.Message}");
            }
        }

        /// <summary>
        /// Fallback loop called by PersistentRunner to keep active vault aligned with active character.
        /// This covers cases where game updates change hook timings/signatures.
        /// </summary>
        internal static void TrySynchronizeCharacterContext()
        {
            try
            {
                if (Player.Instance == null)
                    return;

                string candidate = GetCurrentCharacterName();
                if (string.IsNullOrEmpty(candidate) || string.Equals(candidate, "default", StringComparison.OrdinalIgnoreCase))
                    return;

                lock (_contextLoadLock)
                {
                    if (_isVaultLoaded && string.Equals(_loadedCharacterName, candidate, StringComparison.Ordinal))
                        return;

                    Plugin.Log?.LogInfo($"[CharacterSync] Aligning vault context to '{candidate}' (currently '{_loadedCharacterName ?? "none"}')");
                    Plugin.EnsureUIComponentsExist();

                    if (_isVaultLoaded && !string.Equals(_loadedCharacterName, candidate, StringComparison.Ordinal))
                    {
                        Plugin.SaveVault();
                        ResetState();
                    }

                    LoadVaultForCharacter(candidate);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[CharacterSync] Error: {ex.Message}");
            }
        }
    }
}
