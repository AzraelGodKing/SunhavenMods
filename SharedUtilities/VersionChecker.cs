#pragma warning disable CS0436 // Type conflicts with imported type - this is our VersionChecker/ReflectionHelper
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Logging;
using UnityEngine;

namespace SunhavenMods.Shared
{
    /// <summary>
    /// Checks for mod updates from docs/versions.json on the main branch.
    /// Each mod can call CheckForUpdate() on startup to notify players of new versions.
    /// Every mod links its own copy of this file, so the download is shared through AppDomain data:
    /// one request per launch, however many mods ask.
    /// </summary>
    public static class VersionChecker
    {
        private const string VersionsUrl = "https://raw.githubusercontent.com/AzraelGodKing/SunhavenMods/main/docs/versions.json";

        private const string SharedStateKey = "SunhavenMods.VersionChecker.State";
        private const string SharedJsonKey = "SunhavenMods.VersionChecker.Json";
        private static readonly TimeSpan SharedFetchTimeout = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan SharedSuccessTtl = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan SharedFailureTtl = TimeSpan.FromMinutes(1);

        private static readonly Dictionary<string, ModHealthSnapshot> HealthByPluginGuid = new Dictionary<string, ModHealthSnapshot>(StringComparer.OrdinalIgnoreCase);
        private static readonly object HealthLock = new object();

        /// <summary>
        /// Result of a version check operation.
        /// </summary>
        public class VersionCheckResult
        {
            public bool Success { get; set; }
            public bool UpdateAvailable { get; set; }
            public string CurrentVersion { get; set; }
            public string LatestVersion { get; set; }
            public string ModName { get; set; }
            public string NexusUrl { get; set; }
            public string Changelog { get; set; }
            public string ErrorMessage { get; set; }
        }

        public class ModHealthSnapshot
        {
            public string PluginGuid { get; set; }
            public DateTime LastCheckUtc { get; set; }
            public int ExceptionCount { get; set; }
            public string LastError { get; set; }
        }

        /// <summary>
        /// Checks for updates for a specific mod. Call this from your plugin's Awake or Start.
        /// </summary>
        /// <param name="pluginGuid">The PLUGIN_GUID of your mod</param>
        /// <param name="currentVersion">The current PLUGIN_VERSION</param>
        /// <param name="logger">Optional logger for debug output</param>
        /// <param name="onComplete">Callback when check completes</param>
        public static void CheckForUpdate(string pluginGuid, string currentVersion, ManualLogSource logger = null, Action<VersionCheckResult> onComplete = null)
        {
            TouchHealth(pluginGuid);

            // Use a MonoBehaviour to run the coroutine
            var runner = new GameObject("VersionChecker").AddComponent<VersionCheckRunner>();
            UnityEngine.Object.DontDestroyOnLoad(runner.gameObject);
            SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(runner.gameObject);
            runner.StartCheck(pluginGuid, currentVersion, logger, onComplete);
        }

        /// <summary>
        /// Compares the numeric core of two versions (a leading v and any -prerelease / +build suffix are ignored).
        /// Returns null when either version is missing or has a non-numeric segment.
        /// </summary>
        public static int? TryCompareVersions(string v1, string v2)
        {
            if (!TryParseVersionCore(v1, out var parts1) || !TryParseVersionCore(v2, out var parts2))
                return null;

            int maxLength = Math.Max(parts1.Length, parts2.Length);
            for (int i = 0; i < maxLength; i++)
            {
                int num1 = i < parts1.Length ? parts1[i] : 0;
                int num2 = i < parts2.Length ? parts2[i] : 0;
                if (num1 < num2) return -1;
                if (num1 > num2) return 1;
            }
            return 0;
        }

        private static bool TryParseVersionCore(string version, out int[] parts)
        {
            parts = null;
            if (string.IsNullOrWhiteSpace(version))
                return false;

            string core = version.Trim().TrimStart('v', 'V');
            int suffix = core.IndexOfAny(new[] { '-', '+' });
            if (suffix >= 0) core = core.Substring(0, suffix);

            string[] segments = core.Split('.');
            var parsed = new int[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                if (!int.TryParse(segments[i], NumberStyles.None, CultureInfo.InvariantCulture, out parsed[i]))
                    return false;
            }
            parts = parsed;
            return true;
        }

