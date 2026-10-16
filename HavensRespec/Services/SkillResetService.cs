using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HavensRespec.Config;
using HarmonyLib;
using UnityEngine;
using Wish;

namespace HavensRespec.Services
{
    /// <summary>
    /// Profession skill-tree reset and undo. Walks live <see cref="SkillNode"/> instances
    /// in <c>Skills._professionNodeDictionary</c> rather than hardcoded node-name lists.
    /// </summary>
    internal sealed class SkillResetService
    {
        private readonly ManualLogSource _log;
        private readonly Func<bool> _isDebug;
        private readonly Dictionary<ProfessionType, Stack<ResetSnapshot>> _undoByProfession = new Dictionary<ProfessionType, Stack<ResetSnapshot>>();

        private static MethodInfo _updateProfessionMethod;
        private static FieldInfo _professionNodeDictionaryField;
        private static FieldInfo _skillPointsUsedField;
        private static FieldInfo _numActiveNodesField;
        private static bool _reflectionInitialized;

        public SkillResetService(ManualLogSource log, Func<bool> isDebug)
        {
            _log = log;
            _isDebug = isDebug;
        }

        public bool HasUndo(ProfessionType profession)
        {
            return _undoByProfession.TryGetValue(profession, out var stack) && stack.Count > 0;
        }

        /// <summary>
        /// Once reset + charge have both succeeded, attach the charged currency metadata to the
        /// pending undo snapshot so Undo can fully restore both nodes and wallet.
        /// </summary>
        public void AttachUndoCharge(ProfessionType profession, int chargedCost, RespecCostMode chargedCostMode)
        {
            if (!_undoByProfession.TryGetValue(profession, out var stack) || stack.Count == 0)
                return;
            if (chargedCost <= 0 || chargedCostMode == RespecCostMode.None)
                return;

            var snapshot = stack.Pop();
            stack.Push(new ResetSnapshot(
                snapshot.Profession,
                snapshot.Nodes,
                snapshot.SkillPointsUsed,
                snapshot.NumActiveNodes,
                chargedCost,
                chargedCostMode,
                snapshot.ManaRollback));
        }

