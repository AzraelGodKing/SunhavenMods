using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SunhavenMods.Shared;
using UnityEngine;

namespace TheVault.Vault
{
    /// <summary>
    /// Handles saving and loading vault data to/from encrypted files.
    /// Saves are stored per-player in the BepInEx config folder.
    /// Local storage is tamper-resistant (not confidentiality against a motivated local attacker); see mod README.
    /// File names include Steam ID or a per-machine/player suffix to avoid same-name cross-character collisions.
    /// Supports Steam ID for cross-device portability, with fallback for non-Steam versions.
    /// </summary>
    public class VaultSaveSystem
    {
        private readonly string _saveDirectory;
        private readonly VaultManager _vaultManager;
        private string _currentSaveFile;

        // Auto-save interval in seconds
        private float _autoSaveIntervalSeconds = 300f;
        private float _lastAutoSave;

        private bool _needsReEncryption;
        private bool _loggedSaveBlockedOnce;

        /// <summary>
        /// Set when a save file could not be read and was quarantined; a new empty in-memory vault was started.
        /// </summary>
        public bool LastLoadQuarantinedCorruptFile { get; private set; }

        /// <summary>
        /// True when Load fell through to an empty in-memory vault after every on-disk candidate failed.
        /// Saves are blocked until <see cref="ConfirmStartFreshVault"/> so the spared <c>.backup</c> is not overwritten (AZR-238).
        /// </summary>
        public bool LoadFailedNoRecoverableData { get; private set; }

        public VaultSaveSystem(VaultManager vaultManager)
        {
            _vaultManager = vaultManager;
            _saveDirectory = Path.Combine(BepInEx.Paths.ConfigPath, "TheVault", "Saves");
            _lastAutoSave = Time.time;
            VaultCryptography.LogSource = Plugin.Log;

            // Ensure save directory exists
            if (!Directory.Exists(_saveDirectory))
            {
                Directory.CreateDirectory(_saveDirectory);
                Plugin.Log?.LogInfo($"Created save directory: {_saveDirectory}");
            }

            // Migrate saves from old CurrencySpell folder if they exist
            MigrateOldSaves();
        }

        /// <summary>
        /// Migrate saves from the old CurrencySpell folder to TheVault folder
        /// </summary>
        private void MigrateOldSaves()
        {
            try
            {
                string oldSaveDir = Path.Combine(BepInEx.Paths.ConfigPath, "CurrencySpell", "Saves");
                if (!Directory.Exists(oldSaveDir)) return;

                var oldFiles = Directory.GetFiles(oldSaveDir, "*.vault");
                if (oldFiles.Length == 0) return;

                Plugin.Log?.LogInfo($"Found {oldFiles.Length} save files in old CurrencySpell folder, migrating...");

                foreach (var oldFile in oldFiles)
                {
                    string fileName = Path.GetFileName(oldFile);
                    string newFile = Path.Combine(_saveDirectory, fileName);

                    // Only copy if destination doesn't exist
                    if (!File.Exists(newFile))
                    {
                        File.Copy(oldFile, newFile);
                        Plugin.Log?.LogInfo($"Migrated save: {fileName}");
                    }
                }

                Plugin.Log?.LogInfo("Save migration complete. You can delete the old CurrencySpell folder if desired.");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"Failed to migrate old saves: {ex.Message}");
            }
        }

        /// <summary>
        /// Current canonical save path (includes Steam or local identity suffix).
        /// </summary>
        private string GetSaveFilePath(string playerName)
        {
            string safeName = CharacterSaveStore.SanitizeFileName(playerName, "default");
            string suffix = GetPerPlayerFileSuffix(playerName);
            return Path.Combine(_saveDirectory, $"{safeName}_{suffix}.vault");
        }

        /// <summary>
        /// Legacy path: character name only (pre–Steam-suffix migration). Migrated once on successful load.
        /// </summary>
        private string GetLegacySaveFilePath(string playerName)
        {
            string safeName = CharacterSaveStore.SanitizeFileName(playerName, "default");
            return Path.Combine(_saveDirectory, $"{safeName}.vault");
        }

