using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using SunhavenMods.Shared;

namespace TheVault
{
    /// <summary>
    /// Secret gift system - gives special items to specific users on first vault open
    /// Stores encrypted gift tracking data with backup support
    /// </summary>
    public static class SecretGifts
    {
        // Steam ID hashes for users eligible for secret gifts (SHA256 -> Base64)
        private static readonly HashSet<string> _eligibleSteamIdHashes = new HashSet<string>
        {
            "Tr4eyf86yKRu6fhNQjQOrO/ZnwasQaY/fgKV0PcnoGA=",
            "zAKJKUjJVGiQRm+4gCGcMZqq1sF8vQZT7bhd+r0Q0kw="  
        };

        // Treasure Chest item ID in Sun Haven
        private const int TREASURE_CHEST_ITEM_ID = 3789; 
        private const int GIFT_QUANTITY = 10;

        // Encryption settings (same pattern as VaultSaveSystem)
        private const string ENCRYPTION_SALT = "SecretG1fts_S@lt2026_Secure";
        private const int KEY_SIZE = 256;
        private const int ITERATIONS = 10000;
        private static readonly byte[] _iv = new byte[16] { 0x53, 0x65, 0x63, 0x72, 0x65, 0x74, 0x47, 0x69, 0x66, 0x74, 0x73, 0x49, 0x56, 0x30, 0x30, 0x31 };
        private const string FILE_HEADER = "SCRTGIFT";

        private static string _currentSteamId;
        private static bool _giftCheckPerformed;
        private static bool _dataLoaded;
        private static HashSet<string> _giftRecipients = new HashSet<string>();