        /// <summary>
        /// Reset every node of <paramref name="profession"/> to 0 and refund the skill points.
        /// Returns true on success. On failure the game state is not mutated (or is rolled back
        /// from the pre-reset snapshot before returning false).
        /// </summary>
        public bool ResetProfession(Skills skills, ProfessionType profession, out int pointsRefunded)
        {
            pointsRefunded = 0;
            if (skills == null)
            {
                _log?.LogWarning("[Respec] ResetProfession: Skills instance was null.");
                return false;
            }

            EnsureReflection();

            try
            {
                var profData = ResolveProfessionData(profession);
                if (profData == null)
                {
                    _log?.LogWarning($"[Respec] ResetProfession({profession}): no Profession data on the current save.");
                    return false;
                }

                var snapshot = CaptureSnapshot(profession, profData);

                int nodeSlotsVisited = 0;
                int nodesDeactivated = 0;
                int refundedFromNodes = 0;
                var deactivatedManaNodes = new List<ActiveManaNode>();
                try
                {
                    var dict = _professionNodeDictionaryField?.GetValue(skills) as IDictionary;
                    if (dict != null && dict.Contains(profession) && dict[profession] is SkillNode[] nodes)
                    {
                        foreach (var node in nodes)
                        {
                            if (node == null)
                                continue;
                            nodeSlotsVisited++;
                            if (node.active)
                            {
                                int amount = Mathf.Max(1, node.NodeAmount);
                                refundedFromNodes += amount;
                                nodesDeactivated++;
                                if (SkillManaRollback.LooksLikeManaNode(node))
                                {
                                    deactivatedManaNodes.Add(new ActiveManaNode
                                    {
                                        NodeName = node.nodeName,
                                        Description = SkillManaRollback.BuildDescription(node),
                                        NodeAmount = amount
                                    });
                                }

                                node.SetActive(false, false, amount);
                            }
                        }
                    }

                    var existingKeys = new List<int>(profData.Nodes.Keys);
                    foreach (var key in existingKeys)
                        profData.Nodes[key] = 0;

                    var nodeHashes = CollectNodeHashes(skills, profession, profData);
                    foreach (var hash in nodeHashes)
                        profData.Nodes[hash] = 0;

                    SetSkillPointsUsed(profession, 0);
                    SetNumActiveNodes(profession, 0);

                    pointsRefunded = nodeSlotsVisited > 0 ? refundedFromNodes : snapshot.SkillPointsUsed;

                    TryRefreshProfessionPanel(skills, profession);

                    ManaRollbackResult manaRollback = null;
                    try
                    {
                        manaRollback = SkillManaRollback.Apply(deactivatedManaNodes, _log, _isDebug);
                    }
                    catch (Exception manaEx)
                    {
                        _log?.LogWarning($"[Respec] SkillManaRollback skipped: {manaEx.Message}");
                    }

                    var undoSnapshot = manaRollback != null
                        && (manaRollback.ManaRemoved > 0.01f
                            || manaRollback.ProgressFloats.Count > 0
                            || manaRollback.ProgressInts.Count > 0)
                        ? new ResetSnapshot(
                            snapshot.Profession,
                            snapshot.Nodes,
                            snapshot.SkillPointsUsed,
                            snapshot.NumActiveNodes,
                            snapshot.ChargedCost,
                            snapshot.ChargedCostMode,
                            manaRollback)
                        : snapshot;

                    PushUndoSnapshot(profession, undoSnapshot);

                    if (nodeSlotsVisited > 0)
                    {
                        _log?.LogInfo($"[Respec] Reset {profession}: refunded {pointsRefunded} point(s) from {nodesDeactivated}/{nodeSlotsVisited} active node(s); cleared {existingKeys.Count} save key(s).");
                    }
                    else
                    {
                        _log?.LogInfo($"[Respec] Reset {profession}: refunded {pointsRefunded} point(s) (fallback from skillPointsUsed snapshot); cleared {existingKeys.Count} save key(s). _professionNodeDictionary was unreadable.");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    _log?.LogError($"[Respec] ResetProfession({profession}) aborted mid-mutation; rolling back: {ex}");
                    if (!TryRestoreSnapshot(skills, profession, snapshot, profData))
                        _log?.LogError($"[Respec] Rollback after failed reset of {profession} also failed — skill tree may be inconsistent.");
                    pointsRefunded = 0;
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[Respec] ResetProfession({profession}) failed: {ex}");
                return false;
            }
        }

        /// <summary>Restore the most recent snapshot for <paramref name="profession"/>.</summary>
        public bool UndoLastReset(Skills skills, ProfessionType profession, out ResetSnapshot restoredSnapshot)
        {
            restoredSnapshot = null;
            if (!_undoByProfession.TryGetValue(profession, out var stack) || stack.Count == 0)
                return false;
            var snapshot = stack.Pop();

            EnsureReflection();

            try
            {
                var profData = ResolveProfessionData(profession);
                if (profData == null)
                    return false;

                profData.Nodes.Clear();
                foreach (var kv in snapshot.Nodes)
                    profData.Nodes[kv.Key] = kv.Value;

                SetSkillPointsUsed(profession, snapshot.SkillPointsUsed);
                SetNumActiveNodes(profession, snapshot.NumActiveNodes);

                TryRefreshProfessionPanel(skills, profession);
                SkillManaRollback.Restore(snapshot.ManaRollback, _log);

                if (stack.Count == 0)
                    _undoByProfession.Remove(profession);
                restoredSnapshot = snapshot;
                _log?.LogInfo($"[Respec] Undo {profession}: restored {snapshot.Nodes.Count} node(s), {snapshot.SkillPointsUsed} point(s) used.");
                return true;
            }
            catch (Exception ex)
            {
                _log?.LogError($"[Respec] UndoLastReset({profession}) failed: {ex}");
                return false;
            }
        }

        /// <summary>Number of skill points currently allocated in <paramref name="profession"/>.</summary>
        public int GetAllocatedPoints(ProfessionType profession)
        {
            try
            {
                var dict = _skillPointsUsedField?.GetValue(null) as Dictionary<ProfessionType, int>;
                if (dict != null && dict.TryGetValue(profession, out var used))
                    return used;
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[Respec] GetAllocatedPoints({profession}) failed: {ex.Message}");
            }
            return 0;
        }

        public bool TryEstimateExactRefund(Skills skills, ProfessionType profession, out int refundedPoints)
        {
            refundedPoints = 0;
            if (skills == null)
                return false;

            EnsureReflection();

            try
            {
                int slotsVisited = 0;
                var dict = _professionNodeDictionaryField?.GetValue(skills) as IDictionary;
                if (dict != null && dict.Contains(profession) && dict[profession] is SkillNode[] nodes)
                {
                    foreach (var node in nodes)
                    {
                        if (node == null)
                            continue;
                        slotsVisited++;
                        if (node.active)
                            refundedPoints += Mathf.Max(1, node.NodeAmount);
                    }
                }

                if (slotsVisited > 0)
                    return true;

                refundedPoints = GetAllocatedPoints(profession);
                return refundedPoints > 0;
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[Respec] TryEstimateExactRefund({profession}) fallback: {ex.Message}");
                refundedPoints = GetAllocatedPoints(profession);
                return refundedPoints >= 0;
            }
        }

        // ---------------------------------------------------------------- helpers

        private Profession ResolveProfessionData(ProfessionType profession)
        {
            var save = SingletonBehaviour<GameSave>.Instance;
            if (save == null || save.CurrentSave == null || save.CurrentSave.characterData == null)
                return null;
            if (!save.CurrentSave.characterData.Professions.TryGetValue(profession, out var prof))
                return null;
            return prof;
        }

        private ResetSnapshot CaptureSnapshot(ProfessionType profession, Profession profData)
        {
            var copy = new Dictionary<int, int>(profData.Nodes.Count);
            foreach (var kv in profData.Nodes)
                copy[kv.Key] = kv.Value;

            int used = 0;
            int active = 0;
            try
            {
                var usedDict = _skillPointsUsedField?.GetValue(null) as Dictionary<ProfessionType, int>;
                if (usedDict != null && usedDict.TryGetValue(profession, out var u))
                    used = u;

                var activeDict = _numActiveNodesField?.GetValue(null) as Dictionary<ProfessionType, int>;
                if (activeDict != null && activeDict.TryGetValue(profession, out var a))
                    active = a;
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[Respec] CaptureSnapshot({profession}) counters fallback: {ex.Message}");
            }
            return new ResetSnapshot(profession, copy, used, active);
        }

        private List<int> CollectNodeHashes(Skills skills, ProfessionType profession, Profession profData)
        {
            var hashSet = new HashSet<int>();
            try
            {
                var dict = _professionNodeDictionaryField?.GetValue(skills) as IDictionary;
                if (dict != null && dict.Contains(profession))
                {
                    if (dict[profession] is SkillNode[] nodes)
                    {
                        foreach (var node in nodes)
                        {
                            if (node == null || string.IsNullOrEmpty(node.nodeName))
                                continue;
                            hashSet.Add(node.nodeName.GetStableHashCode());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[Respec] CollectNodeHashes({profession}) reflection fallback: {ex.Message}");
            }

            foreach (var key in profData.Nodes.Keys)
            {
                hashSet.Add(key);
            }
            return new List<int>(hashSet);
        }

        private static void SetSkillPointsUsed(ProfessionType profession, int value)
        {
            var dict = _skillPointsUsedField?.GetValue(null) as Dictionary<ProfessionType, int>;
            if (dict != null)
                dict[profession] = value;
        }

        private static void SetNumActiveNodes(ProfessionType profession, int value)
        {
            var dict = _numActiveNodesField?.GetValue(null) as Dictionary<ProfessionType, int>;
            if (dict != null)
                dict[profession] = value;
        }

        private void TryRefreshProfessionPanel(Skills skills, ProfessionType profession)
        {
            try
            {
                // NOTE: we deliberately do NOT call skills.EnablePanelWithAvailableSkillPoint()
                // here. That method walks ProfessionType in enum order (Combat, Farming,
                // Fishing, Mining, Exploration) and force-enables the panel of the FIRST
                // profession that has AvailableSkillPoint == true — which, after a reset,
                // is almost always a different tab than the one the user is standing on.
                // End result: you click Reset on Mining, the panel jumps to Farming. Instead
                // we just repaint the node images on the profession we actually touched via
                // UpdateProfession, and let the existing tab stay open.
                if (_updateProfessionMethod != null)
                {
                    var panel = ResolvePanelField(skills, profession);
                    var image = ResolveAvailableImageField(skills, profession);
                    var asset = ResolveSkillTreeAssetField(skills, profession);
                    if (panel != null && image != null && asset != null)
                        _updateProfessionMethod.Invoke(skills, new[] { profession, panel, image, asset });
                }
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[Respec] TryRefreshProfessionPanel({profession}) swallowed: {ex.Message}");
            }
        }

        private static object ResolvePanelField(Skills skills, ProfessionType profession)
        {
            string name = ProfessionUiMap.ResolvePanelFieldName(profession);
            return name == null ? null : AccessTools.Field(typeof(Skills), name)?.GetValue(skills);
        }

        private static object ResolveAvailableImageField(Skills skills, ProfessionType profession)
        {
            string name = profession switch
            {
                ProfessionType.Combat => "combatSkillPointAvailable",
                ProfessionType.Farming => "farmingSkillPointAvailable",
                ProfessionType.Mining => "miningSkillPointAvailable",
                ProfessionType.Exploration => "explorationSkillPointAvailable",
                ProfessionType.Fishing => "fishingSkillPointAvailable",
                _ => null,
            };
            return name == null ? null : AccessTools.Field(typeof(Skills), name)?.GetValue(skills);
        }

        private static object ResolveSkillTreeAssetField(Skills skills, ProfessionType profession)
        {
            string name = profession switch
            {
                ProfessionType.Combat => "combatSkillTreeAsset",
                ProfessionType.Farming => "farmingSkillTreeAsset",
                ProfessionType.Mining => "miningSkillTreeAsset",
                ProfessionType.Exploration => "explorationSkillTreeAsset",
                ProfessionType.Fishing => "fishingSkillTreeAsset",
                _ => null,
            };
            return name == null ? null : AccessTools.Field(typeof(Skills), name)?.GetValue(skills);
        }

        private static void EnsureReflection()
        {
            if (_reflectionInitialized)
                return;

            _updateProfessionMethod = typeof(Skills).GetMethod(
                "UpdateProfession",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            _professionNodeDictionaryField = typeof(Skills).GetField(
                "_professionNodeDictionary",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            _skillPointsUsedField = typeof(Skills).GetField(
                "skillPointsUsed",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            _numActiveNodesField = typeof(Skills).GetField(
                "numActiveNodes",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            _reflectionInitialized = true;
        }

        private void PushUndoSnapshot(ProfessionType profession, ResetSnapshot snapshot)
        {
            if (!_undoByProfession.TryGetValue(profession, out var stack))
            {
                stack = new Stack<ResetSnapshot>();
                _undoByProfession[profession] = stack;
            }
            stack.Push(snapshot);
        }

        /// <summary>
        /// Restores save + Skills counters from a pre-mutation snapshot without touching the undo stack.
        /// Used when a reset fails mid-walk so callers never charge for a partial wipe.
        /// </summary>
        private bool TryRestoreSnapshot(Skills skills, ProfessionType profession, ResetSnapshot snapshot, Profession profData)
        {
            if (snapshot == null || profData == null)
                return false;

            try
            {
                profData.Nodes.Clear();
                foreach (var kv in snapshot.Nodes)
                    profData.Nodes[kv.Key] = kv.Value;

                SetSkillPointsUsed(profession, snapshot.SkillPointsUsed);
                SetNumActiveNodes(profession, snapshot.NumActiveNodes);
                TryRefreshProfessionPanel(skills, profession);
                return true;
            }
            catch (Exception ex)
            {
                _log?.LogError($"[Respec] TryRestoreSnapshot({profession}) failed: {ex}");
                return false;
            }
        }
    }
}
