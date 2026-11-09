using HavensBirthright.Session;
using SunhavenMods.Shared;
using System;
using System.Collections.Generic;
using Wish;

namespace HavensBirthright
{
    /// <summary>
    /// Two-tier race detection (Wish.Race + Amari SubRace / Elemental body string).
    /// </summary>
    internal static class RaceDetectionService
    {
        private static bool _raceDetected;

        private const int SUBRACE_DEFAULT = 0;
        private const int SUBRACE_CAT = 1;
        private const int SUBRACE_DOG = 2;
        private const int SUBRACE_BIRD = 3;
        private const int SUBRACE_AQUATIC = 4;
        private const int SUBRACE_GREEN_REPTILE = 5;
        private const int SUBRACE_ORANGE_REPTILE = 6;

        /// <summary>
        /// Called from <see cref="Patches.PlayerPatches.OnPlayerInitialized"/> after reset (flag already cleared there).
        /// </summary>
        internal static void DetectFromPlayerInitialized()
        {
            _raceDetected = false;
            DetectAndSetRace();
        }

        internal static void DetectIfNeeded()
        {
            if (!_raceDetected)
                DetectAndSetRace();
        }

        internal static void ResetRaceDetection()
        {
            _raceDetected = false;
        }

        internal static void RetryRaceDetection()
        {
            var mgr = Plugin.GetRacialBonusManager();
            if (mgr != null && mgr.GetPlayerRace() == null)
                _raceDetected = false;
            else if (_raceDetected && mgr != null && CachedElementalContradictsBody(mgr))
            {
                Plugin.Log.LogDebug("[RaceDetection] Cached elemental race disagrees with body style; re-detecting.");
                _raceDetected = false;
            }
            if (!_raceDetected)
                DetectAndSetRace();
        }

        private static bool CachedElementalContradictsBody(RacialBonusManager mgr)
        {
            var opt = mgr.GetPlayerRace();
            if (!opt.HasValue)
                return false;
            Race r = opt.Value;
            if (r != Race.WaterElemental && r != Race.FireElemental && r != Race.Elemental)
                return false;
            string body = CharacterFingerprint.GetCurrentBodyStyleName();
            if (string.IsNullOrEmpty(body))
                return false;
            Race fromBody = ElementalVariantResolver.ResolveElementalFromBodyStyleName(body);
            if (r == Race.Elemental)
                return fromBody == Race.WaterElemental || fromBody == Race.FireElemental;
            if (fromBody != Race.WaterElemental && fromBody != Race.FireElemental)
                return false;
            return r != fromBody;
        }

