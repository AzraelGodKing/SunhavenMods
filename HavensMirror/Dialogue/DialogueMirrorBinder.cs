using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using HavensMirror.Config;
using HavensMirror.Gallery;
using HavensMirror.Look;
using SunhavenMods.Shared;
using UnityEngine;

namespace HavensMirror.Dialogue
{
    /// <summary>
    /// Discovers DialogueController bust-related members and applies Harmony hooks.
    /// Binding is reflection-first so game updates that rename overloads degrade gracefully.
    /// </summary>
    public static class DialogueMirrorBinder
    {
        private static bool _bound;
        private static FieldInfo _dialoguePanelField;
        private static FieldInfo _bustField;
        private static FieldInfo _npcNameField;
        private static readonly Dictionary<LookSlot, object> EmoteTables = new Dictionary<LookSlot, object>();
        private static readonly Dictionary<LookSlot, object> EmoteAddressables = new Dictionary<LookSlot, object>();

        public static void Apply(Harmony harmony)
        {
            if (_bound)
                return;

            Type dialogueType = ReflectionHelper.FindWishType("DialogueController");
            if (dialogueType == null)
            {
                Plugin.Log?.LogWarning("[Reflect] Wish.DialogueController not found — player bust overlay inactive.");
                return;
            }

            _dialoguePanelField = FindField(dialogueType, "_dialoguePanel", "dialoguePanel", "DialoguePanel");
            _bustField = FindField(dialogueType, "_bust", "bust", "Bust");
            _npcNameField = FindField(dialogueType, "npcName", "_npcName", "NpcName");

            TryPatchAwake(harmony, dialogueType);
            TryPatchBustVisualSetters(harmony, dialogueType);
            TryPatchInitialBustSelectors(harmony, dialogueType);
            TryPatchCancelDialogue(harmony, dialogueType);

            _bound = true;
        }

        private static void TryPatchAwake(Harmony harmony, Type dialogueType)
        {
            MethodInfo awake = AccessTools.Method(dialogueType, "Awake");
            if (awake == null)
            {
                Plugin.Log?.LogWarning("[Reflect] DialogueController.Awake missing.");
                return;
            }

            harmony.Patch(awake, postfix: new HarmonyMethod(typeof(DialogueMirrorBinder), nameof(AwakePostfix)));
            Plugin.Log?.LogInfo("Patched DialogueController.Awake for mirror HUD attach.");
        }