        /// <summary>
        /// Check if the current user should receive a secret gift on first vault open.
        /// Call this from VaultUI.Show()
        /// </summary>
        public static void CheckAndGiveFirstOpenGift(string characterName)
        {
            if (_giftCheckPerformed) return;
            _giftCheckPerformed = true;

            try
            {
                // Get current Steam ID
                _currentSteamId = VaultCryptography.TryGetSteamId();
                if (string.IsNullOrEmpty(_currentSteamId))
                {
                    Plugin.Log?.LogInfo("[SecretGifts] Could not retrieve Steam ID");
                    return;
                }

                // Load gift data from disk if not already loaded this session
                if (!_dataLoaded)
                {
                    LoadGiftRecipients();
                    _dataLoaded = true;
                }

                // Check if eligible
                string steamHash = ComputeHash(_currentSteamId);
                Plugin.Log?.LogDebug($"[SecretGifts] Steam hash: {steamHash}");

                if (!_eligibleSteamIdHashes.Contains(steamHash))
                {
                    Plugin.Log?.LogInfo("[SecretGifts] User not eligible for secret gift");
                    return;
                }

                // Check if already received gift for this character
                if (HasReceivedGift(characterName))
                {
                    Plugin.Log?.LogInfo($"[SecretGifts] {characterName} has already received the secret gift");
                    return;
                }

                // Give the gift!
                Plugin.Log?.LogInfo($"[SecretGifts] Giving secret gift to {characterName}!");
                bool success = SpawnItems(TREASURE_CHEST_ITEM_ID, GIFT_QUANTITY);

                if (success)
                {
                    MarkGiftReceived(characterName);
                    Plugin.Log?.LogInfo($"[SecretGifts] Successfully gave {GIFT_QUANTITY} treasure chests to {characterName}!");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[SecretGifts] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reset the gift check flag when returning to menu (for character switching).
        /// The gift data stays in memory so it persists across character switches.
        /// </summary>
        public static void ResetGiftCheck()
        {
            _giftCheckPerformed = false;
            // Note: _dataLoaded and _giftRecipients stay intact so data persists across character switches
            Plugin.Log?.LogInfo($"[SecretGifts] Reset for character switch. {_giftRecipients.Count} gift records in memory.");
        }

        private static bool SpawnItems(int itemId, int amount)
        {
            try
            {
                var playerInstance = Wish.Player.Instance;
                if (playerInstance == null || playerInstance.Inventory == null)
                {
                    Plugin.Log?.LogWarning("[SecretGifts] Player or inventory not available");
                    return false;
                }

                var inventory = playerInstance.Inventory;
                var addMethod = AccessTools.Method(inventory.GetType(), "AddItem",
                    new[] { typeof(int), typeof(int), typeof(bool) });

                if (addMethod != null)
                {
                    addMethod.Invoke(inventory, new object[] { itemId, amount, true });
                    return true;
                }
                else
                {
                    Plugin.Log?.LogWarning("[SecretGifts] Could not find AddItem method");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[SecretGifts] Spawn error: {ex.Message}");
            }

            return false;
        }

        private static string GetSaveDirectory()
        {
            return Path.Combine(BepInEx.Paths.ConfigPath, "TheVault", "Saves");
        }

        private static string GetGiftTrackingFilePath()
        {
            return Path.Combine(GetSaveDirectory(), "SecretGifts.dat");
        }

        private static string GetBackupFilePath()
        {
            return Path.Combine(GetSaveDirectory(), "SecretGifts.dat.backup");
        }

        /// <summary>
        /// Load gift recipients from encrypted file
        /// </summary>
        private static void LoadGiftRecipients()
        {
            try
            {
                string filePath = GetGiftTrackingFilePath();
                string backupPath = GetBackupFilePath();

                // Try main file first
                if (File.Exists(filePath))
                {
                    if (TryLoadFromFile(filePath))
                    {
                        Plugin.Log?.LogInfo($"[SecretGifts] Loaded {_giftRecipients.Count} gift records");
                        return;
                    }
                }

                // Try backup if main file failed
                if (File.Exists(backupPath))
                {
                    Plugin.Log?.LogInfo("[SecretGifts] Main file failed, trying backup...");
                    if (TryLoadFromFile(backupPath))
                    {
                        Plugin.Log?.LogInfo($"[SecretGifts] Restored {_giftRecipients.Count} gift records from backup");
                        // Restore main file from backup
                        SaveGiftRecipients();
                        return;
                    }
                }

                // No valid data found, start fresh
                _giftRecipients = new HashSet<string>();
                Plugin.Log?.LogInfo("[SecretGifts] No existing gift records found, starting fresh");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[SecretGifts] Error loading gift records: {ex.Message}");
                _giftRecipients = new HashSet<string>();
            }
        }

        private static bool TryLoadFromFile(string filePath)
        {
            try
            {
                byte[] encryptedData = File.ReadAllBytes(filePath);
                string decrypted = Decrypt(encryptedData);

                if (string.IsNullOrEmpty(decrypted))
                    return false;

                _giftRecipients = new HashSet<string>();
                string[] lines = decrypted.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        _giftRecipients.Add(line.Trim());
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[SecretGifts] TryLoadFromFile: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Save gift recipients to encrypted file with backup
        /// </summary>
        private static void SaveGiftRecipients()
        {
            try
            {
                string filePath = GetGiftTrackingFilePath();
                CharacterSaveStore.EnsureDirectory(GetSaveDirectory());

                string data = string.Join("\n", _giftRecipients);
                byte[] encrypted = Encrypt(data);

                CharacterSaveStore.WriteAtomicBytes(
                    filePath,
                    encrypted,
                    CharacterSaveStore.VaultBackupSuffix);

                Plugin.Log?.LogInfo($"[SecretGifts] Saved {_giftRecipients.Count} gift records (encrypted)");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[SecretGifts] Error saving gift records: {ex.Message}");
            }
        }

        private static bool HasReceivedGift(string characterName)
        {
            string key = $"{_currentSteamId}:{characterName}";
            return _giftRecipients.Contains(key);
        }

        private static void MarkGiftReceived(string characterName)
        {
            string key = $"{_currentSteamId}:{characterName}";
            _giftRecipients.Add(key);
            SaveGiftRecipients();
        }

        #region Encryption

        private static byte[] GenerateKey()
        {
            // Use Steam ID if available for consistent encryption across devices
            string identifier = !string.IsNullOrEmpty(_currentSteamId)
                ? $"Steam_{_currentSteamId}"
                : "DefaultKey";

            string combined = $"{ENCRYPTION_SALT}_{identifier}_SecretGiftsKey";

            // SHA1 matches the obsolete 3-arg ctor default — keep existing secret-gift key material.
            using (var deriveBytes = new Rfc2898DeriveBytes(
                combined,
                Encoding.UTF8.GetBytes(ENCRYPTION_SALT),
                ITERATIONS,
                HashAlgorithmName.SHA1))
            {
                return deriveBytes.GetBytes(KEY_SIZE / 8);
            }
        }

        private static byte[] Encrypt(string plainText)
        {
            byte[] key = GenerateKey();

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = _iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    // Write magic header
                    byte[] header = Encoding.UTF8.GetBytes(FILE_HEADER);
                    ms.Write(header, 0, header.Length);

                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var writer = new StreamWriter(cs, Encoding.UTF8))
                    {
                        writer.Write(plainText);
                    }

                    return ms.ToArray();
                }
            }
        }

        private static string Decrypt(byte[] cipherData)
        {
            try
            {
                // Check for magic header
                if (cipherData.Length < FILE_HEADER.Length)
                {
                    Plugin.Log?.LogWarning("[SecretGifts] File too small, may be corrupted");
                    return null;
                }

                string header = Encoding.UTF8.GetString(cipherData, 0, FILE_HEADER.Length);
                if (header != FILE_HEADER)
                {
                    // Try to read as plain text (legacy unencrypted file migration)
                    Plugin.Log?.LogInfo("[SecretGifts] Detected legacy unencrypted file, will re-encrypt on save");
                    return Encoding.UTF8.GetString(cipherData);
                }

                byte[] key = GenerateKey();

                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = _iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(cipherData, FILE_HEADER.Length, cipherData.Length - FILE_HEADER.Length))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cs, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (CryptographicException ex)
            {
                Plugin.Log?.LogError($"[SecretGifts] Decryption failed: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[SecretGifts] Error decrypting: {ex.Message}");
                return null;
            }
        }

        #endregion

        private static string ComputeHash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(bytes);
            }
        }
    }
}
