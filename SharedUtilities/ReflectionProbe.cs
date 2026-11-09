using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace SunhavenMods.Shared
{
    /// <summary>
    /// Reflection probe helper: log the first failure per call-site, then stay silent (AZR-254).
    /// </summary>
    public static class ReflectionProbe
    {
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);
        private static readonly object Lock = new object();

        public static bool Try(string site, Action action, ManualLogSource log = null)
        {
            if (action == null)
                return false;

            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                bool first;
                lock (Lock)
                {
                    first = Reported.Add(site ?? "unknown");
                }

                if (first)
                {
                    log?.LogWarning(
                        $"[Probe] {site} failed ({ex.GetType().Name}: {ex.Message}) — game version drift?");
                }

                return false;
            }
        }

        public static bool Try<T>(string site, Func<T> func, out T result, ManualLogSource log = null)
        {
            result = default;
            if (func == null)
                return false;

            try
            {
                result = func();
                return true;
            }
            catch (Exception ex)
            {
                bool first;
                lock (Lock)
                {
                    first = Reported.Add(site ?? "unknown");
                }

                if (first)
                {
                    log?.LogWarning(
                        $"[Probe] {site} failed ({ex.GetType().Name}: {ex.Message}) — game version drift?");
                }

                return false;
            }
        }

        public static void LogOnce(string site, Exception ex, ManualLogSource log = null)
        {
            bool first;
            lock (Lock)
            {
                first = Reported.Add(site ?? "unknown");
            }

            if (first)
            {
                string msg = ex != null
                    ? $"{ex.GetType().Name}: {ex.Message}"
                    : "unknown error";
                log?.LogWarning($"[Probe] {site} failed ({msg}) — game version drift?");
            }
        }

    }
}
