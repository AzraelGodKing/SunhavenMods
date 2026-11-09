using HarmonyLib;
using HavensBirthright.Abilities;
using HavensBirthright.Patches;
using HavensBirthright.Session;
using Wish;
using SunhavenMods.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HavensBirthright
{
    /// <summary>
    /// Persistent MonoBehaviour that powers auto-triggered abilities.
    /// Runs every frame to check conditions, manage cooldowns, and track environmental state.
    /// Uses <see cref="StatFrameCache"/> for per-frame values StatPatches reads; <see cref="GameApis"/> for TileManager/inventory/notifications.
    /// </summary>
    public class BirthrightRunner : PersistentRunnerBase
    {
        protected override string RunnerName => "BirthrightRunner";

        private float _outdoorTime = 0f;
        private float _lastTidalBlessingCheck = 0f;
        private float _lastInfernalForgeCheck = 0f;
        private float _lastFontOfLightCheck = 0f;
        private float _lastLoyaltyAuraCheck = 0f;
        private const float LoyaltyAuraUpdateInterval = 2f;

        // One-time diagnostic flag (logs all gate checks on first call after toggle ON)
        private bool _tidalBlessingDiagLogged = false;

        protected override void OnUpdate()
        {
            if (!SceneHelpers.IsInGame())
                return;

            CharacterSessionController.OnUpdateIdentityChecks();

            // Check ability toggle hotkey (runs before config/race checks so user
            // can pre-toggle even when abilities haven't activated yet)
            CheckAbilityToggleHotkey();

            // Reload config from file (e.g. after editing the .cfg)
            if (!TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log) &&
                Plugin.StaticReloadConfigKey != KeyCode.None && UnityEngine.Input.GetKeyDown(Plugin.StaticReloadConfigKey))
            {
                Plugin.Instance?.ReloadConfig();
            }

            if (!RacialConfig.EnableRacialBonuses.Value)
                return;

            var manager = Plugin.GetRacialBonusManager();
            if (manager == null)
                return;

            var race = manager.GetPlayerRace();
            if (!race.HasValue)
            {
                // Race detection may have failed during Harmony patches (Player.Instance not ready yet).
                // Retry here — once it succeeds, _raceDetected locks in and this stops retrying.
                RaceDetectionService.RetryRaceDetection();
                race = manager.GetPlayerRace();
                if (!race.HasValue)
                    return;
            }

            GameApis.EnsureWishCachesInitialized();

            // Update per-frame cache for StatPatches. Run every 4th frame to reduce reflection cost
            // and stutter (e.g. Amari Cat + scythe); values are at most 3 frames stale.
            if (!StatFrameCache.IsCacheValid || (Time.frameCount & 3) == 0)
                StatFrameCache.Update(race.Value, GameApis.DayCycleType);

            // Active abilities require their own config toggle
            if (!AbilityConfig.EnableActiveAbilities.Value)
                return;

            // Re-resolve generic Elemental using live body style so F9 + per-frame updates match Infernal Forge / Tidal Blessing.
            Race abilityRace = ElementalVariantResolver.ResolveElementalAbilityRace(race.Value);

            // Track outdoor time for Amari Bird Tailwind
            if (race.Value == Race.AmariBird && AbilityConfig.EnableTailwind.Value)
            {
                UpdateTailwind();
            }

            // Amari Dog Loyalty Aura — bonus scales with max-friendship NPC count
            if (race.Value == Race.AmariDog && AbilityConfig.EnableLoyaltyAura.Value)
            {
                UpdateLoyaltyAura();
            }

            // Water Elemental Tidal Blessing - auto-water nearby plots
            if (abilityRace == Race.WaterElemental && AbilityConfig.EnableTidalBlessing.Value)
            {
                UpdateTidalBlessing();
            }

            // Fire Elemental Infernal Forge - periodic inventory scan for ore → bars
            if ((abilityRace == Race.FireElemental || abilityRace == Race.Elemental) && AbilityConfig.EnableInfernalForge.Value)
            {
                UpdateInfernalForge();
            }

            // Angel only - Font of Light: periodic mana restore at gold cost (Celestial)
            if (race.Value == Race.Angel && AbilityConfig.EnableFontOfLight.Value)
            {
                UpdateFontOfLight();
            }
        }

        protected override void OnMenuTransition()
        {
            BirthrightGameSaveContext.Reset();
            ResetAllStateForNewSave();
        }

        protected override void OnGameTransition()
        {
            ResetInstanceState();
        }

        /// <summary>
        /// Resets all mod state when a new save is loaded. Call from PlayerPatches.OnPlayerInitialized
        /// so the mod works correctly when switching saves without going through the main menu
        /// (e.g. load-screen flows that skip OnMenuTransition).
        /// </summary>
        public static void ResetAllStateForNewSave()
        {
            CharacterSessionController.ClearTrackedCharacterId();
            StatFrameCache.Reset();
            ActiveAbilityManager.ResetAll();
            var manager = Plugin.GetRacialBonusManager();
            manager?.ClearPlayerRace();
            PlayerPatches.ResetRaceDetection();
            Patches.CombatPatches.ResetNineLives();
            GameApis.ResetAllApiCaches();
            AbilityPatches.ResetReflectionCache();

            var runner = Plugin.GetRunner();
            runner?.ResetRunnerTimersOnly();
        }

        /// <summary>
        /// Clears per-run timers/diagnostics after a full <see cref="ResetAllStateForNewSave"/> (API caches reset separately there).
        /// </summary>
        private void ResetRunnerTimersOnly()
        {
            _outdoorTime = 0f;
            _lastTidalBlessingCheck = 0f;
            _lastInfernalForgeCheck = 0f;
            _lastFontOfLightCheck = 0f;
            _lastLoyaltyAuraCheck = 0f;
            _tidalBlessingDiagLogged = false;
            ActiveAbilityManager.SetBonusValue(ActiveAbilityManager.LoyaltyAura, 0f);
        }

        /// <summary>
        /// Resets instance-level caches and state. Called on game scene transitions (not always a full save reset).
        /// </summary>
        public void ResetInstanceState()
        {
            ResetRunnerTimersOnly();
            StatFrameCache.Reset();
            GameApis.ResetFarmingTileAndInventoryScanState();
        }

        protected override void Log(string message)
        {
            Plugin.Log?.LogInfo(message);
        }

        protected override void LogWarning(string message)
        {
            Plugin.Log?.LogWarning(message);
        }

        /// <summary>
        /// Amari Dog Loyalty Aura: bonus defense, melee, magic, and max HP based on NPCs at max friendship.
        /// </summary>
        private void UpdateLoyaltyAura()
        {
            if (Time.time - _lastLoyaltyAuraCheck < LoyaltyAuraUpdateInterval)
                return;

            _lastLoyaltyAuraCheck = Time.time;

            try
            {
                CharacterData characterData = CharacterFingerprint.GetAuthoritativeCharacterData();
                var relationships = characterData?.Relationships;
                if (relationships == null || relationships.Count == 0)
                {
                    ActiveAbilityManager.SetBonusValue(ActiveAbilityManager.LoyaltyAura, 0f);
                    return;
                }

                int maxFriendCount = 0;
                foreach (var entry in relationships)
                {
                    float points = entry.Value;
                    int maxHearts = GetMaxHeartsForRelationshipPoints(points);
                    if (points >= maxHearts * 5f)
                        maxFriendCount++;
                }

                float bonus = Mathf.Min(
                    maxFriendCount * AbilityConfig.LoyaltyAuraBonusPerNPC.Value,
                    AbilityConfig.LoyaltyAuraMaxBonus.Value);
                ActiveAbilityManager.SetBonusValue(ActiveAbilityManager.LoyaltyAura, bonus);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[LoyaltyAura] Error: {ex.Message}");
                ActiveAbilityManager.SetBonusValue(ActiveAbilityManager.LoyaltyAura, 0f);
            }
        }

        /// <summary>Max heart slots for a relationship tier (mirrors Wish.RelationshipHUD).</summary>
        private static int GetMaxHeartsForRelationshipPoints(float points)
        {
            if (points >= 75f)
                return 20;
            if (points >= 50f)
                return 15;
            return 10;
        }

        /// <summary>
        /// Tracks time spent outdoors for Amari Bird's Tailwind ability.
        /// Outdoor time resets when entering indoor/mine scenes.
        /// </summary>
        private void UpdateTailwind()
        {
            string sceneName = SceneHelpers.GetCurrentSceneName().ToLowerInvariant();

            // Consider indoor: scenes containing "house", "inn", "shop", "cave", "mine", "dungeon"
            bool isIndoor = sceneName.Contains("house") || sceneName.Contains("inn") ||
                            sceneName.Contains("shop") || sceneName.Contains("cave") ||
                            sceneName.Contains("mine") || sceneName.Contains("dungeon") ||
                            sceneName.Contains("interior");

            if (isIndoor)
            {
                _outdoorTime = 0f;
                ActiveAbilityManager.SetBonusValue(ActiveAbilityManager.TailwindOutdoor, 0f);
            }
            else
            {
                _outdoorTime += Time.deltaTime;
                float minutesOutdoor = _outdoorTime / 60f;
                float bonus = Mathf.Min(
                    minutesOutdoor * AbilityConfig.TailwindBonusPerMinute.Value,
                    AbilityConfig.TailwindMaxBonus.Value
                );
                ActiveAbilityManager.SetBonusValue(ActiveAbilityManager.TailwindOutdoor, bonus);
            }
        }

        /// <summary>
        /// Water Elemental's Tidal Blessing: auto-water nearby hoed/planted tiles at HP cost.
        ///
        /// Primary approach: Uses TileManager.Water(Vector2Int, Int16) to water tiles directly.
        /// This is the game's native watering API and handles both the tile state AND any crop on it.
        ///
        /// Fallback: If TileManager is unavailable, falls back to FindObjectsOfType(Crop) + Crop.Water().
        /// </summary>
        private void UpdateTidalBlessing()
        {
            // Runtime toggle check (independent from config — allows in-game on/off)
            if (!ActiveAbilityManager.IsRuntimeEnabled(ActiveAbilityManager.TidalBlessing))
            {
                _tidalBlessingDiagLogged = false; // Reset so diagnostic fires again when re-enabled
                return;
            }

            // Cooldown check
            if (Time.time - _lastTidalBlessingCheck < AbilityConfig.TidalBlessingCooldown.Value)
                return;

            _lastTidalBlessingCheck = Time.time;

            if (ActiveAbilityManager.IsOnCooldown(ActiveAbilityManager.TidalBlessing))
                return;

            try
            {
                var player = Wish.Player.Instance;
                if (player == null)
                    return;

                // Safety threshold check
                float maxHP = player.MaxHealth;
                float currentHP = ReflectionHelper.TryGetValue<float>(player, "health", -1f);
                if (currentHP < 0)
                    currentHP = ReflectionHelper.TryGetValue<float>(player, "Health", maxHP);

                if (maxHP <= 0f)
                    return;

                float hpPercent = (currentHP / maxHP) * 100f;

                // One-time diagnostic dump (fires once per toggle ON)
                if (!_tidalBlessingDiagLogged)
                {
                    _tidalBlessingDiagLogged = true;
                    Plugin.Log?.LogInfo($"[TidalBlessing] === DIAGNOSTIC ===");
                    Plugin.Log?.LogInfo($"[TidalBlessing]   HP: {currentHP:F0}/{maxHP:F0} ({hpPercent:F0}%), threshold: {AbilityConfig.TidalBlessingHPThreshold.Value}%");
                    Plugin.Log?.LogInfo($"[TidalBlessing]   TileManager: type={(GameApis.TileManagerType != null ? "ok" : "NULL")}, instance={(GameApis.TileManagerInstance != null ? "ok" : "NULL")}");
                    Plugin.Log?.LogInfo($"[TidalBlessing]   Methods: Water={(GameApis.WaterTileMethod != null ? "ok" : "NULL")}, farmingData field={(GameApis.FarmingDataField != null ? "ok" : "NULL")}");
                    Plugin.Log?.LogInfo($"[TidalBlessing]   Cooldown: {AbilityConfig.TidalBlessingCooldown.Value}s, HP cost: {AbilityConfig.TidalBlessingHPCostPercent.Value}%");

                    // Log farmingData counts
                    if (GameApis.FarmingDataField != null && GameApis.TileManagerInstance != null)
                    {
                        try
                        {
                            var idict = GameApis.FarmingDataField.GetValue(GameApis.TileManagerInstance) as IDictionary;
                            if (idict != null)
                            {
                                int hoedCount = 0;
                                int wateredCount = 0;
                                foreach (DictionaryEntry entry in idict)
                                {
                                    string s = entry.Value.ToString();
                                    if (s == "Hoed" || s == "2") hoedCount++;
                                    else if (s == "Watered" || s == "3") wateredCount++;
                                }
                                Plugin.Log?.LogInfo($"[TidalBlessing]   farmingData: {idict.Count} total, {hoedCount} hoed, {wateredCount} watered");
                            }
                        }
                        catch (Exception ex) { Plugin.Log?.LogDebug($"[BirthrightRunner] TidalBlessing diagnostic: {ex.Message}"); }
                    }

                    Plugin.Log?.LogInfo($"[TidalBlessing]   Scene: {SceneHelpers.GetCurrentSceneName()}");
                    Plugin.Log?.LogInfo($"[TidalBlessing]   Player world pos: ({player.transform.position.x:F1}, {player.transform.position.y:F1})");
                    Plugin.Log?.LogInfo($"[TidalBlessing]   Grid distance filter: {(GameApis.GridCellToWorldMethod != null && GameApis.GridInstance != null ? "enabled (radius=1)" : "disabled (no Grid)")}");
                    Plugin.Log?.LogInfo($"[TidalBlessing] === END DIAGNOSTIC ===");
                }

                if (hpPercent <= AbilityConfig.TidalBlessingHPThreshold.Value)
                    return;

                // Try TileManager approach first (primary), fall back to Crop approach
                if (GameApis.TileManagerType != null && GameApis.IsWateredMethod != null && GameApis.WaterTileMethod != null)
                {
                    UpdateTidalBlessingViaTileManager(player, maxHP, currentHP);
                }
                else
                {
                    Plugin.Log?.LogInfo("[TidalBlessing] Using fallback Crop approach (TileManager methods not available)");
                    UpdateTidalBlessingViaCropObjects(player, maxHP, currentHP);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[TidalBlessing] Error: {ex.Message}");
                Plugin.Log?.LogWarning($"[TidalBlessing] Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Primary Tidal Blessing approach: iterate farmingData dictionary directly.
        /// Bypasses coordinate conversion entirely — farmingData keys are already in the
        /// exact format that TileManager.Water(Vector2Int, Int16) expects.
        /// Only waters hoed tiles within TidalBlessingRadius of the player (using Grid.CellToWorld
        /// to convert farmingData keys to world positions for distance comparison).
        /// </summary>
        private void UpdateTidalBlessingViaTileManager(Wish.Player player, float maxHP, float currentHP)
        {
            // Check for stale Unity reference before use
            if (GameApis.TileManagerInstance is UnityEngine.Object unityObj && unityObj == null)
            {
                Plugin.Log?.LogInfo("[TidalBlessing] TileManager instance was stale — re-acquiring");
                GameApis.TileManagerInstance = null;
            }

            // Lazy-acquire TileManager instance (may not be ready during init)
            if (GameApis.TileManagerInstance == null)
                GameApis.TryGetTileManagerInstance();
            if (GameApis.TileManagerInstance == null)
            {
                Plugin.Log?.LogInfo("[TidalBlessing] TileManager instance not available — skipping this cycle");
                return;
            }

            // Read farmingData dictionary directly (bypasses coordinate conversion)
            if (GameApis.FarmingDataField == null) return;
            var farmingDict = GameApis.FarmingDataField.GetValue(GameApis.TileManagerInstance) as IDictionary;
            if (farmingDict == null || farmingDict.Count == 0) return;

            float hpCostPerPlot = maxHP * (AbilityConfig.TidalBlessingHPCostPercent.Value / 100f);
            short sceneIndex = (short)SceneManager.GetActiveScene().buildIndex;

            int plotsWatered = 0;
            int hoedFound = 0;

            // Collect hoed tile positions near the player (can't modify dict during iteration
            // because Water() may change the entry from Hoed → Watered)
            var hoedTiles = new List<Vector2Int>();
            var playerPos2D = new Vector2(player.transform.position.x, player.transform.position.y);
            int radius = 1;
            bool hasGridConversion = GameApis.GridCellToWorldMethod != null && GameApis.GridInstance != null;

            foreach (DictionaryEntry entry in farmingDict)
            {
                string state = entry.Value.ToString();
                if (state == "Hoed" || state == "2")
                {
                    var tilePos = (Vector2Int)entry.Key;

                    // Distance filter: convert farmingData key → world pos via Grid.CellToWorld
                    if (hasGridConversion)
                    {
                        try
                        {
                            var worldPos = (Vector3)GameApis.GridCellToWorldMethod.Invoke(GameApis.GridInstance,
                                new object[] { new Vector3Int(tilePos.x, tilePos.y, 0) });
                            float dist = Vector2.Distance(playerPos2D, new Vector2(worldPos.x, worldPos.y));
                            if (dist <= radius)
                                hoedTiles.Add(tilePos);
                        }
                        catch
                        {
                            // Grid conversion failed for this tile — include it as fallback
                            hoedTiles.Add(tilePos);
                        }
                    }
                    else
                    {
                        // No Grid available — water all hoed tiles (fallback behavior)
                        hoedTiles.Add(tilePos);
                    }
                }
            }
            hoedFound = hoedTiles.Count;

            // Water each hoed tile using the dictionary key directly
            // (keys are already in the coordinate system Water() expects)
            foreach (var tilePos in hoedTiles)
            {
                // HP safety check before each watering
                float newHPPercent = ((currentHP - hpCostPerPlot * (plotsWatered + 1)) / maxHP) * 100f;
                if (newHPPercent <= AbilityConfig.TidalBlessingHPThreshold.Value)
                    break;

                try
                {
                    var waterResult = GameApis.WaterTileMethod.Invoke(GameApis.TileManagerInstance, new object[] { tilePos, sceneIndex });
                    if (waterResult is bool success && success)
                        plotsWatered++;
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[TidalBlessing] Water({tilePos}) error: {ex.Message}");
                }
            }

            if (plotsWatered > 0)
            {
                // Deduct HP directly (avoid ReceiveDamage which triggers CombatPatches)
                float totalHPCost = hpCostPerPlot * plotsWatered;
                float newHP = currentHP - totalHPCost;
                if (newHP < 1f) newHP = 1f;
                ReflectionHelper.SetInstanceValue(player, "Health", newHP);

                Plugin.Log?.LogInfo($"[TidalBlessing] Watered {plotsWatered}/{hoedFound} hoed tiles via farmingData scan");
            }
            else if (hoedFound > 0)
            {
                Plugin.Log?.LogInfo($"[TidalBlessing] Found {hoedFound} hoed tiles but Water() returned false for all");
            }
            else
            {
                Plugin.Log?.LogInfo("[TidalBlessing] No hoed tiles in farmingData");
            }
        }

        /// <summary>
        /// Fallback Tidal Blessing approach: find Crop MonoBehaviours via FindObjectsOfType.
        /// Used only if TileManager is unavailable.
        /// </summary>
        private void UpdateTidalBlessingViaCropObjects(Wish.Player player, float maxHP, float currentHP)
        {
            if (GameApis.CropType == null)
            {
                Plugin.Log?.LogWarning("[TidalBlessing] No Crop type and no TileManager — cannot water");
                return;
            }

            var playerPos = player.transform.position;
            int radius = 1;
            int plotsWatered = 0;

            var crops = UnityEngine.Object.FindObjectsOfType(GameApis.CropType);
            Plugin.Log?.LogDebug($"[TidalBlessing] Fallback mode: FindObjectsOfType found {crops?.Length ?? 0} Crop objects");

            if (crops == null || crops.Length == 0)
                return;

            float hpCostPerPlot = maxHP * (AbilityConfig.TidalBlessingHPCostPercent.Value / 100f);

            foreach (var cropObj in crops)
            {
                var cropComponent = cropObj as Component;
                if (cropComponent == null)
                    continue;

                // Check distance
                float distance = Vector2.Distance(
                    new Vector2(playerPos.x, playerPos.y),
                    new Vector2(cropComponent.transform.position.x, cropComponent.transform.position.y)
                );

                if (distance > radius)
                    continue;

                // Check if already watered via crop.data.watered (CropSaveData property)
                object cropData = ReflectionHelper.GetInstanceValue(cropObj, "data");
                if (cropData != null)
                {
                    bool isWatered = ReflectionHelper.TryGetValue<bool>(cropData, "watered", false);
                    if (isWatered)
                        continue;
                }

                // Check HP safety before each watering
                float newHPPercent = ((currentHP - hpCostPerPlot * (plotsWatered + 1)) / maxHP) * 100f;
                if (newHPPercent <= AbilityConfig.TidalBlessingHPThreshold.Value)
                    break;

                // Call Crop.Water() — public void method
                var cropObjType = cropObj.GetType();
                var waterMethod = cropObjType.GetMethod("Water", BindingFlags.Public | BindingFlags.Instance);
                if (waterMethod != null)
                {
                    waterMethod.Invoke(cropObj, null);
                    plotsWatered++;
                }
                else
                {
                    // Last resort: set watered on CropSaveData directly
                    if (cropData != null)
                    {
                        ReflectionHelper.SetInstanceValue(cropData, "watered", true);
                        plotsWatered++;
                    }
                }
            }

            if (plotsWatered > 0)
            {
                float totalHPCost = hpCostPerPlot * plotsWatered;
                float newHP = currentHP - totalHPCost;
                if (newHP < 1f) newHP = 1f;
                ReflectionHelper.SetInstanceValue(player, "Health", newHP);

                Plugin.Log?.LogInfo($"[TidalBlessing] Watered {plotsWatered} crops via fallback Crop.Water()");
            }
        }

        // ===================== INFERNAL FORGE (PERIODIC INVENTORY SCAN) =====================

        /// <summary>
        /// Fire Elemental's Infernal Forge: periodically scans inventory for ore stacks,
        /// smelts them into bars, deducts mana. Replaces the old per-pickup Harmony prefix
        /// which failed because ore is picked up 1 unit at a time (1/3 = 0 bars).
        /// </summary>
        private void UpdateInfernalForge()
        {
            // Runtime toggle check
            if (!ActiveAbilityManager.IsRuntimeEnabled(ActiveAbilityManager.InfernalForge))
                return;

            // Cooldown: only scan every N seconds (default 2s)
            float scanInterval = AbilityConfig.InfernalForgeScanInterval?.Value ?? 2f;
            if (Time.time - _lastInfernalForgeCheck < scanInterval)
                return;
            _lastInfernalForgeCheck = Time.time;

            try
            {
                var player = Wish.Player.Instance;
                if (player == null)
                    return;

                // Get player inventory via reflection
                object inventory = ReflectionHelper.GetInstanceValue(player, "Inventory");
                if (inventory == null)
                    inventory = ReflectionHelper.GetInstanceValue(player, "inventory");
                if (inventory == null)
                {
                    Plugin.Log?.LogInfo("[InfernalForge] Could not find player inventory");
                    return;
                }

                GameApis.EnsureInventoryMethodsCached(inventory);

                // Check mana threshold before scanning
                float maxMana = player.MaxMana;
                if (maxMana <= 0f)
                    return;
                float currentMana = ReflectionHelper.TryGetValue<float>(player, "Mana", 0f);
                float manaPercent = (currentMana / maxMana) * 100f;
                if (manaPercent <= AbilityConfig.InfernalForgeManaThreshold.Value)
                    return;

                int orePerBar = AbilityConfig.InfernalForgeOrePerBar.Value;
                if (orePerBar < 1) orePerBar = 3;

                var addMethod = GameApis.GetAddItemIntMethod(inventory);

                int totalBarsSmelted = 0;
                float totalManaCost = 0f;

                // Scan all ore types in OreToBarMap
                foreach (var kvp in AbilityPatches.OreToBarMap)
                {
                    int oreId = kvp.Key;
                    int barId = kvp.Value;

                    // Get current ore count in inventory
                    int oreCount = GameApis.GetInventoryAmount(inventory, oreId);
                    if (oreCount < orePerBar)
                        continue;

                    // Tiered mana cost: lookup per-ore cost (defaults to 3% if unknown ore)
                    float costPercent = AbilityPatches.OreManaCostMap.TryGetValue(oreId, out float pct) ? pct : 3f;
                    float costPerBar = maxMana * (costPercent / 100f);

                    // Calculate how many bars we can produce
                    int barsProduced = oreCount / orePerBar;
                    int oreUsed = barsProduced * orePerBar;

                    // Check if we can afford the mana cost
                    float manaCostForBatch = costPerBar * barsProduced;
                    float newManaPercent = ((currentMana - totalManaCost - manaCostForBatch) / maxMana) * 100f;

                    if (newManaPercent < AbilityConfig.InfernalForgeManaThreshold.Value)
                    {
                        // Try fewer bars
                        barsProduced = 0;
                        for (int i = oreCount / orePerBar; i >= 1; i--)
                        {
                            float cost = costPerBar * i;
                            float testPercent = ((currentMana - totalManaCost - cost) / maxMana) * 100f;
                            if (testPercent >= AbilityConfig.InfernalForgeManaThreshold.Value)
                            {
                                barsProduced = i;
                                break;
                            }
                        }
                        if (barsProduced <= 0)
                            continue;

                        oreUsed = barsProduced * orePerBar;
                        manaCostForBatch = costPerBar * barsProduced;
                    }

                    // Remove ore from inventory
                    bool removeSuccess = GameApis.RemoveInventoryItem(inventory, oreId, oreUsed);
                    if (!removeSuccess)
                    {
                        Plugin.Log?.LogWarning($"[InfernalForge] Failed to remove {oreUsed}x ore {oreId}");
                        continue;
                    }

                    // Add bars to inventory
                    bool addSuccess = false;
                    if (addMethod != null)
                    {
                        addSuccess = GameApis.InvokeAddItem(addMethod, inventory, barId, barsProduced, true);
                    }

                    if (!addSuccess)
                    {
                        // Bar addition failed — try to return ore
                        Plugin.Log?.LogError($"[InfernalForge] Bar addition failed for {barsProduced}x bar {barId} — returning ore");
                        GameApis.AddInventoryItem(inventory, oreId, oreUsed);
                        continue;
                    }

                    totalBarsSmelted += barsProduced;
                    totalManaCost += manaCostForBatch;

                    Plugin.Log?.LogInfo($"[InfernalForge] Smelted {oreUsed}x ore {oreId} → {barsProduced}x bar {barId} (cost: {costPercent}% per bar)");
                }

                // Apply mana cost and send notification
                if (totalBarsSmelted > 0)
                {
                    float newMana = currentMana - totalManaCost;
                    if (newMana < 0f) newMana = 0f;
                    ReflectionHelper.SetInstanceValue(player, "Mana", newMana);

                    GameApis.SendGameNotification(
                        $"Infernal Forge: Smelted {totalBarsSmelted} bar(s) (-{totalManaCost:F1} Mana)");

                    Plugin.Log?.LogInfo($"[InfernalForge] Total: {totalBarsSmelted} bars, {totalManaCost:F1} mana used");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[InfernalForge] Error: {ex.Message}");
                Plugin.Log?.LogWarning($"[InfernalForge] Stack: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Angel-only: Font of Light — periodic mana restore at the cost of gold.
        /// Only runs when player race is Angel; has no effect for any other race.
        /// </summary>
        private void UpdateFontOfLight()
        {
            if (!ActiveAbilityManager.IsRuntimeEnabled(ActiveAbilityManager.FontOfLight))
                return;

            float interval = AbilityConfig.FontOfLightInterval?.Value ?? 45f;
            if (Time.time - _lastFontOfLightCheck < interval)
                return;
            _lastFontOfLightCheck = Time.time;

            try
            {
                var player = Wish.Player.Instance;
                if (player == null) return;

                float maxMana = player.MaxMana;
                float currentMana = ReflectionHelper.TryGetValue<float>(player, "Mana", 0f);
                float manaPercent = maxMana > 0 ? (currentMana / maxMana) * 100f : 0f;
                if (manaPercent >= (AbilityConfig.FontOfLightManaThreshold?.Value ?? 80f))
                    return;

                int goldCost = AbilityConfig.FontOfLightGoldCost?.Value ?? 10;
                if (goldCost > 0)
                {
                    var gameSaveType = AccessTools.TypeByName("Wish.GameSave");
                    if (gameSaveType != null)
                    {
                        var coinsObj = ReflectionHelper.GetStaticValue(gameSaveType, "Coins");
                        int coins = coinsObj is int c ? c : 0;
                        if (coins < goldCost)
                            return;
                    }
                }

                float restorePercent = AbilityConfig.FontOfLightManaPercent?.Value ?? 5f;
                float restoreAmount = maxMana * (restorePercent / 100f);
                if (restoreAmount <= 0f) return;

                ReflectionHelper.InvokeMethod(player, "AddMana", restoreAmount, 1f);

                if (goldCost > 0)
                {
                    var addMoneyMethod = AccessTools.Method(player.GetType(), "AddMoney",
                        new[] { typeof(int), typeof(bool), typeof(bool), typeof(bool) });
                    if (addMoneyMethod != null)
                        addMoneyMethod.Invoke(player, new object[] { -goldCost, false, false, false });
                }

                GameApis.SendGameNotification(
                    $"Font of Light: +{restorePercent:F0}% mana" + (goldCost > 0 ? $" (-{goldCost} gold)" : ""));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[FontOfLight] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the player's current HP ratio (0-1).
        /// </summary>
        public static float GetPlayerHPRatio()
        {
            try
            {
                var player = Wish.Player.Instance;
                if (player == null)
                    return 1f;

                float maxHP = player.MaxHealth;
                if (maxHP <= 0) return 1f;

                float currentHP = ReflectionHelper.TryGetValue<float>(player, "health", maxHP);
                return currentHP / maxHP;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[BirthrightRunner] Failed to read player HP ratio: {ex.Message}");
                return 1f;
            }
        }

        // ===================== ABILITY TOGGLE HOTKEY =====================

        /// <summary>
        /// Checks for the ability toggle hotkey press and toggles the active ability
        /// for the player's current race. Sends an in-game notification on toggle.
        /// </summary>
        private void CheckAbilityToggleHotkey()
        {
            try
            {
                if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                    return;

                if (!Input.GetKeyDown(Plugin.StaticAbilityToggleKey))
                    return;

                var manager = Plugin.GetRacialBonusManager();
                if (manager == null) return;

                var race = manager.GetPlayerRace();
                if (!race.HasValue)
                {
                    RaceDetectionService.RetryRaceDetection();
                    race = manager.GetPlayerRace();
                }
                if (!race.HasValue)
                {
                    Plugin.Log?.LogInfo("[AbilityToggle] F9 pressed but race is not detected yet; waiting for Player/GameSave (try again shortly).");
                    return;
                }

                RaceDetectionService.RetryRaceDetection();
                race = manager.GetPlayerRace();
                if (!race.HasValue)
                {
                    Plugin.Log?.LogInfo("[AbilityToggle] F9 pressed but race is not detected yet after retry; try again shortly.");
                    return;
                }

                Race stored = race.Value;
                Race abilityRace = ElementalVariantResolver.ResolveRaceForActiveAbilityToggle(stored);
                if (abilityRace != stored)
                    Plugin.Log?.LogInfo($"[AbilityToggle] Body-resolved race for F9: {abilityRace} (cached was {stored}).");

                abilityRace = ElementalVariantResolver.ResolveElementalAbilityRace(abilityRace);
                string abilityKey = GetActiveAbilityForRace(abilityRace);
                if (abilityKey == null)
                {
                    Plugin.Log?.LogInfo($"[AbilityToggle] No toggleable active ability for cached={stored} resolved={abilityRace} (F9).");
                    return;
                }

                bool newState = ActiveAbilityManager.ToggleRuntime(abilityKey);
                string displayName = GetAbilityDisplayName(abilityKey);

                // Send in-game notification via cached 5-arg method
                GameApis.SendGameNotification($"{displayName}: {(newState ? "ON" : "OFF")}");

                Plugin.Log?.LogInfo(
                    $"[AbilityToggle] {(newState ? "ENABLED" : "DISABLED")} " +
                    $"{displayName} (key={abilityKey}) | " +
                    $"storedRace={stored} abilityRace={abilityRace} | " +
                    $"ToggleKey={Plugin.StaticAbilityToggleKey}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[BirthrightRunner] Toggle hotkey error: {ex.Message}");
            }
        }

        /// <summary>
        /// Maps a mod Race to its corresponding toggleable active ability key.
        /// Returns null if the race has no toggleable active ability.
        /// </summary>
        private static string GetActiveAbilityForRace(Race race)
        {
            switch (race)
            {
                case Race.FireElemental:
                    return ActiveAbilityManager.InfernalForge;
                case Race.WaterElemental:
                    return ActiveAbilityManager.TidalBlessing;
                case Race.Angel:
                    return ActiveAbilityManager.FontOfLight;
                case Race.Demon:
                    return ActiveAbilityManager.SoulHarvest;
                case Race.Elemental:
                    // Body still ambiguous (e.g. StyleData not ready); Infernal Forge is the toggle for generic Elemental
                    return ActiveAbilityManager.InfernalForge;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Returns a human-readable display name for an ability key.
        /// </summary>
        private static string GetAbilityDisplayName(string abilityKey)
        {
            switch (abilityKey)
            {
                case ActiveAbilityManager.InfernalForge:
                    return "Infernal Forge";
                case ActiveAbilityManager.TidalBlessing:
                    return "Tidal Blessing";
                case ActiveAbilityManager.FontOfLight:
                    return "Font of Light";
                case ActiveAbilityManager.SoulHarvest:
                    return "Soul Harvest";
                default:
                    return abilityKey;
            }
        }
    }
}
