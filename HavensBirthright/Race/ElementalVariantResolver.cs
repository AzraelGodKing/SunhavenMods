using HavensBirthright.Session;

namespace HavensBirthright
{
    /// <summary>
    /// Fire vs water Elemental resolution from body style (layer 14) strings — shared by abilities, stats, and patches.
    /// </summary>
    internal static class ElementalVariantResolver
    {
        internal static Race ResolveElementalFromBodyStyleName(string bodyStyleName)
        {
            if (string.IsNullOrEmpty(bodyStyleName))
            {
                Plugin.Log.LogWarning("[RaceDetection] No body style name for Elemental, using generic");
                return Race.Elemental;
            }

            Plugin.Log.LogDebug($"[RaceDetection] Elemental body style (key 14): {bodyStyleName}");
            string bodyLower = bodyStyleName.ToLowerInvariant();

            if (bodyLower.Contains("fire"))
                return Race.FireElemental;
            if (bodyLower.Contains("water"))
                return Race.WaterElemental;

            Plugin.Log.LogWarning($"[RaceDetection] Could not determine Elemental variant from '{bodyStyleName}', using generic");
            return Race.Elemental;
        }

        internal static Race ResolveElementalAbilityRace(Race race)
        {
            if (race != Race.Elemental)
                return race;
            return ResolveElementalFromBodyStyleName(CharacterFingerprint.GetCurrentBodyStyleName());
        }

        internal static Race ResolveRaceForActiveAbilityToggle(Race stored)
        {
            if (stored != Race.WaterElemental && stored != Race.FireElemental && stored != Race.Elemental)
                return stored;
            string body = CharacterFingerprint.GetCurrentBodyStyleName();
            if (string.IsNullOrEmpty(body))
                return stored;
            Race fromBody = ResolveElementalFromBodyStyleName(body);
            if (fromBody == Race.WaterElemental || fromBody == Race.FireElemental)
                return fromBody;
            return stored;
        }
    }
}
