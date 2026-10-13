using System;
using System.Collections.Generic;
using System.Text;
using HavenDevTools.Services;
using SunhavenMods.Shared;
using UnityEngine;

namespace HavenDevTools.UI
{
    /// <summary>
    /// Suite-wide mod health dashboard (Health tab).
    /// </summary>
    public static class ModHealthDashboardPanel
    {
        public static readonly (string guid, string name)[] KnownSuiteMods =
        {
            ("com.azraelgodking.havendevtools", "Haven Dev Tools"),
            ("com.azraelgodking.senpaischest", "Senpai's Chest"),
            ("com.azraelgodking.squirrelsbirthdayreminder", "Birthday Reminder"),
            ("com.azraelgodking.havensbirthright", "Haven's Birthright"),
            ("com.azraelgodking.sunhavenmuseumutilitytracker", "S.M.U.T."),
            ("com.azraelgodking.sunhaventodo", "Sun Haven Todo"),
            ("com.azraelgodking.thevault", "The Vault"),
            ("com.azraelgodking.havensalmanac", "Haven's Almanac"),
            ("com.azraelgodking.fasterraces", "Faster Races"),
            ("com.azraelgodking.trinketfortune", "Trinket Fortune"),
            ("com.azraelgodking.cropoptimizer", "Crop Optimizer"),
            ("com.azraelgodking.havensrespec", "Haven's Respec"),
            ("com.azraelgodking.giftingassistant", "Gifting Assistant"),
        };

        private static IReadOnlyList<ModHealthAggregator.MergedModHealthRow> _cachedRows =
            Array.Empty<ModHealthAggregator.MergedModHealthRow>();

        private static ModHealthAggregator.SharedCodeSkewAnalysis _cachedSkew;
        private static DateTime _lastRefreshUtc = DateTime.MinValue;
        private static readonly HashSet<string> ExpandedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static Vector2 _scrollPosition;

        private static Dictionary<string, VersionChecker.VersionCheckResult> _versionResults =
            new Dictionary<string, VersionChecker.VersionCheckResult>(StringComparer.OrdinalIgnoreCase);

        private static bool _isCheckingVersions;

        public static void Draw(
            GUIStyle boxStyle,
            GUIStyle sectionHeaderStyle,
            GUIStyle buttonStyle,
            GUIStyle labelStyle)
        {
            GUILayout.BeginVertical(boxStyle);
            GUILayout.Label(ModLocalization.T("devtools.health.title"), sectionHeaderStyle);
            GUILayout.Space(4);

            DrawToolbar(buttonStyle, labelStyle);
            GUILayout.Space(6);
            DrawSkewBanner(labelStyle);
            GUILayout.Space(4);

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            DrawModRows(buttonStyle, labelStyle);
            GUILayout.Space(8);
            DrawVersionCheckerSection(sectionHeaderStyle, buttonStyle, labelStyle);
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
        }

        private static void DrawToolbar(GUIStyle buttonStyle, GUIStyle labelStyle)
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(ModLocalization.T("devtools.health.refresh"), buttonStyle))
                RefreshRows(force: true);

            if (GUILayout.Button(ModLocalization.T("devtools.health.copyAll"), buttonStyle))
            {
                string text = ModHealthAggregator.BuildCombinedDiagnosticDump(_cachedRows);
                CopyToClipboard(text);
            }

            int issueCount = 0;
            for (int i = 0; i < _cachedRows.Count; i++)
            {
                if (_cachedRows[i].HasIssue)
                    issueCount++;
            }

