using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace SunhavenMods.Shared
{
    /// <summary>
    /// Per-mod structured health reports (one static registry copy per compiled mod DLL).
    /// Aggregated cross-mod via <see cref="ModHealthAggregator"/>.
    /// </summary>
    public static class ModDiagnostics
    {
        private static readonly Dictionary<string, ModHealthReport> ReportsByPluginGuid =
            new Dictionary<string, ModHealthReport>(StringComparer.OrdinalIgnoreCase);

        private static readonly object Lock = new object();

        public static void ReportStartup(ModHealthReport report)
        {
            if (report == null || string.IsNullOrWhiteSpace(report.PluginGuid))
                return;

            report.ReportedUtc = DateTime.UtcNow;
            if (string.IsNullOrEmpty(report.SharedCodeRevision))
                report.SharedCodeRevision = SharedCodeRevision.Value;

            lock (Lock)
            {
                ReportsByPluginGuid[report.PluginGuid] = report.Clone();
            }
        }

        public static void ReportRuntime(string pluginGuid, Action<ModHealthReport> update)
        {
            if (string.IsNullOrWhiteSpace(pluginGuid) || update == null)
                return;

            lock (Lock)
            {
                if (!ReportsByPluginGuid.TryGetValue(pluginGuid, out var report))
                {
                    report = new ModHealthReport { PluginGuid = pluginGuid };
                    ReportsByPluginGuid[pluginGuid] = report;
                }

                update(report);
                report.ReportedUtc = DateTime.UtcNow;
            }
        }

        public static ModHealthReport GetReport(string pluginGuid)
        {
            if (string.IsNullOrWhiteSpace(pluginGuid))
                return null;

            lock (Lock)
            {
                return ReportsByPluginGuid.TryGetValue(pluginGuid, out var report)
                    ? report.Clone()
                    : null;
            }
        }

        public static string FormatHealthLogLine(ModHealthReport report)
        {
            if (report == null)
                return "[Health] (empty report)";

            string name = string.IsNullOrEmpty(report.DisplayName) ? report.PluginGuid : report.DisplayName;
            string version = string.IsNullOrEmpty(report.PluginVersion) ? "?" : report.PluginVersion;
            string shared = string.IsNullOrEmpty(report.SharedCodeRevision) ? "?" : report.SharedCodeRevision;
            string integrations = string.IsNullOrEmpty(report.IntegrationSummary) ? "—" : report.IntegrationSummary;
            string mode = string.IsNullOrEmpty(report.Mode) ? "—" : report.Mode;
            string character = string.IsNullOrEmpty(report.CharacterName) ? "—" : report.CharacterName;
            string data = report.DataLoaded.HasValue ? (report.DataLoaded.Value ? "loaded" : "none") : "—";
            string save = string.IsNullOrEmpty(report.LastPersistenceOutcome) ? "—" : report.LastPersistenceOutcome;

            return $"[Health] {name} v{version} | shared {shared} | integrations: {integrations} | mode: {mode} | character: {character} | data: {data} | save: {save}";
        }

        public static void LogStartupHealth(ManualLogSource log, ModHealthReport report)
        {
            ReportStartup(report);
            log?.LogInfo(FormatHealthLogLine(report));
        }

        /// <summary>
        /// Standard startup health line + registry entry for the Mod Health dashboard.
        /// </summary>
        public static void LogModStartup(
            ManualLogSource log,
            string pluginGuid,
            string displayName,
            string pluginVersion,
            string integrationSummary,
            string mode = "startup",
            bool? dataLoaded = false,
            string lastPersistenceOutcome = "—")
        {
            LogStartupHealth(log, new ModHealthReport
            {
                PluginGuid = pluginGuid,
                DisplayName = displayName,
                PluginVersion = pluginVersion,
                SharedCodeRevision = SharedCodeRevision.Value,
                IntegrationSummary = integrationSummary,
                Mode = mode,
                DataLoaded = dataLoaded,
                LastPersistenceOutcome = lastPersistenceOutcome
            });
        }
    }
}
