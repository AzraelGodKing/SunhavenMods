using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using HavensMirror.Config;
using HavensMirror.Dialogue;
using HavensMirror.Gallery;
using HavensMirror.Hotkeys;
using HavensMirror.Look;
using SunhavenMods.Shared;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HavensMirror
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log { get; private set; }
        public static SpriteShelf Shelf { get; private set; }

        private static Harmony _harmony;
        private static MirrorPersistentRunner _runner;
        private bool _applicationQuitting;

        private void Awake()
        {
            Log = Logger;
            ConfigFile configFile = CreateNamedConfig();
            ConfigFileHelper.ReplacePluginConfig(this, configFile, Log.LogWarning);
            MirrorOptions.Bind(configFile);

            Shelf = new SpriteShelf();

            if (MirrorOptions.Enabled == null || !MirrorOptions.Enabled.Value)
            {
                Log.LogInfo($"{PluginInfo.PLUGIN_NAME} disabled in config.");
                ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                    "standalone",
                    mode: "disabled");
                return;
            }

            GalleryPaths.EnsureStarterLayout(msg => Log.LogInfo(msg));
            // Creates empty gallery/<CharacterName>/ for each save slot when GameSave is already up.
            int created = SaveSlotFolderSync.EnsureCharacterFolders();
            if (created > 0)
                Log.LogInfo($"[Gallery] Created {created} character folder(s) from saves.");

            _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
            try
            {
                DialogueMirrorBinder.Apply(_harmony);
                NpcLookPatches.Apply(_harmony);
            }
            catch (Exception ex)
            {
                Log.LogError($"Harmony setup failed (game version drift?): {ex}");
            }

            // DDOL runner owns folder sync + reload hotkey so early BepInEx OnDestroy
            // (empty scene) does not kill Harmony patches or gallery sync.
            if (_runner == null)
                _runner = PersistentRunnerBase.CreateRunner<MirrorPersistentRunner>();

            ReloadGallery(notify: false);

            VersionChecker.CheckForUpdate(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_VERSION, Log,
                onComplete: result => result.NotifyUpdateAvailable(Log));

            Log.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded. Gallery: {GalleryPaths.GalleryRoot}");
            ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                ModHealthIntegrationSummary.Build(("DevTools", SuitePluginGuids.DevTools)),
                mode: "startup");
        }

        public static void ReloadGallery(bool notify)
        {
            if (Shelf == null)
                Shelf = new SpriteShelf();

            // Re-scan saves on reload so newly created characters get folders without restart.
            SaveSlotFolderSync.EnsureCharacterFolders();

            string character = TryReadCharacterName();
            string status = Shelf.LoadForCharacter(character);

            if (Shelf.HasAny)
                Log?.LogInfo($"[Gallery] {status}");
            else
                Log?.LogDebug($"[Gallery] {status}");

            if (notify)
                TryNotify($"[Haven's Mirror] {status}");
        }

        /// <summary>
        /// Reload when the shelf was filled for another character (title screen before a save was
        /// picked, or a save switch), so gallery/&lt;CharacterName&gt;/ is used as soon as it is known.
        /// </summary>
        public static void EnsureGalleryCurrent()
        {
            if (Shelf == null)
            {
                ReloadGallery(notify: false);
                return;
            }

            string character = TryReadCharacterName();
            // A failed name read mid-game must not throw away an already loaded character folder.
            if (string.IsNullOrWhiteSpace(character) || !Shelf.IsStaleFor(character))
                return;

            Log?.LogInfo($"[Gallery] Active character is now '{character ?? "(none)"}' — reloading portraits.");
            ReloadGallery(notify: false);
        }

        private static string TryReadCharacterName()
        {
            return GameSaveCharacterName.TryGetCurrent(
                fallback: null,
                logWarning: msg => Log?.LogDebug($"[Gallery] Character name resolve failed: {msg}"));
        }

        private static void TryNotify(string message)
        {
            try
            {
                Type stackType = ReflectionHelper.FindWishType("NotificationStack");
                object stack = stackType != null ? ReflectionHelper.GetSingletonInstance(stackType) : null;
                if (stack == null)
                    return;

                // Wish.NotificationStack.SendNotification(string text, int id, int amount, bool unique, bool error)
                MethodInfo send = AccessTools.Method(
                                      stackType,
                                      "SendNotification",
                                      new[] { typeof(string), typeof(int), typeof(int), typeof(bool), typeof(bool) })
                                  ?? AccessTools.Method(stackType, "SendNotification");
                if (send == null)
                    return;

                var parms = send.GetParameters();
                if (parms.Length == 1 && parms[0].ParameterType == typeof(string))
                {
                    send.Invoke(stack, new object[] { message });
                    return;
                }

                object[] args = new object[parms.Length];
                args[0] = message;
                for (int i = 1; i < parms.Length; i++)
                {
                    Type pt = parms[i].ParameterType;
                    if (pt == typeof(int))
                        args[i] = 0;
                    else if (pt == typeof(bool))
                        args[i] = false; // unique=false, error=false
                    else if (parms[i].HasDefaultValue)
                        args[i] = parms[i].DefaultValue;
                    else
                        args[i] = GetDefault(pt);
                }

                send.Invoke(stack, args);
            }
            catch (Exception ex)
            {
                Log?.LogDebug($"[Notify] {ex.Message}");
            }
        }

        private static object GetDefault(Type type)
        {
            if (type == typeof(bool)) return false;
            if (type == typeof(int)) return 0;
            if (type == typeof(float)) return 0f;
            if (type.IsValueType) return Activator.CreateInstance(type);
            return null;
        }

        private static ConfigFile CreateNamedConfig()
        {
            return ConfigFileHelper.CreateNamedConfig(PluginInfo.PLUGIN_GUID, "HavensMirror.cfg", msg => Log?.LogWarning(msg));
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void OnDestroy()
        {
            string sceneName = SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying
                                    || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
            {
                Log?.LogInfo($"[Lifecycle] Plugin OnDestroy during expected teardown (scene: {sceneName})");
                Shelf?.Dispose();
                Shelf = null;
                _harmony?.UnpatchSelf();
                _harmony = null;
            }
            else
            {
                // Early BepInEx empty-scene OnDestroy: keep Harmony + Shelf + PersistentRunner alive
                // so dialogue busts and gallery sync continue working (Suite pattern / AZR-346).
                Log?.LogWarning($"[Lifecycle] Plugin OnDestroy outside expected teardown (scene: {sceneName}) — keeping Harmony patches, Shelf, and MirrorPersistentRunner");
            }
        }
    }
}