            string summary = ModLocalization.T(
                "devtools.health.summary",
                _cachedRows.Count.ToString(),
                issueCount.ToString(),
                _cachedSkew?.HasSkew == true
                    ? ModLocalization.T("devtools.health.skewShort")
                    : ModLocalization.T("devtools.health.skewNone"));
            GUILayout.Label(summary, labelStyle);
            GUILayout.EndHorizontal();
        }

        private static void DrawSkewBanner(GUIStyle labelStyle)
        {
            if (_cachedSkew == null || !_cachedSkew.HasSkew)
                return;

            var warnStyle = new GUIStyle(labelStyle)
            {
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.85f, 0.35f) }
            };

            GUILayout.Label(ModLocalization.T("devtools.health.skewTitle"), warnStyle);
            foreach (var kvp in _cachedSkew.ModsByRevision)
            {
                string names = string.Join(", ", kvp.Value);
                GUILayout.Label(
                    ModLocalization.T("devtools.health.skewRevision", kvp.Key, names),
                    warnStyle);
            }
        }

        private static void DrawModRows(GUIStyle buttonStyle, GUIStyle labelStyle)
        {
            if (_cachedRows.Count == 0)
            {
                GUILayout.Label(ModLocalization.T("devtools.health.noMods"), labelStyle);
                return;
            }

            foreach (var row in _cachedRows)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.BeginHorizontal();

                string statusKey = row.HasIssue
                    ? "devtools.health.statusIssue"
                    : row.HasDiagnosticsReport
                        ? "devtools.health.statusOk"
                        : "devtools.health.statusNoReport";
                var statusStyle = new GUIStyle(labelStyle)
                {
                    normal =
                    {
                        textColor = row.HasIssue
                            ? new Color(1f, 0.55f, 0.55f)
                            : row.HasDiagnosticsReport
                                ? new Color(0.55f, 1f, 0.55f)
                                : new Color(0.75f, 0.75f, 0.75f)
                    }
                };
                GUILayout.Label(ModLocalization.T(statusKey), statusStyle, GUILayout.Width(72));

                string title = string.IsNullOrEmpty(row.InstalledVersion)
                    ? row.DisplayName
                    : $"{row.DisplayName} v{row.InstalledVersion}";
                GUILayout.Label(title, labelStyle);

                if (GUILayout.Button(
                        ExpandedGuids.Contains(row.PluginGuid)
                            ? "▼"
                            : ModLocalization.T("devtools.health.expand"),
                        buttonStyle,
                        GUILayout.Width(64)))
                {
                    if (!ExpandedGuids.Add(row.PluginGuid))
                        ExpandedGuids.Remove(row.PluginGuid);
                }

                if (GUILayout.Button(ModLocalization.T("devtools.health.copyRow"), buttonStyle, GUILayout.Width(56)))
                {
                    var sb = new StringBuilder();
                    row.AppendDiagnosticDump(sb);
                    CopyToClipboard(sb.ToString());
                }

                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(row.IntegrationSummary))
                {
                    GUILayout.Label(
                        ModLocalization.T("devtools.health.integrations", row.IntegrationSummary),
                        labelStyle);
                }

                if (!string.IsNullOrEmpty(row.SharedCodeRevision))
                {
                    GUILayout.Label(
                        ModLocalization.T("devtools.health.sharedCode", row.SharedCodeRevision),
                        labelStyle);
                }

                if (ExpandedGuids.Contains(row.PluginGuid))
                {
                    var sb = new StringBuilder();
                    row.AppendDiagnosticDump(sb);
                    GUILayout.Label(sb.ToString(), labelStyle);
                }

                GUILayout.EndVertical();
                GUILayout.Space(4);
            }
        }

        private static void DrawVersionCheckerSection(
            GUIStyle sectionHeaderStyle,
            GUIStyle buttonStyle,
            GUIStyle labelStyle)
        {
            GUILayout.Label(ModLocalization.T("devtools.versions.title"), sectionHeaderStyle);
            GUILayout.Space(3);

            GUILayout.BeginHorizontal();
            GUI.enabled = !_isCheckingVersions;
            if (GUILayout.Button(
                    _isCheckingVersions
                        ? ModLocalization.T("devtools.versions.checking")
                        : ModLocalization.T("devtools.versions.checkAll"),
                    buttonStyle))
            {
                CheckAllModVersions();
            }

            GUI.enabled = true;

            if (GUILayout.Button(ModLocalization.T("devtools.versions.testNotify"), buttonStyle, GUILayout.Width(120)))
                TestUpdateNotification();

            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            foreach (var mod in KnownSuiteMods)
            {
                GUILayout.BeginHorizontal();
                string installedVersion = ModHealthResolver.ResolveInstalledVersion(mod.guid);
                string versionLabel = installedVersion != null
                    ? $"{mod.name} v{installedVersion}"
                    : $"{mod.name} ({ModLocalization.T("devtools.versions.notInstalled")})";
                GUILayout.Label(versionLabel, labelStyle, GUILayout.Width(220));

                if (_versionResults.TryGetValue(mod.guid, out var result))
                {
                    if (!result.Success)
                    {
                        var errorStyle = new GUIStyle(labelStyle) { normal = { textColor = new Color(1f, 0.5f, 0.5f) } };
                        GUILayout.Label($"Error: {result.ErrorMessage}", errorStyle);
                    }
                    else if (result.UpdateAvailable)
                    {
                        var updateStyle = new GUIStyle(labelStyle) { normal = { textColor = new Color(1f, 0.9f, 0.3f) } };
                        GUILayout.Label(ModLocalization.T("devtools.versions.update", result.LatestVersion), updateStyle);
                    }
                    else
                    {
                        var okStyle = new GUIStyle(labelStyle) { normal = { textColor = new Color(0.5f, 1f, 0.5f) } };
                        GUILayout.Label(ModLocalization.T("devtools.versions.upToDate"), okStyle);
                    }
                }
                else
                {
                    GUILayout.Label(ModLocalization.T("devtools.versions.notChecked"), labelStyle);
                }

                GUILayout.EndHorizontal();
            }
        }

        public static void RefreshRows(bool force = false)
        {
            if (!force && DateTime.UtcNow - _lastRefreshUtc < TimeSpan.FromSeconds(2))
                return;

            _cachedRows = ModHealthAggregator.CollectMergedRows(
                ModHealthResolver.ResolveDisplayName,
                ModHealthResolver.ResolveInstalledVersion);
            _cachedSkew = ModHealthAggregator.AnalyzeSharedCodeSkew(_cachedRows);
            _lastRefreshUtc = DateTime.UtcNow;
        }

        private static void CheckAllModVersions()
        {
            _isCheckingVersions = true;
            _versionResults.Clear();
            int pendingChecks = KnownSuiteMods.Length;

            foreach (var mod in KnownSuiteMods)
            {
                string installedVersion = ModHealthResolver.ResolveInstalledVersion(mod.guid);
                if (installedVersion == null)
                {
                    pendingChecks--;
                    if (pendingChecks <= 0)
                        _isCheckingVersions = false;
                    continue;
                }

                VersionChecker.CheckForUpdate(mod.guid, installedVersion, Plugin.Log, result =>
                {
                    _versionResults[mod.guid] = result;
                    pendingChecks--;
                    if (pendingChecks <= 0)
                        _isCheckingVersions = false;

                    RefreshRows(force: true);
                });
            }
        }

        private static void TestUpdateNotification()
        {
            var fakeResult = new VersionChecker.VersionCheckResult
            {
                Success = true,
                UpdateAvailable = true,
                ModName = PluginInfo.PLUGIN_NAME,
                CurrentVersion = "1.0.0",
                LatestVersion = "9.9.9",
                NexusUrl = "https://example.com"
            };
            fakeResult.NotifyUpdateAvailable(Plugin.Log);
        }

        private static void CopyToClipboard(string text)
        {
            try
            {
                GUIUtility.systemCopyBuffer = text ?? string.Empty;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[Health] Clipboard copy failed: {ex.Message}");
            }
        }
    }
}