        /// <summary>
        /// Reads one mod's entry from a versions.json document into <paramref name="result"/>.
        /// Returns null on success, otherwise the reason the check could not complete.
        /// </summary>
        internal static string EvaluateManifest(string json, string pluginGuid, string currentVersion, VersionCheckResult result)
        {
            Dictionary<string, object> root;
            try
            {
                root = MinimalJsonParser.Parse((json ?? string.Empty).TrimStart('\uFEFF')) as Dictionary<string, object>;
            }
            catch (JsonParseException ex)
            {
                return $"versions.json is not valid JSON ({ex.Message})";
            }
            if (root == null)
                return "versions.json root is not an object";

            if (string.IsNullOrEmpty(pluginGuid) ||
                !root.TryGetValue(pluginGuid, out object entryValue) ||
                !(entryValue is Dictionary<string, object> entry))
                return $"Mod '{pluginGuid}' not found in versions.json";

            result.LatestVersion = ManifestField(entry, "version");
            result.ModName = ManifestField(entry, "name");
            result.NexusUrl = ManifestField(entry, "nexus");
            result.Changelog = ManifestField(entry, "changelog");

            if (string.IsNullOrEmpty(result.LatestVersion))
                return "Could not parse version from response";

            int? comparison = TryCompareVersions(currentVersion, result.LatestVersion);
            if (comparison == null)
                return $"Could not compare installed version '{currentVersion}' with '{result.LatestVersion}'";

            result.UpdateAvailable = comparison.Value < 0;
            return null;
        }

        private static string ManifestField(Dictionary<string, object> entry, string key)
        {
            if (!entry.TryGetValue(key, out object value) || value == null)
                return null;
            return value as string ?? Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static void TouchHealth(string pluginGuid)
        {
            if (string.IsNullOrWhiteSpace(pluginGuid))
                return;
            lock (HealthLock)
            {
                if (!HealthByPluginGuid.TryGetValue(pluginGuid, out var snapshot))
                {
                    snapshot = new ModHealthSnapshot { PluginGuid = pluginGuid };
                    HealthByPluginGuid[pluginGuid] = snapshot;
                }
                snapshot.LastCheckUtc = DateTime.UtcNow;
            }
        }

        private static void RecordHealthError(string pluginGuid, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(pluginGuid))
                return;
            lock (HealthLock)
            {
                if (!HealthByPluginGuid.TryGetValue(pluginGuid, out var snapshot))
                {
                    snapshot = new ModHealthSnapshot { PluginGuid = pluginGuid };
                    HealthByPluginGuid[pluginGuid] = snapshot;
                }
                snapshot.LastCheckUtc = DateTime.UtcNow;
                snapshot.ExceptionCount++;
                snapshot.LastError = errorMessage;
            }
        }

        /// <summary>
        /// Helper MonoBehaviour to run the version check coroutine.
        /// </summary>
        private class VersionCheckRunner : MonoBehaviour
        {
            private ManualLogSource _pluginLog;

            public void StartCheck(string pluginGuid, string currentVersion, ManualLogSource pluginLog, Action<VersionCheckResult> onComplete)
            {
                _pluginLog = pluginLog;
                StartCoroutine(CheckVersionCoroutine(pluginGuid, currentVersion, onComplete));
            }

            private void LogInfo(string message) => _pluginLog?.LogInfo($"[VersionChecker] {message}");
            private void LogWarningMsg(string message) => _pluginLog?.LogWarning($"[VersionChecker] {message}");
            private void LogErrorMsg(string message) => _pluginLog?.LogError($"[VersionChecker] {message}");

