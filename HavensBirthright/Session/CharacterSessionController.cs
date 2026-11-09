using System;
using HavensBirthright.Patches;

namespace HavensBirthright.Session
{
    /// <summary>
    /// Single place for tracked switch identity, transient load policy, and elemental mismatch resets.
    /// </summary>
    internal static class CharacterSessionController
    {
        private static string _trackedCharacterId;

        internal static void ClearTrackedCharacterId()
        {
            _trackedCharacterId = null;
        }

        internal static void SyncTrackedCharacterIdFromSave()
        {
            if (Plugin.GetRacialBonusManager()?.GetPlayerRace() != null)
                _trackedCharacterId = CharacterFingerprint.GetCurrentCharacterSwitchId();
        }

        internal static void OnUpdateIdentityChecks()
        {
            CheckCharacterIdentityChanged();
            InvalidateStaleElementalVariant();
        }

        private static void CheckCharacterIdentityChanged()
        {
            try
            {
                string id = CharacterFingerprint.GetCurrentCharacterSwitchId();
                if (string.IsNullOrEmpty(id))
                    return;

                if (_trackedCharacterId == null)
                {
                    var mgr = Plugin.GetRacialBonusManager();
                    if (mgr?.GetPlayerRace() == null)
                        RaceDetectionService.RetryRaceDetection();
                    SyncTrackedCharacterIdFromSave();
                    return;
                }

                if (string.Equals(id, _trackedCharacterId, StringComparison.Ordinal))
                    return;

                if (IsTransientBodyOnlyIdentityChange(_trackedCharacterId, id))
                {
                    _trackedCharacterId = id;
                    return;
                }

                Plugin.Log.LogInfo($"[CharacterSession] Identity changed ({_trackedCharacterId} -> {id}), resetting state for new character");
                _trackedCharacterId = null;
                BirthrightRunner.ResetAllStateForNewSave();
                RaceDetectionService.RetryRaceDetection();
                SyncTrackedCharacterIdFromSave();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"[CharacterSession] CheckCharacterIdentityChanged: {ex.Message}");
            }
        }

        private static bool IsTransientBodyOnlyIdentityChange(string oldId, string newId)
        {
            if (string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(newId))
                return false;

            if (!CharacterFingerprint.TryParseCharacterSwitchFingerprint(oldId, out string on, out string orace, out byte oci, out int oslot, out string obody))
                return false;
            if (!CharacterFingerprint.TryParseCharacterSwitchFingerprint(newId, out string nn, out string nrace, out byte nci, out int nslot, out string nbody))
                return false;

            if (oci == CharacterFingerprint.LegacySwitchFingerprintCharIndex || nci == CharacterFingerprint.LegacySwitchFingerprintCharIndex ||
                oslot == CharacterFingerprint.LegacySwitchFingerprintListSlot || nslot == CharacterFingerprint.LegacySwitchFingerprintListSlot)
                return false;

            if (!string.Equals(on, nn, StringComparison.Ordinal)) return false;
            if (!string.Equals(orace, nrace, StringComparison.Ordinal)) return false;
            if (oci != nci) return false;
            if (oslot != nslot) return false;

            return string.IsNullOrEmpty(obody) && !string.IsNullOrEmpty(nbody);
        }

        private static void InvalidateStaleElementalVariant()
        {
            try
            {
                var mgr = Plugin.GetRacialBonusManager();
                var raceOpt = mgr?.GetPlayerRace();
                if (!raceOpt.HasValue)
                    return;

                Race r = raceOpt.Value;
                if (r != Race.WaterElemental && r != Race.FireElemental && r != Race.Elemental)
                    return;

                string body = CharacterFingerprint.GetCurrentBodyStyleName();
                if (string.IsNullOrEmpty(body))
                    return;

                Race resolved = ElementalVariantResolver.ResolveElementalFromBodyStyleName(body);
                if (resolved != Race.WaterElemental && resolved != Race.FireElemental)
                    return;

                if (r == Race.Elemental)
                {
                    Plugin.Log.LogDebug($"[CharacterSession] Cached generic Elemental but body resolves to {resolved}; resetting.");
                    BirthrightRunner.ResetAllStateForNewSave();
                    RaceDetectionService.RetryRaceDetection();
                    SyncTrackedCharacterIdFromSave();
                    return;
                }

                if (r == resolved)
                    return;

                Plugin.Log.LogDebug($"[CharacterSession] Cached elemental ({r}) mismatches body ({resolved}); resetting.");
                BirthrightRunner.ResetAllStateForNewSave();
                RaceDetectionService.RetryRaceDetection();
                SyncTrackedCharacterIdFromSave();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"[CharacterSession] InvalidateStaleElementalVariant: {ex.Message}");
            }
        }
    }
}