        /// <summary>
        /// Steam ID when available; otherwise a stable hash of device id + player name (reduces same-name collisions on one PC).
        /// </summary>
        private string GetPerPlayerFileSuffix(string playerName)
        {
            string steamId = VaultCryptography.TryGetSteamId();
            if (!string.IsNullOrEmpty(steamId))
                return "steam_" + CharacterSaveStore.SanitizeFileName(steamId, string.Empty);

            try
            {
                using (var sha = SHA256.Create())
                {
                    string seed = SystemInfo.deviceUniqueIdentifier + "\0" + (playerName ?? "");
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
                    var sb = new StringBuilder(16);
                    for (int i = 0; i < 8; i++)
                        sb.Append(hash[i].ToString("x2"));
                    return "local_" + sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[VaultSave] local suffix fallback: {ex.Message}");
                return "local_unknown";
            }
        }

        private static bool TryQuarantineUnreadableFile(string filePath, string reason)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;
            try
            {
                string dir = Path.GetDirectoryName(filePath) ?? "";
                string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                string dest = Path.Combine(dir, Path.GetFileNameWithoutExtension(filePath) + ".corrupt-" + stamp + ".bak");
                int n = 0;
                while (File.Exists(dest))
                {
                    n++;
                    dest = Path.Combine(dir, Path.GetFileNameWithoutExtension(filePath) + ".corrupt-" + stamp + "-" + n + ".bak");
                }
                File.Move(filePath, dest);
                Plugin.Log?.LogError($"[VaultSave] Quarantined unreadable vault to: {dest} ({reason})");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[VaultSave] Failed to quarantine corrupt vault file: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Decrypt and deserialize vault data from a file path. Returns null when the file cannot be read.
        /// Uses shared <see cref="VaultCryptography"/> so the CSVAULT2 header is skipped and legacy keys work (AZR-240).
        /// </summary>
        private VaultData TryParseVaultFile(string pathToRead, string playerName)
        {
            byte[] encryptedData = File.ReadAllBytes(pathToRead);

            string json = VaultCryptography.Decrypt(encryptedData, playerName);
            if (string.IsNullOrEmpty(json))
                return null;
            if (VaultCryptography.LastDecryptNeedsUpgrade)
                _needsReEncryption = true;

            var wrapper = JsonUtility.FromJson<VaultDataWrapper>(json);
            if (wrapper == null)
                return null;

            return MigrateData(wrapper.ToVaultData());
        }

        /// <summary>
        /// Load vault data for a player
        /// </summary>
        public bool Load(string playerName)
        {
            LastLoadQuarantinedCorruptFile = false;
            LoadFailedNoRecoverableData = false;
            _loggedSaveBlockedOnce = false;

            if (!IsValidPlayerName(playerName))
            {
                Plugin.Log?.LogWarning("[VaultSave] Load skipped: player name was null/empty/invalid.");
                return false;
            }

            try
            {
                string canonicalPath = GetSaveFilePath(playerName);
                string legacyPath = GetLegacySaveFilePath(playerName);
                string backupPath = canonicalPath + CharacterSaveStore.VaultBackupSuffix;
                _currentSaveFile = canonicalPath;

                var candidatePaths = new System.Collections.Generic.List<(string path, bool readFromLegacyNameOnly)>();
                if (File.Exists(canonicalPath))
                    candidatePaths.Add((canonicalPath, false));
                if (File.Exists(backupPath))
                    candidatePaths.Add((backupPath, false));
                if (File.Exists(legacyPath))
                    candidatePaths.Add((legacyPath, true));

                if (candidatePaths.Count == 0)
                {
                    Plugin.Log?.LogInfo($"No existing save file for player '{playerName}', creating new vault");
                    _vaultManager.LoadVaultData(new VaultData { PlayerName = playerName });
                    return true;
                }

                VaultData data = null;
                bool readFromLegacyNameOnly = false;
                string loadedPath = null;

                foreach (var candidate in candidatePaths)
                {
                    _needsReEncryption = false;
                    try
                    {
                        data = TryParseVaultFile(candidate.path, playerName);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning($"[VaultSave] Failed to read {candidate.path}: {ex.Message}");
                        data = null;
                    }

                    if (data != null)
                    {
                        loadedPath = candidate.path;
                        readFromLegacyNameOnly = candidate.readFromLegacyNameOnly;
                        break;
                    }
                }

                if (data != null && loadedPath != canonicalPath)
                {
                    foreach (var candidate in candidatePaths)
                    {
                        if (candidate.path == loadedPath)
                            break;
                        if (candidate.path == backupPath)
                            continue;
                        if (TryQuarantineUnreadableFile(candidate.path, "loaded from later candidate: " + loadedPath))
                            LastLoadQuarantinedCorruptFile = true;
                    }
                }

                if (data == null)
                {
                    foreach (var candidate in candidatePaths)
                    {
                        if (candidate.path == backupPath)
                            continue;

                        if (TryQuarantineUnreadableFile(candidate.path, "all decryption paths failed"))
                            LastLoadQuarantinedCorruptFile = true;
                    }

                    Plugin.Log?.LogError(
                        $"[VaultSave] Vault file for '{playerName}' could not be loaded from primary, legacy, or backup paths. Starting an empty vault in memory; saving is blocked until you confirm Start Fresh so the .backup is not overwritten.");
                    LoadFailedNoRecoverableData = true;
                    _vaultManager.LoadVaultData(new VaultData { PlayerName = playerName });
                    return true;
                }

                if (loadedPath == backupPath)
                    Plugin.Log?.LogInfo($"[VaultSave] Loaded vault from backup for player '{playerName}'");

                _vaultManager.LoadVaultData(data);
                Plugin.Log?.LogInfo($"Loaded vault data for player '{playerName}'");

                if (_needsReEncryption)
                {
                    Plugin.Log?.LogInfo("Re-encrypting vault with new method...");
                    Save();
                    _needsReEncryption = false;
                    Plugin.Log?.LogInfo("Vault successfully migrated to new encryption!");
                }

                if (readFromLegacyNameOnly && File.Exists(legacyPath))
                {
                    try
                    {
                        Plugin.Log?.LogInfo("[VaultSave] Migrating legacy save filename (Steam/local suffix)...");
                        Save();
                        if (File.Exists(canonicalPath))
                        {
                            File.Delete(legacyPath);
                            Plugin.Log?.LogInfo($"[VaultSave] Migrated to new path and removed legacy file: {legacyPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning($"[VaultSave] Legacy filename migration: {ex.Message}");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Failed to load vault data: {ex.Message}");
                string canonicalPath = GetSaveFilePath(playerName);
                string legacyPath = GetLegacySaveFilePath(playerName);
                _currentSaveFile = canonicalPath;
                string pathToQuarantine = File.Exists(canonicalPath) ? canonicalPath : legacyPath;
                if (File.Exists(pathToQuarantine))
                {
                    TryQuarantineUnreadableFile(pathToQuarantine, "exception during load: " + ex.Message);
                    LastLoadQuarantinedCorruptFile = true;
                }
                LoadFailedNoRecoverableData = true;
                _vaultManager.LoadVaultData(new VaultData { PlayerName = playerName });
                // Empty vault is now in memory; callers should treat load as applied for this character.
                return true;
            }
        }

        /// <summary>
        /// Clears the failed-load save latch so the empty in-memory vault may be persisted.
        /// Call only after the player explicitly chooses to abandon the on-disk backup.
        /// </summary>
        public void ConfirmStartFreshVault()
        {
            if (!LoadFailedNoRecoverableData)
                return;

            LoadFailedNoRecoverableData = false;
            _loggedSaveBlockedOnce = false;
            Plugin.Log?.LogWarning(
                "[VaultSave] Player confirmed Start Fresh — empty vault may now be saved; on-disk .backup will be rotated on the next successful write.");
        }

        /// <summary>
        /// Save current vault data (encrypted)
        /// </summary>
        public bool Save()
        {
            if (string.IsNullOrEmpty(_currentSaveFile))
            {
                Plugin.Log?.LogWarning("No save file set, cannot save");
                return false;
            }

            if (!VaultFailedLoadSavePolicy.AllowSave(LoadFailedNoRecoverableData))
            {
                if (!_loggedSaveBlockedOnce)
                {
                    _loggedSaveBlockedOnce = true;
                    Plugin.Log?.LogError(
                        "[VaultSave] Save blocked: vault load recovered no data and a .backup may still hold the last good copy. " +
                        "Open the Vault UI and choose Start Fresh only if you intend to abandon that backup (AZR-238).");
                }
                return false;
            }

            try
            {
                var data = _vaultManager.GetVaultData();
                if (!IsValidPlayerName(data?.PlayerName))
                {
                    Plugin.Log?.LogWarning("[VaultSave] Save skipped: current vault player name was null/empty/invalid.");
                    return false;
                }
                var wrapper = VaultDataWrapper.FromVaultData(data);
                string json = JsonUtility.ToJson(wrapper, true);

                byte[] encryptedData = VaultCryptography.Encrypt(json, data.PlayerName);

                if (!CharacterSaveStore.WriteAtomicBytes(
                        _currentSaveFile,
                        encryptedData,
                        CharacterSaveStore.VaultBackupSuffix,
                        deleteTempInFinally: false))
                {
                    throw new IOException("Atomic vault write failed");
                }

                _vaultManager.MarkClean();
                _lastAutoSave = Time.time;
                Plugin.Log?.LogInfo($"Saved vault data to {_currentSaveFile}");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Failed to save vault data: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Force an immediate save
        /// </summary>
        public void ForceSave()
        {
            if (_vaultManager.IsDirty)
            {
                Save();
            }
        }

        /// <summary>
        /// Check if auto-save should run and perform it if needed
        /// </summary>
        public void CheckAutoSave()
        {
            if (!_vaultManager.IsDirty)
                return;

            if (Time.time - _lastAutoSave >= _autoSaveIntervalSeconds)
            {
                Plugin.Log?.LogInfo("Auto-saving vault data...");
                Save();
            }
        }

        /// <summary>
        /// Runtime-configurable autosave interval. Values below 10 seconds are clamped.
        /// </summary>
        public void SetAutoSaveIntervalSeconds(float seconds)
        {
            _autoSaveIntervalSeconds = Mathf.Max(10f, seconds);
        }

        /// <summary>
        /// Handle data migration between versions
        /// </summary>
        private VaultData MigrateData(VaultData data)
        {
            return VaultDataMigration.Migrate(data, message => Plugin.Log?.LogInfo(message));
        }

        #region Encryption

        private static bool IsValidPlayerName(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
                return false;
            return !string.Equals(playerName.Trim(), "null", StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }

    /// <summary>
    /// Wrapper class for Unity's JsonUtility serialization.
    /// JsonUtility doesn't support Dictionary, so we use arrays of key-value pairs.
    /// </summary>
    [Serializable]
    public class VaultDataWrapper
    {
        public int Version;
        public string PlayerName;
        public string LastSaved;

        // Seasonal tokens as parallel arrays
        public int[] SeasonalTokenTypes;
        public int[] SeasonalTokenValues;

        // Community tokens
        public string[] CommunityTokenKeys;
        public int[] CommunityTokenValues;

        // Keys
        public string[] KeyIds;
        public int[] KeyValues;

        // Tickets
        public string[] TicketIds;
        public int[] TicketValues;

        // Orbs
        public string[] OrbIds;
        public int[] OrbValues;

        // Custom currencies
        public string[] CustomCurrencyIds;
        public int[] CustomCurrencyValues;

        public static VaultDataWrapper FromVaultData(VaultData data)
        {
            var wrapper = new VaultDataWrapper
            {
                Version = data.Version,
                PlayerName = data.PlayerName ?? "",
                LastSaved = data.LastSaved.ToString("o")
            };

            // Seasonal tokens
            var seasonalTypes = new System.Collections.Generic.List<int>();
            var seasonalValues = new System.Collections.Generic.List<int>();
            foreach (var kvp in data.SeasonalTokens)
            {
                seasonalTypes.Add((int)kvp.Key);
                seasonalValues.Add(kvp.Value);
            }
            wrapper.SeasonalTokenTypes = seasonalTypes.ToArray();
            wrapper.SeasonalTokenValues = seasonalValues.ToArray();

            // Community tokens
            var commKeys = new System.Collections.Generic.List<string>();
            var commValues = new System.Collections.Generic.List<int>();
            foreach (var kvp in data.CommunityTokens)
            {
                commKeys.Add(kvp.Key);
                commValues.Add(kvp.Value);
            }
            wrapper.CommunityTokenKeys = commKeys.ToArray();
            wrapper.CommunityTokenValues = commValues.ToArray();

            // Keys
            var keyIds = new System.Collections.Generic.List<string>();
            var keyVals = new System.Collections.Generic.List<int>();
            foreach (var kvp in data.Keys)
            {
                keyIds.Add(kvp.Key);
                keyVals.Add(kvp.Value);
            }
            wrapper.KeyIds = keyIds.ToArray();
            wrapper.KeyValues = keyVals.ToArray();

            // Tickets
            var ticketIds = new System.Collections.Generic.List<string>();
            var ticketVals = new System.Collections.Generic.List<int>();
            foreach (var kvp in data.Tickets)
            {
                ticketIds.Add(kvp.Key);
                ticketVals.Add(kvp.Value);
            }
            wrapper.TicketIds = ticketIds.ToArray();
            wrapper.TicketValues = ticketVals.ToArray();

            // Orbs
            var orbIds = new System.Collections.Generic.List<string>();
            var orbVals = new System.Collections.Generic.List<int>();
            foreach (var kvp in data.Orbs)
            {
                orbIds.Add(kvp.Key);
                orbVals.Add(kvp.Value);
            }
            wrapper.OrbIds = orbIds.ToArray();
            wrapper.OrbValues = orbVals.ToArray();

            // Custom currencies
            var customIds = new System.Collections.Generic.List<string>();
            var customVals = new System.Collections.Generic.List<int>();
            foreach (var kvp in data.CustomCurrencies)
            {
                customIds.Add(kvp.Key);
                customVals.Add(kvp.Value);
            }
            wrapper.CustomCurrencyIds = customIds.ToArray();
            wrapper.CustomCurrencyValues = customVals.ToArray();

            return wrapper;
        }

        public VaultData ToVaultData()
        {
            var data = new VaultData
            {
                Version = Version,
                PlayerName = PlayerName ?? ""
            };

            // Parse last saved
            if (DateTime.TryParse(LastSaved, out var lastSaved))
                data.LastSaved = lastSaved;

            // Seasonal tokens
            data.SeasonalTokens.Clear();
            if (SeasonalTokenTypes != null && SeasonalTokenValues != null)
            {
                int count = Math.Min(SeasonalTokenTypes.Length, SeasonalTokenValues.Length);
                for (int i = 0; i < count; i++)
                {
                    if (Enum.IsDefined(typeof(SeasonalTokenType), SeasonalTokenTypes[i]))
                    {
                        data.SeasonalTokens[(SeasonalTokenType)SeasonalTokenTypes[i]] = SeasonalTokenValues[i];
                    }
                }
            }

            // Community tokens
            data.CommunityTokens.Clear();
            if (CommunityTokenKeys != null && CommunityTokenValues != null)
            {
                int count = Math.Min(CommunityTokenKeys.Length, CommunityTokenValues.Length);
                for (int i = 0; i < count; i++)
                {
                    data.CommunityTokens[CommunityTokenKeys[i]] = CommunityTokenValues[i];
                }
            }

            // Keys
            data.Keys.Clear();
            if (KeyIds != null && KeyValues != null)
            {
                int count = Math.Min(KeyIds.Length, KeyValues.Length);
                for (int i = 0; i < count; i++)
                {
                    data.Keys[KeyIds[i]] = KeyValues[i];
                }
            }

            // Tickets
            data.Tickets.Clear();
            if (TicketIds != null && TicketValues != null)
            {
                int count = Math.Min(TicketIds.Length, TicketValues.Length);
                for (int i = 0; i < count; i++)
                {
                    data.Tickets[TicketIds[i]] = TicketValues[i];
                }
            }

            // Orbs
            data.Orbs.Clear();
            if (OrbIds != null && OrbValues != null)
            {
                int count = Math.Min(OrbIds.Length, OrbValues.Length);
                for (int i = 0; i < count; i++)
                {
                    data.Orbs[OrbIds[i]] = OrbValues[i];
                }
            }

            // Custom currencies
            data.CustomCurrencies.Clear();
            if (CustomCurrencyIds != null && CustomCurrencyValues != null)
            {
                int count = Math.Min(CustomCurrencyIds.Length, CustomCurrencyValues.Length);
                for (int i = 0; i < count; i++)
                {
                    data.CustomCurrencies[CustomCurrencyIds[i]] = CustomCurrencyValues[i];
                }
            }

            return data;
        }
    }
}