            private IEnumerator CheckVersionCoroutine(string pluginGuid, string currentVersion, Action<VersionCheckResult> onComplete)
            {
                var result = new VersionCheckResult
                {
                    CurrentVersion = currentVersion
                };

                string json = null;
                string fetchError = null;
                while (true)
                {
                    var state = SharedFetchState.Read();
                    TimeSpan age = DateTime.UtcNow - state.AtUtc;
                    if (state.Kind == SharedFetchState.Done && age < SharedSuccessTtl &&
                        AppDomain.CurrentDomain.GetData(SharedJsonKey) is string cached)
                    {
                        json = cached;
                        break;
                    }
                    if (state.Kind == SharedFetchState.Failed && age < SharedFailureTtl)
                    {
                        fetchError = state.Message;
                        break;
                    }
                    if (state.Kind == SharedFetchState.Fetching && age < SharedFetchTimeout)
                    {
                        yield return null;
                        continue;
                    }

                    SharedFetchState.Write(SharedFetchState.Fetching, null);
                    using (var www = UnityEngine.Networking.UnityWebRequest.Get(VersionsUrl))
                    {
                        www.timeout = 10;
                        yield return www.SendWebRequest();

                        if (www.result == UnityEngine.Networking.UnityWebRequest.Result.ConnectionError ||
                            www.result == UnityEngine.Networking.UnityWebRequest.Result.ProtocolError)
                        {
                            fetchError = $"Network error: {www.error}";
                            SharedFetchState.Write(SharedFetchState.Failed, fetchError);
                        }
                        else
                        {
                            json = www.downloadHandler.text;
                            AppDomain.CurrentDomain.SetData(SharedJsonKey, json);
                            SharedFetchState.Write(SharedFetchState.Done, null);
                        }
                    }
                    break;
                }

                string error = fetchError;
                if (error == null)
                {
                    try
                    {
                        error = EvaluateManifest(json, pluginGuid, currentVersion, result);
                    }
                    catch (Exception ex)
                    {
                        error = $"Parse error: {ex.Message}";
                    }
                }

                if (error != null)
                {
                    result.Success = false;
                    result.UpdateAvailable = false;
                    result.ErrorMessage = error;
                    RecordHealthError(pluginGuid, error);
                    LogWarningMsg(error);
                }
                else
                {
                    result.Success = true;
                    if (result.UpdateAvailable)
                        LogInfo($"Update available for {result.ModName}: {currentVersion} -> {result.LatestVersion}");
                    else
                        LogInfo($"{result.ModName} is up to date (v{currentVersion})");
                }

                onComplete?.Invoke(result);
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Cross-assembly fetch state in AppDomain data, formatted as <c>kind|utcTicks|message</c>.
        /// Strings only, so every mod's copy of this class can read what another copy wrote.
        /// </summary>
        private struct SharedFetchState
        {
            public const string Fetching = "fetching";
            public const string Done = "done";
            public const string Failed = "failed";

            public string Kind;
            public DateTime AtUtc;
            public string Message;

            public static SharedFetchState Read()
            {
                var state = new SharedFetchState { AtUtc = DateTime.MinValue };
                if (!(AppDomain.CurrentDomain.GetData(SharedStateKey) is string raw))
                    return state;
                string[] parts = raw.Split(new[] { '|' }, 3);
                if (parts.Length < 2 || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long ticks))
                    return state;
                state.Kind = parts[0];
                state.AtUtc = new DateTime(ticks, DateTimeKind.Utc);
                state.Message = parts.Length > 2 ? parts[2] : null;
                return state;
            }

            public static void Write(string kind, string message)
            {
                string ticks = DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
                AppDomain.CurrentDomain.SetData(SharedStateKey, kind + "|" + ticks + "|" + (message ?? string.Empty));
            }
        }
    }

    /// <summary>
    /// Extension methods for easy integration with BepInEx plugins.
    /// </summary>
    public static class VersionCheckerExtensions
    {
        /// <summary>
        /// Sends an in-game notification about an available update.
        /// Uses the native Sun Haven notification system if available.
        /// </summary>
        public static void NotifyUpdateAvailable(this SunhavenMods.Shared.VersionChecker.VersionCheckResult result, ManualLogSource logger = null)
        {
            if (!result.UpdateAvailable)
                return;

            var message = $"{result.ModName} update available: v{result.LatestVersion}";

            // Try to use native notification system
            try
            {
                var notificationStackType = SunhavenMods.Shared.ReflectionHelper.FindWishType("NotificationStack");
                if (notificationStackType != null)
                {
                    // Use reflection to get SingletonBehaviour<NotificationStack>.Instance
                    var singletonBaseType = SunhavenMods.Shared.ReflectionHelper.FindType("SingletonBehaviour`1", "Wish");
                    if (singletonBaseType != null)
                    {
                        var singletonType = singletonBaseType.MakeGenericType(notificationStackType);
                        var instanceProp = singletonType.GetProperty("Instance");
                        var instance = instanceProp?.GetValue(null);

                        if (instance != null)
                        {
                            var sendMethod = notificationStackType.GetMethod("SendNotification",
                                new[] { typeof(string), typeof(int), typeof(int), typeof(bool), typeof(bool) });

                            if (sendMethod != null)
                            {
                                // SendNotification(message, itemId, amount, showInChat, playSound)
                                sendMethod.Invoke(instance, new object[] { message, 0, 1, false, true });
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning($"Failed to send native notification: {ex.Message}");
            }

            // Fallback to log message
            logger?.LogWarning($"[UPDATE AVAILABLE] {message}");
            if (!string.IsNullOrEmpty(result.NexusUrl))
            {
                logger?.LogWarning($"Download at: {result.NexusUrl}");
            }
        }
    }
}