        private static void TryPatchBustVisualSetters(Harmony harmony, Type dialogueType)
        {
            int patched = 0;
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(dialogueType))
            {
                if (method == null || method.IsAbstract)
                    continue;

                string name = method.Name ?? string.Empty;
                // Cover both the optimized and legacy visual setters without hardcoding one signature.
                if (name.IndexOf("SetDialogueBust", StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("SetBustVisual", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                try
                {
                    harmony.Patch(
                        method,
                        prefix: new HarmonyMethod(typeof(DialogueMirrorBinder), nameof(BustSetterPrefix)),
                        postfix: new HarmonyMethod(typeof(DialogueMirrorBinder), nameof(BustSetterPostfix)));
                    patched++;
                    Plugin.Log?.LogInfo($"Patched {dialogueType.Name}.{method.Name} for player bust present.");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[Reflect] Could not patch {method.Name}: {ex.Message}");
                }
            }

            if (patched == 0)
                Plugin.Log?.LogWarning("[Reflect] No DialogueController bust visual setters found.");
        }

        private static void TryPatchInitialBustSelectors(Harmony harmony, Type dialogueType)
        {
            foreach (string methodName in new[] { "SetInitialBust", "GetInitialBust" })
            {
                foreach (MethodInfo method in AccessTools.GetDeclaredMethods(dialogueType))
                {
                    if (method == null || !string.Equals(method.Name, methodName, StringComparison.Ordinal))
                        continue;

                    try
                    {
                        if (methodName == "SetInitialBust")
                        {
                            harmony.Patch(method, prefix: new HarmonyMethod(typeof(DialogueMirrorBinder), nameof(SetInitialBustPrefix)));
                        }
                        else
                        {
                            harmony.Patch(method, prefix: new HarmonyMethod(typeof(DialogueMirrorBinder), nameof(GetInitialBustPrefix)));
                        }

                        Plugin.Log?.LogInfo($"Patched DialogueController.{methodName} for forced look.");
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log?.LogWarning($"[Reflect] Could not patch {methodName}: {ex.Message}");
                    }
                }
            }
        }

        // ---- Harmony targets (parameter-flexible via __args) ----

        private static void AwakePostfix(object __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                CaptureEmoteTables(__instance);
                EnsureHudOn(__instance);
                if (PlayerBustHud.Instance != null)
                    Plugin.Log?.LogInfo("[Dialogue] PlayerBustHud attached on DialogueController.Awake.");
                else
                    Plugin.Log?.LogWarning("[Dialogue] Awake: DialogueController host not found — HUD attach deferred.");

                // Saves are usually loaded by the time dialogue UI exists — ensure character folders exist.
                SaveSlotFolderSync.EnsureCharacterFolders();
                Plugin.ReloadGallery(notify: false);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[Dialogue] Awake hook failed: {ex.Message}");
            }
        }

        private static void BustSetterPrefix(object[] __args)
        {
            if (MirrorOptions.Enabled == null || !MirrorOptions.Enabled.Value)
                return;

            try
            {
                MutateLookFlagsInArgs(__args);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[Dialogue] BustSetterPrefix: {ex.Message}");
            }
        }

        private static void BustSetterPostfix(object __instance, object[] __args)
        {
            if (MirrorOptions.Enabled == null || !MirrorOptions.Enabled.Value)
                return;

            try
            {
                // NPCAI.Interact always passes isRefreshBust:true — that flag only hides the
                // vanilla NPC bust while addressables load. Do NOT treat it as "hide player".
                bool vows = false, shore = false, costume = false, refreshIgnored = false;
                ReadLookFlagsFromArgs(__args, ref vows, ref shore, ref costume, ref refreshIgnored);

                // Awake may have missed the panel (or Instance was cleared) — re-attach here.
                EnsureHudOn(__instance);

                PlayerBustHud hud = PlayerBustHud.Instance;
                if (hud == null)
                {
                    Plugin.Log?.LogWarning("[Dialogue] Present skipped — PlayerBustHud not attached.");
                    return;
                }

                hud.Present(vows, shore, costume);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[Dialogue] BustSetterPostfix: {ex.Message}");
            }
        }

        private static void TryPatchCancelDialogue(Harmony harmony, Type dialogueType)
        {
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(dialogueType))
            {
                if (method == null || !string.Equals(method.Name, "CancelDialogue", StringComparison.Ordinal))
                    continue;

                try
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(DialogueMirrorBinder), nameof(CancelDialoguePostfix)));
                    Plugin.Log?.LogInfo($"Patched DialogueController.{method.Name} to hide player bust.");
                }
                catch (Exception ex)
                {
                    Plugin.Log?.LogWarning($"[Reflect] Could not patch CancelDialogue: {ex.Message}");
                }
            }
        }

        private static void CancelDialoguePostfix()
        {
            try
            {
                PlayerBustHud.Instance?.Hide();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[Dialogue] CancelDialogue hide: {ex.Message}");
            }
        }

        private static void EnsureHudOn(object dialogueController)
        {
            // Prefer the DialogueController GameObject — _bust is a sibling of _dialoguePanel,
            // not a child of it. Parenting under the panel put the player behind the text chrome.
            GameObject host = null;
            if (dialogueController is Component component)
                host = component.gameObject;

            if (host == null && _dialoguePanelField != null && dialogueController != null)
                host = _dialoguePanelField.GetValue(dialogueController) as GameObject;

            if (host == null)
                return;

            bool created = PlayerBustHud.Instance == null || PlayerBustHud.Instance.gameObject == null;
            PlayerBustHud hud = PlayerBustHud.EnsureOn(host);
            if (hud == null)
                return;

            if (_bustField != null && dialogueController != null
                && _bustField.GetValue(dialogueController) is UnityEngine.UI.Image bustImage)
            {
                hud.BindNpcBust(bustImage);
            }

            if (created)
                Plugin.Log?.LogInfo("[Dialogue] PlayerBustHud attached on DialogueController host.");
        }

        private static bool SetInitialBustPrefix(object __instance, object[] __args)
        {
            if (MirrorOptions.Enabled == null || !MirrorOptions.Enabled.Value)
                return true;

            try
            {
                MutateLookFlagsInArgs(__args);

                string npcName = ReadNpcName(__instance);
                if (string.IsNullOrWhiteSpace(npcName))
                    return false; // empty name — skip vanilla initial bust (player-facing path)

                if (!ForcedLookResolver.TryParseForced(out LookSlot forced)
                    && !ArgsRequestSpecialLook(__args))
                {
                    // No force and no special look — let vanilla seasonal logic run.
                    return true;
                }

                bool vows = false, shore = false, costume = false, refresh = false;
                ReadLookFlagsFromArgs(__args, ref vows, ref shore, ref costume, ref refresh);
                LookSlot slot = ForcedLookResolver.ResolveActiveLook(vows, shore, costume);

                if (TryAssignNpcSprite(__instance, slot, npcName, __args))
                    return false;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[Dialogue] SetInitialBustPrefix: {ex.Message}");
            }

            return true;
        }

        private static bool GetInitialBustPrefix(object __instance, ref object __result, object[] __args)
        {
            if (MirrorOptions.Enabled == null || !MirrorOptions.Enabled.Value)
                return true;

            try
            {
                MutateLookFlagsInArgs(__args);

                string npcName = ReadNpcName(__instance);
                if (string.IsNullOrWhiteSpace(npcName))
                    return false;

                if (!ForcedLookResolver.TryParseForced(out _)
                    && !ArgsRequestSpecialLook(__args))
                {
                    return true;
                }

                bool vows = false, shore = false, costume = false, refresh = false;
                ReadLookFlagsFromArgs(__args, ref vows, ref shore, ref costume, ref refresh);
                LookSlot slot = ForcedLookResolver.ResolveActiveLook(vows, shore, costume);

                if (TryResolveAddressable(__instance, slot, npcName, out object assetRef))
                {
                    __result = assetRef;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[Dialogue] GetInitialBustPrefix: {ex.Message}");
            }

            return true;
        }

        private static void CaptureEmoteTables(object dialogueController)
        {
            EmoteTables.Clear();
            EmoteAddressables.Clear();

            TryCapture(dialogueController, LookSlot.Spring, EmoteTables, "_npcEmotes", "npcEmotes");
            TryCapture(dialogueController, LookSlot.Summer, EmoteTables, "_npcSummerEmotes", "npcSummerEmotes");
            TryCapture(dialogueController, LookSlot.Autumn, EmoteTables, "_npcFallEmotes", "npcFallEmotes");
            TryCapture(dialogueController, LookSlot.Winter, EmoteTables, "_npcWinterEmotes", "npcWinterEmotes");
            TryCapture(dialogueController, LookSlot.Vows, EmoteTables, "_npcWeddingEmotes", "npcWeddingEmotes");
            TryCapture(dialogueController, LookSlot.Shore, EmoteTables, "_npcSwimsuitEmotes", "npcSwimsuitEmotes");

            TryCapture(dialogueController, LookSlot.Spring, EmoteAddressables, "_npcEmotes2", "npcEmotes2");
            TryCapture(dialogueController, LookSlot.Summer, EmoteAddressables, "_npcSummerEmotes2", "npcSummerEmotes2");
            TryCapture(dialogueController, LookSlot.Autumn, EmoteAddressables, "_npcFallEmotes2", "npcFallEmotes2");
            TryCapture(dialogueController, LookSlot.Winter, EmoteAddressables, "_npcWinterEmotes2", "npcWinterEmotes2");
            TryCapture(dialogueController, LookSlot.Vows, EmoteAddressables, "_npcWeddingEmotes2", "npcWeddingEmotes2");
            TryCapture(dialogueController, LookSlot.Shore, EmoteAddressables, "_npcSwimsuitEmotes2", "npcSwimsuitEmotes2");
            TryCapture(dialogueController, LookSlot.Costume, EmoteAddressables, "_npcHalloweenEmotes2", "npcHalloweenEmotes2");
        }

        private static void TryCapture(object instance, LookSlot slot, Dictionary<LookSlot, object> bag, params string[] names)
        {
            FieldInfo field = FindField(instance.GetType(), names);
            if (field == null)
                return;

            object value = field.GetValue(instance);
            if (value != null)
                bag[slot] = value;
        }

        private static bool TryAssignNpcSprite(object dialogueController, LookSlot slot, string npcName, object[] args)
        {
            if (!EmoteTables.TryGetValue(slot, out object table) || table == null)
                return false;

            // table is Dictionary<string, List<Sprite>> (or similar) — use IDictionary.
            if (!(table is System.Collections.IDictionary dict))
                return false;

            object listObj = null;
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (entry.Key != null && string.Equals(entry.Key.ToString(), npcName, StringComparison.OrdinalIgnoreCase))
                {
                    listObj = entry.Value;
                    break;
                }
            }

            if (listObj == null)
                return false;

            Sprite sprite = null;
            if (listObj is System.Collections.IList list && list.Count > 0)
                sprite = list[0] as Sprite;

            if (sprite == null)
                return false;

            // Prefer an Image argument named bust / _bust if present; else find Image field.
            ImageFieldAssign(dialogueController, sprite, args);
            return true;
        }

        private static void ImageFieldAssign(object dialogueController, Sprite sprite, object[] args)
        {
            if (args != null)
            {
                foreach (object arg in args)
                {
                    if (arg is UnityEngine.UI.Image imgArg)
                    {
                        imgArg.sprite = sprite;
                        return;
                    }
                }
            }

            FieldInfo bustField = FindField(dialogueController.GetType(), "_bust", "bust", "Bust");
            if (bustField != null && bustField.GetValue(dialogueController) is UnityEngine.UI.Image bustImage)
                bustImage.sprite = sprite;
        }

        private static bool TryResolveAddressable(object dialogueController, LookSlot slot, string npcName, out object assetRef)
        {
            assetRef = null;
            if (!EmoteAddressables.TryGetValue(slot, out object table) || table == null)
                return false;

            if (!(table is System.Collections.IDictionary dict))
                return false;

            object arrObj = null;
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (entry.Key != null && string.Equals(entry.Key.ToString(), npcName, StringComparison.OrdinalIgnoreCase))
                {
                    arrObj = entry.Value;
                    break;
                }
            }

            if (arrObj is Array arr && arr.Length > 0)
            {
                assetRef = arr.GetValue(0);
                return assetRef != null;
            }

            return false;
        }

        private static string ReadNpcName(object dialogueController)
        {
            if (_npcNameField == null || dialogueController == null)
                return null;

            object value = _npcNameField.GetValue(dialogueController);
            return value?.ToString();
        }

        private static void MutateLookFlagsInArgs(object[] args)
        {
            if (args == null || !ForcedLookResolver.TryParseForced(out LookSlot forced))
                return;

            // Sun Haven bust setters typically expose bools as:
            // [0]=small, [1]=marriage/vows, [2]=swimsuit/shore, [3]=hideName, [4]=refresh, [5]=halloween/costume
            if (forced != LookSlot.Vows && forced != LookSlot.Shore && forced != LookSlot.Costume)
                return;

            var boolIndexes = new List<int>();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is bool)
                    boolIndexes.Add(i);
            }

            if (boolIndexes.Count < 2)
                return;

            // Clear only the special-look slots; leave small/hide/refresh alone.
            if (boolIndexes.Count > 1) args[boolIndexes[1]] = false;
            if (boolIndexes.Count > 2) args[boolIndexes[2]] = false;
            if (boolIndexes.Count > 5) args[boolIndexes[5]] = false;

            if (forced == LookSlot.Vows)
                args[boolIndexes[1]] = true;
            else if (forced == LookSlot.Shore && boolIndexes.Count > 2)
                args[boolIndexes[2]] = true;
            else if (forced == LookSlot.Costume && boolIndexes.Count > 5)
                args[boolIndexes[5]] = true;
            else if (forced == LookSlot.Costume)
                args[boolIndexes[boolIndexes.Count - 1]] = true;
        }

        private static void ReadLookFlagsFromArgs(object[] args, ref bool vows, ref bool shore, ref bool costume, ref bool refresh)
        {
            if (args == null)
                return;

            var bools = new List<bool>();
            foreach (object arg in args)
            {
                if (arg is bool b)
                    bools.Add(b);
            }

            // [0]=small, [1]=vows, [2]=shore, [3]=hideName, [4]=refresh, [5]=costume
            if (bools.Count > 1) vows = bools[1];
            if (bools.Count > 2) shore = bools[2];
            if (bools.Count > 4) refresh = bools[4];
            if (bools.Count > 5) costume = bools[5];

            ForcedLookResolver.ApplyForcedFlags(ref vows, ref shore, ref costume);
        }

        private static bool ArgsRequestSpecialLook(object[] args)
        {
            bool vows = false, shore = false, costume = false, refresh = false;
            ReadLookFlagsFromArgs(args, ref vows, ref shore, ref costume, ref refresh);
            return vows || shore || costume;
        }

        private static FieldInfo FindField(Type type, params string[] names)
        {
            if (type == null || names == null)
                return null;

            foreach (string name in names)
            {
                FieldInfo field = AccessTools.Field(type, name);
                if (field != null)
                    return field;
            }

            return null;
        }
    }
}
