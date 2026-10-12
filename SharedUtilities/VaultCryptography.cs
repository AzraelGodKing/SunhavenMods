using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Logging;
using UnityEngine;

namespace SunhavenMods.Shared
{
    /// <summary>
    /// Vault file crypto. New saves are written as CSVAULT3 (<see cref="VaultCsvault3"/>: per-vault salt, random IV,
    /// PBKDF2-SHA256, encrypt-then-MAC). CSVAULT2 and older legacy files stay readable and are upgraded on load.
    /// This is **tamper-resistant local storage**, not confidentiality against a motivated user with file access.
    /// </summary>
    public static class VaultCryptography
    {
        private const string EncryptionSalt = "TheV4ultS@lt2026Secure";
        private const int KeySize = 256;
        private const int Iterations = 10000;

        private static readonly byte[] Iv =
        {
            0x43, 0x75, 0x72, 0x72, 0x65, 0x6E, 0x63, 0x79, 0x53, 0x70, 0x65, 0x6C, 0x6C, 0x49, 0x56, 0x31
        };

        private static string _cachedSteamId;

        public static ManualLogSource LogSource { get; set; }

        /// <summary>True when the last successful <see cref="Decrypt"/> read a pre-CSVAULT3 file that should be rewritten.</summary>
        public static bool LastDecryptNeedsUpgrade { get; private set; }

        public static byte[] Encrypt(string plainText, string playerName)
        {
            return VaultCsvault3.Encrypt(plainText, playerName);
        }

        /// <param name="alternateKeyName">Sanitized file stem when it differs from <paramref name="playerName"/>.</param>
        public static string Decrypt(byte[] cipherData, string playerName, string alternateKeyName = null)
        {
            LastDecryptNeedsUpgrade = false;
            try
            {
                if (cipherData == null || cipherData.Length < 8)
                {
                    LogSource?.LogWarning("[VaultCryptography] Vault file too small.");
                    return null;
                }

                if (VaultCsvault3.HasHeader(cipherData))
                    return DecryptCsvault3(cipherData, playerName, alternateKeyName);

                string header = Encoding.UTF8.GetString(cipherData, 0, 8);
                if (header != "CSVAULT2")
                {
                    LogSource?.LogInfo("[VaultCryptography] Unencrypted vault payload; will re-encrypt on save.");
                    LastDecryptNeedsUpgrade = true;
                    return Encoding.UTF8.GetString(cipherData);
                }

                string json = DecryptCsvault2(cipherData, playerName, alternateKeyName);
                if (json != null)
                {
                    LastDecryptNeedsUpgrade = true;
                    return json;
                }

                LogSource?.LogError("[VaultCryptography] Decryption failed (legacy-compatible paths exhausted).");
                return null;
            }
            catch (Exception ex)
            {
                LogSource?.LogError($"[VaultCryptography] Decrypt error: {ex.Message}");
                return null;
            }
        }

        private static string DecryptCsvault3(byte[] cipherData, string playerName, string alternateKeyName)
        {
            if (VaultCsvault3.TryDecrypt(cipherData, playerName, out string json))
                return json;
            if (!string.IsNullOrEmpty(alternateKeyName) &&
                !string.Equals(alternateKeyName, playerName, StringComparison.OrdinalIgnoreCase) &&
                VaultCsvault3.TryDecrypt(cipherData, alternateKeyName, out json))
                return json;

            LogSource?.LogError("[VaultCryptography] CSVAULT3 integrity check failed (file corrupted, edited, or written for another character).");
            return null;
        }

        private static string DecryptCsvault2(byte[] cipherData, string playerName, string alternateKeyName)
        {
            // 1–2) Steam-keyed primary when Steam is up, then the player-portable key regardless of Steam state (AZR-242)
            string steamId = TryGetSteamId();
            string json;
            if (!string.IsNullOrEmpty(steamId))
            {
                json = DecryptWithPrimaryKey(cipherData, GenerateLegacySteamKey(steamId));
                if (IsVaultJson(json)) return json;
            }
            json = DecryptWithPrimaryKey(cipherData, DeriveKeyPlayerPortable(playerName));
            if (IsVaultJson(json)) return json;

            // 3) Legacy TryLegacyDecryption password strings (header skipped; matches migration intent)
            json = TryLegacyKeyStrings(cipherData, playerName);
            if (IsVaultJson(json)) return json;

            if (!string.IsNullOrEmpty(alternateKeyName) &&
                !string.Equals(alternateKeyName, playerName, StringComparison.OrdinalIgnoreCase))
            {
                json = TryLegacyKeyStrings(cipherData, alternateKeyName);
                if (IsVaultJson(json)) return json;
            }

            json = TryLegacyKeyStrings(cipherData, "Player_" + playerName);
            return IsVaultJson(json) ? json : null;
        }

        /// <summary>Public for persistence that still mirrors legacy load order.</summary>
        public static string TryLegacyDecryption(byte[] encryptedData, string playerName)
        {
            string json = TryLegacyKeyStrings(encryptedData, playerName);
            return IsVaultJson(json) ? json : null;
        }