        private static void DetectAndSetRace()
        {
            if (_raceDetected)
                return;

            try
            {
                if (Player.Instance == null)
                    return;

                var currentChar = CharacterFingerprint.GetAuthoritativeCharacterData();
                if (currentChar == null)
                    return;

                if (currentChar.StyleData == null || currentChar.StyleData.Count == 0)
                    return;

                var manager = Plugin.GetRacialBonusManager();
                if (manager == null)
                {
                    Plugin.Log.LogError("RacialBonusManager is NULL - cannot proceed");
                    return;
                }

                byte gameRace = currentChar.race;
                var wishRace = (Wish.Race)gameRace;
                Plugin.Log.LogDebug($"[RaceDetection] Tier 1 - Game race byte: {gameRace}, Wish.Race: {wishRace}");

                string bodyStyleName = null;
                currentChar.StyleData.TryGetValue(14, out bodyStyleName);

                Race modRace;
                int detectedSubRace = -1;

                switch (wishRace)
                {
                    case Wish.Race.Amari:
                        detectedSubRace = TryGetSubRaceFromClothingData(bodyStyleName);
                        if (detectedSubRace >= 0)
                        {
                            Plugin.Log.LogDebug($"[RaceDetection] Tier 2 - Amari SubRace from ClothingData: {detectedSubRace}");
                            modRace = ResolveAmariRace(detectedSubRace);
                        }
                        else
                        {
                            Plugin.Log.LogDebug("[RaceDetection] Tier 2 - ClothingData lookup failed, falling back to string parsing");
                            modRace = ResolveAmariFromString(bodyStyleName);
                        }
                        break;

                    case Wish.Race.Elemental:
                        modRace = ElementalVariantResolver.ResolveElementalFromBodyStyleName(bodyStyleName);
                        break;

                    case Wish.Race.Naga:
                        modRace = Race.Naga;
                        detectedSubRace = TryGetSubRaceFromClothingData(bodyStyleName);
                        Plugin.Log.LogDebug($"[RaceDetection] Tier 2 - Naga SubRace: {detectedSubRace} (no variant needed)");
                        break;

                    default:
                        modRace = ConvertGameRaceByName(wishRace.ToString());
                        break;
                }

                manager.SetPlayerRace(modRace, detectedSubRace);
                _raceDetected = true;
                Plugin.Log.LogInfo($"Player race set to: {modRace} (SubRace cached: {detectedSubRace})");
                CharacterSessionController.SyncTrackedCharacterIdFromSave();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"!!! CRITICAL ERROR in race detection: {ex.Message}");
                Plugin.Log.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private static int TryGetSubRaceFromClothingData(string bodyStyleName)
        {
            if (string.IsNullOrEmpty(bodyStyleName))
                return -1;

            try
            {
                var clothingStylesType = ReflectionHelper.FindWishType("CharacterClothingStyles");
                if (clothingStylesType == null)
                {
                    Plugin.Log.LogDebug("[RaceDetection] Could not find CharacterClothingStyles type");
                    return -1;
                }

                var clothingStyles = ReflectionHelper.GetStaticValue(clothingStylesType, "ClothingStyles");
                if (clothingStyles == null)
                {
                    Plugin.Log.LogDebug("[RaceDetection] ClothingStyles is null");
                    return -1;
                }

                var bodyLayerDict = ReflectionHelper.InvokeMethod(clothingStyles, "get_Item", (ClothingLayer)14);
                if (bodyLayerDict == null)
                {
                    Plugin.Log.LogDebug("[RaceDetection] No Body layer in ClothingStyles");
                    return -1;
                }

                var containsMethod = bodyLayerDict.GetType().GetMethod("ContainsKey");
                if (containsMethod != null)
                {
                    bool contains = (bool)containsMethod.Invoke(bodyLayerDict, new object[] { bodyStyleName });
                    if (!contains)
                    {
                        Plugin.Log.LogDebug($"[RaceDetection] Body style '{bodyStyleName}' not found in ClothingStyles[Body]");
                        return -1;
                    }
                }

                var clothingLayerData = ReflectionHelper.InvokeMethod(bodyLayerDict, "get_Item", bodyStyleName);
                if (clothingLayerData == null)
                {
                    Plugin.Log.LogDebug($"[RaceDetection] ClothingLayerData is null for '{bodyStyleName}'");
                    return -1;
                }

                var subRaceValue = ReflectionHelper.GetInstanceValue(clothingLayerData, "subRace");
                if (subRaceValue == null)
                {
                    Plugin.Log.LogDebug("[RaceDetection] subRace field not found on ClothingLayerData");
                    return -1;
                }

                int subRaceInt = (int)subRaceValue;
                Plugin.Log.LogDebug($"[RaceDetection] ClothingLayerData.subRace = {subRaceValue} ({subRaceInt})");
                return subRaceInt;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[RaceDetection] ClothingLayerData lookup failed: {ex.Message}");
                return -1;
            }
        }

        private static Race ResolveAmariRace(int subRace)
        {
            switch (subRace)
            {
                case SUBRACE_CAT:
                    return Race.AmariCat;
                case SUBRACE_DOG:
                    return Race.AmariDog;
                case SUBRACE_BIRD:
                    return Race.AmariBird;
                case SUBRACE_AQUATIC:
                    return Race.AmariAquatic;
                case SUBRACE_GREEN_REPTILE:
                case SUBRACE_ORANGE_REPTILE:
                    return Race.AmariReptile;
                default:
                    Plugin.Log.LogWarning($"[RaceDetection] Unknown Amari SubRace: {subRace}, using generic Amari");
                    return Race.Amari;
            }
        }

        private static Race ResolveAmariFromString(string bodyStyleName)
        {
            if (string.IsNullOrEmpty(bodyStyleName))
            {
                Plugin.Log.LogWarning("[RaceDetection] No body style name for Amari, using generic");
                return Race.Amari;
            }

            Plugin.Log.LogDebug($"[RaceDetection] Amari string fallback, body style: {bodyStyleName}");
            string bodyLower = bodyStyleName.ToLowerInvariant();

            if (bodyLower.Contains("cat"))
                return Race.AmariCat;
            if (bodyLower.Contains("dog") || bodyLower.Contains("wolf") || bodyLower.Contains("canine"))
                return Race.AmariDog;
            if (bodyLower.Contains("bird") || bodyLower.Contains("avian") || bodyLower.Contains("feather"))
                return Race.AmariBird;
            if (bodyLower.Contains("aquatic") || bodyLower.Contains("fish") || bodyLower.Contains("amphibian") || bodyLower.Contains("frog"))
                return Race.AmariAquatic;
            if (bodyLower.Contains("reptile") || bodyLower.Contains("lizard") || bodyLower.Contains("dragon") || bodyLower.Contains("snake"))
                return Race.AmariReptile;

            Plugin.Log.LogWarning($"[RaceDetection] Could not determine Amari variant from '{bodyStyleName}', using generic");
            return Race.Amari;
        }

        private static Race ConvertGameRaceByName(string raceName)
        {
            string normalized = raceName?.ToLowerInvariant() ?? "";

            if (normalized.Contains("human"))
                return Race.Human;
            if (normalized.Contains("elf"))
                return Race.Elf;
            if (normalized.Contains("angel"))
                return Race.Angel;
            if (normalized.Contains("demon"))
                return Race.Demon;
            if (normalized.Contains("naga"))
                return Race.Naga;

            if (normalized.Contains("fire") && normalized.Contains("element"))
                return Race.FireElemental;
            if (normalized.Contains("water") && normalized.Contains("element"))
                return Race.WaterElemental;
            if (normalized.Contains("element"))
                return Race.Elemental;
            if (normalized.Contains("amari"))
                return Race.Amari;

            Plugin.Log.LogWarning($"Unknown race name: {raceName}, defaulting to Human");
            return Race.Human;
        }
    }
}
