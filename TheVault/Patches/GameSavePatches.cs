using System;
using System.Reflection;
using Wish;

namespace TheVault.Patches
{
    /// <summary>
    /// Minimal patches for GameSave class.
    /// Main vault loading is handled by PlayerPatches.OnPlayerInitialized.
    /// </summary>
    public static class GameSavePatches
    {
        public static string LastLoadedCharacterName { get; private set; } = null;

        public static void ResetLastLoadedSlot()
        {
            LastLoadedCharacterName = null;
            Plugin.Log?.LogDebug("GameSavePatches: Reset LastLoadedCharacterName");
        }

        public static void OnLoadCharacterAny(object[] __args, MethodBase __originalMethod)
        {
            try
            {
                int characterNumber = -1;
                if (__args != null && __args.Length > 0 && __args[0] != null)
                {
                    try
                    {
                        characterNumber = Convert.ToInt32(__args[0]);
                    }
                    catch
                    {
                        characterNumber = -1;
                    }
                }

                string sourceMethod = __originalMethod != null ? __originalMethod.Name : "LoadCharacter";
                Plugin.Log?.LogDebug($"GameSave.{sourceMethod} invoked (slot={characterNumber})");

                if (GameSave.Instance?.Saves == null ||
                    characterNumber < 0 ||
                    characterNumber >= GameSave.Instance.Saves.Count)
                {
                    TrySetCharacterFromCurrentCharacter("LoadCharacter fallback");
                    Plugin.Log?.LogWarning($"GameSavePatches: invalid or missing slot {characterNumber}; used fallback");
                    return;
                }

                var saveData = GameSave.Instance.Saves[characterNumber];
                string charName = GetCharacterNameFromSaveData(saveData);
                if (!string.IsNullOrEmpty(charName))
                {
                    LastLoadedCharacterName = charName;
                    PlayerPatches.SetPendingCharacterName(charName);
                    Plugin.Log?.LogDebug($"GameSavePatches: character name '{charName}' from slot {characterNumber}");
                }
                else
                {
                    TrySetCharacterFromCurrentCharacter("LoadCharacter slot resolve fallback");
                    Plugin.Log?.LogWarning($"GameSavePatches: could not resolve name for slot {characterNumber}");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"Error in OnLoadCharacterAny: {ex.Message}");
            }
        }

        private static string GetCharacterNameFromSaveData(GameSaveData saveData)
        {
            if (saveData == null) return null;

            string name = saveData.characterData?.characterName;
            if (!string.IsNullOrEmpty(name))
                return name;

            // Reflection fallback for game patches where characterData is nested or renamed.
            string reflected = GetCharacterNameFromUnknownSaveData(saveData);
            if (!string.IsNullOrEmpty(reflected))
                return reflected;

            if (!string.IsNullOrEmpty(saveData.fileName))
            {
                string fromFile = saveData.fileName;
                if (fromFile.EndsWith(".save", StringComparison.OrdinalIgnoreCase))
                    fromFile = fromFile.Substring(0, fromFile.Length - 5);
                if (!string.IsNullOrEmpty(fromFile))
                    return fromFile;
            }

            return null;
        }

        private static string GetCharacterNameFromUnknownSaveData(object saveData)
        {
            if (saveData == null) return null;

            var type = saveData.GetType();
            string[] possibleNames = { "characterName", "CharacterName", "name", "Name", "playerName", "PlayerName" };

            foreach (var propName in possibleNames)
            {
                var prop = type.GetProperty(propName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (prop != null && prop.PropertyType == typeof(string))
                {
                    var value = prop.GetValue(saveData) as string;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }

            var charDataProp = type.GetProperty("characterData", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (charDataProp != null)
            {
                var charDataObj = charDataProp.GetValue(saveData);
                if (charDataObj != null)
                {
                    var charDataType = charDataObj.GetType();
                    foreach (var propName in possibleNames)
                    {
                        var nameProp = charDataType.GetProperty(propName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
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

        private static void TrySetCharacterFromCurrentCharacter(string reason)
        {
            var currentChar = GameSave.CurrentCharacter;
            string currentName = currentChar?.characterName;
            if (string.IsNullOrEmpty(currentName))
                return;

            LastLoadedCharacterName = currentName;
            PlayerPatches.SetPendingCharacterName(currentName);
            Plugin.Log?.LogDebug($"GameSavePatches: using CurrentCharacter '{currentName}' ({reason})");
        }
    }
}