        private static string TryLegacyKeyStrings(byte[] encryptedData, string playerName)
        {
            if (string.IsNullOrEmpty(playerName)) return null;
            string[] legacyMethods =
            {
                $"{EncryptionSalt}_{playerName}_TheVaultPortable",
                $"{EncryptionSalt}_Player_{playerName}_TheVaultPortable",
                $"{EncryptionSalt}_{playerName}_{GetMachineId()}"
            };
            foreach (var keySource in legacyMethods)
            {
                try
                {
                    byte[] key = GenerateLegacyKey(keySource);
                    string json = DecryptAesBodyAfterHeader(encryptedData, key);
                    if (IsVaultJson(json)) return json;
                }
                catch (Exception ex)
                {
                    LogSource?.LogWarning($"[VaultCryptography] Legacy key attempt failed ({ex.GetType().Name}).");
                }
            }
            return null;
        }

        private static string DecryptWithPrimaryKey(byte[] cipherData, byte[] key)
        {
            try
            {
                return DecryptAesBodyAfterHeader(cipherData, key);
            }
            catch (Exception ex)
            {
                LogSource?.LogWarning($"[VaultCryptography] Primary key decrypt failed ({ex.GetType().Name}).");
                return null;
            }
        }

        private static string DecryptAesBodyAfterHeader(byte[] cipherData, byte[] key)
        {
            int offset = 0;
            if (cipherData.Length >= 8 && Encoding.UTF8.GetString(cipherData, 0, 8) == "CSVAULT2")
                offset = 8;
            if (offset >= cipherData.Length) return null;

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = Iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(cipherData, offset, cipherData.Length - offset))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs, Encoding.UTF8))
                    return reader.ReadToEnd();
            }
        }

        private static byte[] GenerateLegacyKey(string combined)
        {
            // SHA1 matches the obsolete 3-arg ctor default — required for existing CSVAULT2 key compatibility.
            using (var deriveBytes = new Rfc2898DeriveBytes(
                combined,
                Encoding.UTF8.GetBytes(EncryptionSalt),
                Iterations,
                HashAlgorithmName.SHA1))
                return deriveBytes.GetBytes(KeySize / 8);
        }

        private static byte[] DeriveKeyPlayerPortable(string playerName)
        {
            string combined = $"{EncryptionSalt}_Player_{playerName}_TheVaultPortable";
            using (var deriveBytes = new Rfc2898DeriveBytes(
                combined,
                Encoding.UTF8.GetBytes(EncryptionSalt),
                Iterations,
                HashAlgorithmName.SHA1))
                return deriveBytes.GetBytes(KeySize / 8);
        }

        private static byte[] GenerateLegacySteamKey(string steamId)
        {
            return GenerateLegacyKey($"{EncryptionSalt}_Steam_{steamId}_TheVaultPortable");
        }

        /// <summary>
        /// Steam ID from Steamworks when available. Caches successes only; a miss is retried (AZR-242).
        /// </summary>
        internal static string TryGetSteamId()
        {
            if (!string.IsNullOrEmpty(_cachedSteamId))
                return _cachedSteamId;

            try
            {
                Assembly steamAssembly = FindSteamworksAssembly();
                if (steamAssembly == null)
                {
                    LogSource?.LogDebug("[VaultCryptography] Steam assembly not found.");
                    return null;
                }

                var steamUserType = steamAssembly.GetType("Steamworks.SteamUser");
                var getSteamIdMethod = steamUserType?.GetMethod("GetSteamID", BindingFlags.Public | BindingFlags.Static);
                if (getSteamIdMethod == null)
                {
                    LogSource?.LogDebug("[VaultCryptography] SteamUser.GetSteamID not found.");
                    return null;
                }

                var steamId = getSteamIdMethod.Invoke(null, null);
                if (steamId == null)
                {
                    LogSource?.LogDebug("[VaultCryptography] GetSteamID returned null.");
                    return null;
                }

                string steamIdStr = steamId.ToString();
                if (string.IsNullOrEmpty(steamIdStr) || steamIdStr == "0" || steamIdStr.Length < 10)
                {
                    LogSource?.LogDebug("[VaultCryptography] Invalid Steam ID.");
                    return null;
                }

                _cachedSteamId = steamIdStr;
                LogSource?.LogDebug($"[VaultCryptography] Retrieved Steam ID (length: {steamIdStr.Length}).");
                return _cachedSteamId;
            }
            catch (Exception ex)
            {
                LogSource?.LogDebug($"[VaultCryptography] Steam ID lookup failed: {ex.Message}");
                return null;
            }
        }

        private static Assembly FindSteamworksAssembly()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name != null && (name.Contains("rlabrecque") || name == "Steamworks.NET"))
                {
                    LogSource?.LogDebug($"[VaultCryptography] Found Steamworks assembly: {name}");
                    return assembly;
                }
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType("Steamworks.SteamUser") != null)
                    {
                        LogSource?.LogDebug($"[VaultCryptography] Found SteamUser in assembly: {assembly.GetName().Name}");
                        return assembly;
                    }
                }
                catch (Exception ex)
                {
                    LogSource?.LogDebug($"[VaultCryptography] Skipping assembly for Steam search: {ex.Message}");
                }
            }

            return null;
        }

        private static string GetMachineId()
        {
            try { return SystemInfo.deviceUniqueIdentifier; }
            catch (Exception ex)
            {
                LogSource?.LogWarning($"[VaultCryptography] Machine ID lookup failed ({ex.GetType().Name}).");
                return "unknown";
            }
        }

        private static bool IsVaultJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return false;
            if (json.IndexOf("\"PlayerName\"", StringComparison.Ordinal) >= 0) return true;
            // V3 persisted payload
            if (json.IndexOf("\"Entries\"", StringComparison.Ordinal) >= 0 &&
                json.IndexOf("\"CurrencyId\"", StringComparison.Ordinal) >= 0)
                return true;
            return false;
        }
    }
}
