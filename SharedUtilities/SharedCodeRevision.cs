namespace SunhavenMods.Shared
{
    /// <summary>
    /// Bump when linked SharedUtilities behavior changes materially (same PR as the change).
    /// Reported at mod startup for cross-mod skew detection in Haven Dev Tools → Health.
    /// </summary>
    public static class SharedCodeRevision
    {
        public const string Value = "2026.11.12";
    }
}
