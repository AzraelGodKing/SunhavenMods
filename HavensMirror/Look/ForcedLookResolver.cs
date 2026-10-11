using System;
using HavensMirror.Config;
using HavensMirror.Gallery;
using SunhavenMods.Shared;

namespace HavensMirror.Look
{
    /// <summary>
    /// Maps config ForcedLook + in-game season into a <see cref="LookSlot"/>.
    /// </summary>
    public static class ForcedLookResolver
    {
        public static bool TryParseForced(out LookSlot slot)
        {
            slot = LookSlot.Spring;
            string raw = MirrorOptions.ForcedLook?.Value;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            string key = raw.Trim();
            foreach (LookSlot candidate in Enum.GetValues(typeof(LookSlot)))
            {
                if (string.Equals(candidate.ToString(), key, StringComparison.OrdinalIgnoreCase))
                {
                    slot = candidate;
                    return true;
                }
            }

            // Friendly aliases players might type.
            switch (key.ToLowerInvariant())
            {
                case "fall":
                    slot = LookSlot.Autumn;
                    return true;
                case "wedding":
                case "marriage":
                case "ceremony":
                    slot = LookSlot.Vows;
                    return true;
                case "swimsuit":
                case "beach":
                case "swim":
                    slot = LookSlot.Shore;
                    return true;
                case "halloween":
                case "spooky":
                    slot = LookSlot.Costume;
                    return true;
                case "normal":
                case "default":
                    slot = LookSlot.Spring;
                    return true;
                default:
                    Plugin.Log?.LogWarning($"[Look] Unknown ForcedLook '{raw}' — ignored.");
                    return false;
            }
        }

        public static LookSlot ResolveActiveLook(bool vows, bool shore, bool costume)
        {
            if (TryParseForced(out LookSlot forced))
                return forced;

            if (vows) return LookSlot.Vows;
            if (shore) return LookSlot.Shore;
            if (costume) return LookSlot.Costume;
            return ResolveSeasonalLook();
        }

        public static LookSlot ResolveSeasonalLook()
        {
            if (TryParseForced(out LookSlot forced)
                && forced != LookSlot.Vows
                && forced != LookSlot.Shore
                && forced != LookSlot.Costume)
            {
                return forced;
            }

            string seasonName = TryReadSeasonName();
            if (string.IsNullOrEmpty(seasonName))
                return LookSlot.Spring;

            switch (seasonName.ToLowerInvariant())
            {
                case "summer": return LookSlot.Summer;
                case "fall":
                case "autumn": return LookSlot.Autumn;
                case "winter": return LookSlot.Winter;
                default: return LookSlot.Spring;
            }
        }

        /// <summary>
        /// Seasonal index used by NPC season animator loaders (0 spring, 1 summer, 2 fall, 3 winter).
        /// </summary>
        public static int ResolveSeasonAnimatorIndex()
        {
            switch (ResolveSeasonalLook())
            {
                case LookSlot.Summer: return 1;
                case LookSlot.Autumn: return 2;
                case LookSlot.Winter: return 3;
                default: return 0;
            }
        }

        public static void ApplyForcedFlags(ref bool vows, ref bool shore, ref bool costume)
        {
            if (!TryParseForced(out LookSlot forced))
                return;

            if (forced == LookSlot.Vows)
            {
                vows = true;
                shore = false;
                costume = false;
            }
            else if (forced == LookSlot.Shore)
            {
                shore = true;
                vows = false;
                costume = false;
            }
            else if (forced == LookSlot.Costume)
            {
                costume = true;
                vows = false;
                shore = false;
            }
        }

        private static string TryReadSeasonName()
        {
            try
            {
                // Prefer DayCycle.Instance.Season when present.
                var dayCycleType = ReflectionHelper.FindWishType("DayCycle");
                if (dayCycleType != null)
                {
                    object instance = ReflectionHelper.GetSingletonInstance(dayCycleType);
                    if (instance != null)
                    {
                        var seasonProp = AccessToolsProp(instance.GetType(), "Season")
                                         ?? AccessToolsProp(instance.GetType(), "season");
                        object season = seasonProp?.GetValue(instance, null);
                        if (season != null)
                            return season.ToString();
                    }
                }

                // Fallback: GameDate / world season fields if DayCycle is unavailable.
                var gameDateType = ReflectionHelper.FindWishType("GameDate");
                if (gameDateType != null)
                {
                    var seasonField = HarmonyLib.AccessTools.Field(gameDateType, "Season")
                                      ?? HarmonyLib.AccessTools.Field(gameDateType, "season");
                    if (seasonField != null && seasonField.IsStatic)
                    {
                        object season = seasonField.GetValue(null);
                        if (season != null)
                            return season.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[Look] Season read failed: {ex.Message}");
            }

            return null;
        }

        private static System.Reflection.PropertyInfo AccessToolsProp(Type type, string name)
        {
            return HarmonyLib.AccessTools.Property(type, name);
        }
    }
}
