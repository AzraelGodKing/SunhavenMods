using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HavensBirthright.Abilities;
using HarmonyLib;
using SunhavenMods.Shared;
using System;
using System.Linq;
using UnityEngine;

namespace HavensBirthright
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }
        public static ConfigFile ConfigFile { get; private set; }

        // Static config value for BirthrightRunner hotkey detection
        internal static UnityEngine.KeyCode StaticAbilityToggleKey = UnityEngine.KeyCode.F9;
        internal static UnityEngine.KeyCode StaticReloadConfigKey = UnityEngine.KeyCode.F12;

        private Harmony _harmony;
        private RacialBonusManager _racialBonusManager;
        private ConfigEntry<bool> _checkForUpdates;
        private ConfigEntry<UnityEngine.KeyCode> _reloadConfigKey;
        private BirthrightRunner _runner;
        private EventHandler _updateKeybindsHandler;
        private EventHandler _rebuildCrossRaceRulesHandler;
        private bool _applicationQuitting;

        /// <summary>
        /// Set when an essential Harmony patch (player init, GetStat, or core combat/mana hooks) failed — racial gameplay patches no-op to avoid half-applied behavior.
        /// </summary>
        public static bool CriticalBirthrightHarmonyIncomplete { get; private set; }

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            ConfigFile = CreateNamedConfig();
            SunhavenMods.Shared.ConfigFileHelper.ReplacePluginConfig(this, ConfigFile, Log.LogWarning);

            Log.LogInfo($"Loading {PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}");

            try
            {
                // Initialize configuration
                RacialConfig.Initialize(ConfigFile);
                AbilityConfig.Initialize(ConfigFile);

                _checkForUpdates = ConfigFile.Bind(
                    "Updates",
                    "CheckForUpdates",
                    true,
                    "Check for mod updates on startup"
                );

                _reloadConfigKey = ConfigFile.Bind(
                    "General",
                    "ReloadConfigKey",
                    UnityEngine.KeyCode.F12,
                    "Key to reload config from file (edit the .cfg file, then press this key in-game to apply)"
                );

                // Cache static keybinds for BirthrightRunner to access
                StaticAbilityToggleKey = AbilityConfig.ActiveAbilityToggleKey.Value;
                StaticReloadConfigKey = _reloadConfigKey.Value;

                SubscribeConfigChanged();

                // Initialize the racial bonus manager
                _racialBonusManager = new RacialBonusManager();

                // Create the persistent runner for ability update loop
                _runner = PersistentRunnerBase.CreateRunner<BirthrightRunner>();
                Log.LogInfo("BirthrightRunner created for active abilities");

                // Apply Harmony patches
                _harmony = new Harmony(PluginInfo.PLUGIN_GUID);

                try
                {
                    var playerType = typeof(Wish.Player);
                    Log.LogInfo($"Player type: {playerType.FullName} from {playerType.Assembly.GetName().Name}");

                    // Patch InitializeAsOwner for race detection
                    PatchMethod(playerType, "InitializeAsOwner",
                        typeof(Patches.PlayerPatches), "OnPlayerInitialized", null, essential: true);

                    // Patch Initialize as backup for race detection
                    PatchMethod(playerType, "Initialize",
                        typeof(Patches.PlayerPatches), "OnPlayerInitialize",
                        Type.EmptyTypes, essential: true);

                    // LoadCharacter — reset cached race/actives when switching save/slot (name alone is not unique)
                    var gameSaveType = AccessTools.TypeByName("Wish.GameSave");
                    if (gameSaveType != null)
                    {
                        var loadCharMethod = AccessTools.Method(gameSaveType, "LoadCharacter", new[] { typeof(int) });
                        if (loadCharMethod != null)
                        {
                            _harmony.Patch(loadCharMethod,
                                postfix: new HarmonyMethod(typeof(Patches.PlayerPatches), nameof(Patches.PlayerPatches.OnGameSaveLoadCharacter)));
                            Log.LogInfo("Patched GameSave.LoadCharacter (race/actives reset on character load)");
                        }
                        else
                            Log.LogWarning("Could not find GameSave.LoadCharacter(int) — character-switch reset may be incomplete");
                    }

                    // Patch GetStat for stat bonuses (combat, skills, regen, abilities, drawbacks, synergies)
                    PatchMethod(playerType, "GetStat",
                        typeof(Patches.StatPatches), "ModifyGetStat",
                        new[] { typeof(Wish.StatType) }, essential: true);

                    // Patch ReceiveDamage for defense + combat abilities (prefix)
                    PatchMethodPrefix(playerType, "ReceiveDamage",
                        typeof(Patches.CombatPatches), "ModifyDamageReceived", null, essential: true);

                    // Patch ReceiveDamage for dodge detection + Divine Ward trigger (postfix)
                    PatchMethod(playerType, "ReceiveDamage",
                        typeof(Patches.CombatPatches), "OnDamageReceivedPostfix",
                        parameters: null, essential: true);

                    // Patch NPCAI.AddRelationship (game API: float increase, float romanceBonus, bool showUI)
                    var npcaiType = AccessTools.TypeByName("Wish.NPCAI");
                    if (npcaiType != null)
                    {
                        var addRelMethod = AccessTools.Method(npcaiType, "AddRelationship", new[] { typeof(float), typeof(float), typeof(bool) });
                        if (addRelMethod != null)
                        {
                            var prefix = AccessTools.Method(typeof(Patches.EconomyPatches), "ModifyRelationshipGain");
                            _harmony.Patch(addRelMethod, prefix: new HarmonyLib.HarmonyMethod(prefix));
                            Log.LogInfo("Successfully patched NPCAI.AddRelationship");
                        }
                        else
                        {
                            Log.LogWarning("Could not find NPCAI.AddRelationship - relationship bonuses will not work");
                        }
                    }
                    else
                    {
                        Log.LogWarning("Could not find NPCAI type - relationship bonuses will not work");
                    }

                    // Patch Wish.Shop.BuyItem for shop discounts (game uses Shop, not ShopMenu)
                    PatchShopBuyItemForDiscount();

                    // Patch Player.AddMana to block mana regen while Infernal Forge is active
                    PatchMethodPrefix(playerType, "AddMana",
                        typeof(Patches.AbilityPatches), "OnPlayerAddManaPrefix", null, essential: true);

                    // NOTE: Infernal Forge no longer uses a Harmony prefix on Inventory.AddItem.
                    // It now uses a periodic inventory scan in BirthrightRunner.UpdateInfernalForge()
                    // because ore is picked up 1 at a time, making per-pickup smelting impossible.

                    // Demon-only: Soul Harvest — bonus gold and HP cost on enemy kill
                    var enemyAIType = AccessTools.TypeByName("Wish.EnemyAI");
                    if (enemyAIType != null)
                    {
                        var dieMethod = AccessTools.Method(enemyAIType, "Die", new[] { typeof(bool) });
                        if (dieMethod != null)
                        {
                            var soulHarvestPostfix = AccessTools.Method(typeof(Patches.SoulHarvestPatches), "OnEnemyDiePostfix");
                            if (soulHarvestPostfix != null)
                            {
                                _harmony.Patch(dieMethod, postfix: new HarmonyMethod(soulHarvestPostfix));
                                Log.LogInfo("Successfully patched EnemyAI.Die (Soul Harvest)");
                            }
                        }
                    }

                    // Log results
                    var patchedMethods = _harmony.GetPatchedMethods();
                    int count = 0;
                    foreach (var method in patchedMethods)
                    {
                        Log.LogInfo($"Patched: {method.DeclaringType?.Name}.{method.Name}");
                        count++;
                    }
                    Log.LogInfo($"Total methods patched: {count}");
                    if (CriticalBirthrightHarmonyIncomplete)
                    {
                        Log.LogError(
                            "[Haven's Birthright] One or more essential Harmony patches failed — racial stat/combat hooks are disabled for this session. Check the log above for 'Could not find method' / patch errors.");
                    }
                }
                catch (Exception patchEx)
                {
                    Log.LogError($"Harmony patching failed: {patchEx}");
                    CriticalBirthrightHarmonyIncomplete = true;
                }

                // Check for updates
                if (_checkForUpdates.Value)
                {
                    VersionChecker.CheckForUpdate(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_VERSION, Log,
                        result => result.NotifyUpdateAvailable(Log));
                }

                Log.LogInfo($"{PluginInfo.PLUGIN_NAME} loaded successfully!");
                ModDiagnostics.LogModStartup(Log, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION,
                    ModHealthIntegrationSummary.Build(
                        ("DevTools", SuitePluginGuids.DevTools),
                        ("FasterRaces", SuitePluginGuids.FasterRaces)));
                Log.LogInfo($"Active abilities: {(AbilityConfig.EnableActiveAbilities.Value ? "ENABLED" : "DISABLED")}");
                Log.LogInfo($"Racial drawbacks: {(AbilityConfig.EnableRacialDrawbacks.Value ? "ENABLED" : "DISABLED")}");
                Log.LogInfo($"Conditional synergies: {(AbilityConfig.EnableConditionalSynergies.Value ? "ENABLED" : "DISABLED")}");
                Log.LogInfo($"Ability toggle key: {StaticAbilityToggleKey}");
                Log.LogInfo($"Config reload key: {StaticReloadConfigKey} (edit .cfg then press in-game to apply)");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to load {PluginInfo.PLUGIN_NAME}: {ex}");
            }
        }

        /// <summary>
        /// Subscribe to config changes so in-game editors (e.g. BepInEx ConfigurationManager) update our caches immediately.
        /// </summary>
        private void SubscribeConfigChanged()
        {
            _updateKeybindsHandler = (_, _) =>
            {
                StaticAbilityToggleKey = AbilityConfig.ActiveAbilityToggleKey.Value;
                StaticReloadConfigKey = _reloadConfigKey.Value;
            };
            AbilityConfig.ActiveAbilityToggleKey.SettingChanged += _updateKeybindsHandler;
            _reloadConfigKey.SettingChanged += _updateKeybindsHandler;

            _rebuildCrossRaceRulesHandler = (_, _) =>
            {
                BonusTransferRules.RebuildFromConfig(
                    RacialConfig.CrossRaceBonusRules != null ? RacialConfig.CrossRaceBonusRules.Value : "");
            };

            RacialConfig.CrossRaceBonusRules.SettingChanged += _rebuildCrossRaceRulesHandler;
            RacialConfig.EnableBonusTransfers.SettingChanged += _rebuildCrossRaceRulesHandler;
        }

        /// <summary>
        /// Reload config from disk and re-apply (racial bonuses, ability toggles, keybinds).
        /// Call after the player edits the config file, or from the reload keybind.
        /// </summary>
        public void ReloadConfig()
        {
            try
            {
                ConfigFile.Reload();
                RacialConfig.Initialize(ConfigFile);
                AbilityConfig.Initialize(ConfigFile);
                StaticAbilityToggleKey = AbilityConfig.ActiveAbilityToggleKey.Value;
                StaticReloadConfigKey = _reloadConfigKey.Value;
                _racialBonusManager = new RacialBonusManager();
                Log?.LogInfo("[Haven's Birthright] Config reloaded from file");
            }
            catch (Exception ex)
            {
                Log?.LogError($"[Haven's Birthright] Config reload failed: {ex.Message}");
            }
        }

        private static ConfigFile CreateNamedConfig()
        {
            return ConfigFileHelper.CreateNamedConfig(
                PluginInfo.PLUGIN_GUID,
                "HavensBirthright.cfg",
                message => Log?.LogWarning(message)
            );
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void OnDestroy()
        {
            try
            {
                if (_updateKeybindsHandler != null)
                {
                    AbilityConfig.ActiveAbilityToggleKey.SettingChanged -= _updateKeybindsHandler;
                    _reloadConfigKey.SettingChanged -= _updateKeybindsHandler;
                }
                if (_rebuildCrossRaceRulesHandler != null)
                {
                    RacialConfig.CrossRaceBonusRules.SettingChanged -= _rebuildCrossRaceRulesHandler;
                    RacialConfig.EnableBonusTransfers.SettingChanged -= _rebuildCrossRaceRulesHandler;
                }
            }
            catch (Exception ex)
            {
                Log?.LogDebug($"[Lifecycle] Config handler teardown encountered an issue: {ex.Message}");
            }

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name ?? string.Empty;
            string sceneLower = sceneName.ToLowerInvariant();
            bool expectedTeardown = _applicationQuitting || !Application.isPlaying || sceneLower.Contains("menu") || sceneLower.Contains("title");
            if (expectedTeardown)
                Log?.LogInfo($"[Lifecycle] Plugin OnDestroy during expected teardown (scene: {sceneName})");
            else
                Log?.LogWarning($"[Lifecycle] Plugin OnDestroy outside expected teardown (scene: {sceneName})");

            _harmony?.UnpatchSelf();
        }

        /// <summary>
        /// Helper method to manually patch a method with a postfix
        /// </summary>
        private void PatchMethod(Type targetType, string methodName, Type patchType, string patchMethodName, Type[] parameters = null, bool essential = false)
        {
            try
            {
                var original = parameters == null
                    ? AccessTools.Method(targetType, methodName)
                    : AccessTools.Method(targetType, methodName, parameters);

                if (original == null)
                {
                    Log.LogWarning($"Could not find method {targetType.Name}.{methodName}");
                    if (essential)
                        CriticalBirthrightHarmonyIncomplete = true;
                    return;
                }

                var postfix = AccessTools.Method(patchType, patchMethodName);
                if (postfix == null)
                {
                    Log.LogWarning($"Could not find patch method {patchType.Name}.{patchMethodName}");
                    if (essential)
                        CriticalBirthrightHarmonyIncomplete = true;
                    return;
                }

                _harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                Log.LogInfo($"Successfully patched {targetType.Name}.{methodName}");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to patch {targetType.Name}.{methodName}: {ex.Message}");
                if (essential)
                    CriticalBirthrightHarmonyIncomplete = true;
            }
        }

        /// <summary>
        /// Helper method to manually patch a method with a prefix
        /// </summary>
        private void PatchMethodPrefix(Type targetType, string methodName, Type patchType, string patchMethodName, Type[] parameters = null, bool essential = false)
        {
            try
            {
                var original = parameters == null
                    ? AccessTools.Method(targetType, methodName)
                    : AccessTools.Method(targetType, methodName, parameters);

                if (original == null)
                {
                    Log.LogWarning($"Could not find method {targetType.Name}.{methodName}");
                    if (essential)
                        CriticalBirthrightHarmonyIncomplete = true;
                    return;
                }

                var prefix = AccessTools.Method(patchType, patchMethodName);
                if (prefix == null)
                {
                    Log.LogWarning($"Could not find patch method {patchType.Name}.{patchMethodName}");
                    if (essential)
                        CriticalBirthrightHarmonyIncomplete = true;
                    return;
                }

                _harmony.Patch(original, prefix: new HarmonyMethod(prefix));
                Log.LogInfo($"Successfully patched {targetType.Name}.{methodName} (prefix)");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to patch {targetType.Name}.{methodName}: {ex.Message}");
                if (essential)
                    CriticalBirthrightHarmonyIncomplete = true;
            }
        }

        /// <summary>
        /// Helper method to manually patch a method with a postfix
        /// </summary>
        private void PatchMethodPostfix(Type targetType, string methodName, Type patchType, string patchMethodName, Type[] parameters = null)
        {
            try
            {
                var original = parameters == null
                    ? AccessTools.Method(targetType, methodName)
                    : AccessTools.Method(targetType, methodName, parameters);

                if (original == null)
                {
                    Log.LogWarning($"Could not find method {targetType.Name}.{methodName}");
                    return;
                }

                var postfix = AccessTools.Method(patchType, patchMethodName);
                if (postfix == null)
                {
                    Log.LogWarning($"Could not find patch method {patchType.Name}.{patchMethodName}");
                    return;
                }

                _harmony.Patch(original, postfix: new HarmonyMethod(postfix));
                Log.LogInfo($"Successfully patched {targetType.Name}.{methodName} (postfix)");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to patch {targetType.Name}.{methodName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to manually patch a method with a finalizer
        /// </summary>
        private void PatchMethodFinalizer(Type targetType, string methodName, Type patchType, string patchMethodName, Type[] parameters = null)
        {
            try
            {
                var original = parameters == null
                    ? AccessTools.Method(targetType, methodName)
                    : AccessTools.Method(targetType, methodName, parameters);

                if (original == null)
                {
                    Log.LogWarning($"Could not find method {targetType.Name}.{methodName}");
                    return;
                }

                var finalizer = AccessTools.Method(patchType, patchMethodName);
                if (finalizer == null)
                {
                    Log.LogWarning($"Could not find patch method {patchType.Name}.{patchMethodName}");
                    return;
                }

                _harmony.Patch(original, finalizer: new HarmonyMethod(finalizer));
                Log.LogInfo($"Successfully patched {targetType.Name}.{methodName} (finalizer)");
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to patch {targetType.Name}.{methodName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Patch Wish.Shop.BuyItem so shop discount is applied (game uses Shop, not ShopMenu).
        /// </summary>
        private void PatchShopBuyItemForDiscount()
        {
            try
            {
                var shopType = AccessTools.TypeByName("Wish.Shop");
                if (shopType == null)
                {
                    Log.LogWarning("Could not find Wish.Shop type - shop discounts will not work");
                    return;
                }
                var shopItemInfo2Type = AccessTools.TypeByName("Wish.ShopItemInfo2");
                var shopLoot2Type = AccessTools.TypeByName("Wish.ShopLoot2");
                if (shopItemInfo2Type != null)
                {
                    var buyItemMethod = AccessTools.Method(shopType, "BuyItem", new[] { shopItemInfo2Type, typeof(int) });
                    if (buyItemMethod != null)
                    {
                        PatchMethodPrefix(shopType, "BuyItem", typeof(Patches.EconomyPatches), "OnBeforeShopBuyItem", new[] { shopItemInfo2Type, typeof(int) });
                        PatchMethodPostfix(shopType, "BuyItem", typeof(Patches.EconomyPatches), "OnAfterShopBuyItem", new[] { shopItemInfo2Type, typeof(int) });
                        PatchMethodFinalizer(shopType, "BuyItem", typeof(Patches.EconomyPatches), "OnBuyItemFinalizer", new[] { shopItemInfo2Type, typeof(int) });
                    }
                }
                if (shopLoot2Type != null)
                {
                    var buyItemMethod = AccessTools.Method(shopType, "BuyItem", new[] { shopLoot2Type, typeof(int) });
                    if (buyItemMethod != null)
                    {
                        PatchMethodPrefix(shopType, "BuyItem", typeof(Patches.EconomyPatches), "OnBeforeShopBuyItem", new[] { shopLoot2Type, typeof(int) });
                        PatchMethodPostfix(shopType, "BuyItem", typeof(Patches.EconomyPatches), "OnAfterShopBuyItem", new[] { shopLoot2Type, typeof(int) });
                        PatchMethodFinalizer(shopType, "BuyItem", typeof(Patches.EconomyPatches), "OnBuyItemFinalizer", new[] { shopLoot2Type, typeof(int) });
                    }
                    var buyItemSingle = AccessTools.Method(shopType, "BuyItem", new[] { shopLoot2Type });
                    if (buyItemSingle != null)
                    {
                        PatchMethodPrefix(shopType, "BuyItem", typeof(Patches.EconomyPatches), "OnBeforeShopBuyItemSingle", new[] { shopLoot2Type });
                        PatchMethodPostfix(shopType, "BuyItem", typeof(Patches.EconomyPatches), "OnAfterShopBuyItemSingle", new[] { shopLoot2Type });
                        PatchMethodFinalizer(shopType, "BuyItem", typeof(Patches.EconomyPatches), "OnBuyItemFinalizer", new[] { shopLoot2Type });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to patch Shop for discount: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the racial bonus manager instance
        /// </summary>
        public static RacialBonusManager GetRacialBonusManager()
        {
            return Instance?._racialBonusManager;
        }

        /// <summary>
        /// Gets the BirthrightRunner instance (for save-load reset).
        /// </summary>
        internal static BirthrightRunner GetRunner()
        {
            return Instance?._runner;
        }

        /// <summary>
        /// Ensures BirthrightRunner exists. Recreates it if destroyed (e.g. by UIHandler.UnloadGame).
        /// Called from PlayerPatches.OnPlayerInitialized() on every game load.
        /// </summary>
        public static void EnsureRunner()
        {
            if (Instance == null) return;

            // Check if runner is null or destroyed (destroyed Unity objects bypass C# null check)
            if (Instance._runner == null || (Instance._runner is UnityEngine.Object obj && obj == null))
            {
                Instance._runner = PersistentRunnerBase.CreateRunner<BirthrightRunner>();
                Log?.LogInfo("[Plugin] BirthrightRunner recreated after destruction");
            }
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.azraelgodking.havensbirthright";
        public const string PLUGIN_NAME = "Haven's Birthright";
        public const string PLUGIN_VERSION = "3.1.4";
    }
}
