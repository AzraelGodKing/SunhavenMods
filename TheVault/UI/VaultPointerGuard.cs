using System;
using HarmonyLib;
using SunhavenMods.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheVault.UI
{
    /// <summary>
    /// Keeps inventory clicks from passing through the vault window (AZR-438).
    /// The window is IMGUI, so <see cref="GUI.Button"/> never consumes Sun Haven's
    /// uGUI slot handlers, and <c>PlayerInput.DisableInput</c> does not cover them.
    /// </summary>
    internal static class VaultPointerGuard
    {
        private static bool _windowOpen;
        private static Rect _guiRect;
        private static GameObject _blocker;
        private static RectTransform _hitRect;

        public static void Sync(Rect guiRect, bool open)
        {
            _guiRect = guiRect;
            _windowOpen = open && guiRect.width > 1f && guiRect.height > 1f;
            if (!_windowOpen)
            {
                if (_blocker != null)
                    _blocker.SetActive(false);
                return;
            }

            if (_blocker == null)
                return;

            _blocker.SetActive(true);

            // IMGUI origin is top-left. Overlay canvas origin is bottom-left.
            float y = Screen.height - (guiRect.y + guiRect.height);
            _hitRect.anchoredPosition = new Vector2(guiRect.x, y);
            _hitRect.sizeDelta = new Vector2(guiRect.width, guiRect.height);
        }

        /// <summary>Creates the hit target outside OnGUI. Safe to call every frame.</summary>
        public static void EnsureBlocker()
        {
            if (!_windowOpen || _blocker != null)
                return;

            CreateBlocker();
            if (_blocker == null || _hitRect == null)
                return;

            _blocker.SetActive(true);
            float y = Screen.height - (_guiRect.y + _guiRect.height);
            _hitRect.anchoredPosition = new Vector2(_guiRect.x, y);
            _hitRect.sizeDelta = new Vector2(_guiRect.width, _guiRect.height);
        }

        public static void DestroyBlocker()
        {
            _windowOpen = false;
            if (_blocker == null)
                return;

            UnityEngine.Object.Destroy(_blocker);
            _blocker = null;
            _hitRect = null;
        }

        /// <summary>
        /// <paramref name="screenPoint"/> uses Unity's bottom-left origin
        /// (<see cref="Input.mousePosition"/> / <see cref="PointerEventData.position"/>).
        /// </summary>
        public static bool IsScreenPointOverWindow(Vector2 screenPoint)
        {
            if (!_windowOpen)
                return false;

            float guiY = Screen.height - screenPoint.y;
            return _guiRect.Contains(new Vector2(screenPoint.x, guiY));
        }

        public static void Apply(Harmony harmony)
        {
            if (harmony == null)
                return;

            try
            {
                var pointerDown = new HarmonyMethod(typeof(VaultPointerGuard), nameof(BlockPointerDown));
                Patch(harmony, "Wish.ItemIcon", "OnPointerDown", new[] { typeof(PointerEventData) }, pointerDown);
                Patch(harmony, "Wish.Slot", "OnPointerDown", new[] { typeof(PointerEventData) }, pointerDown);

                var mouse = new HarmonyMethod(typeof(VaultPointerGuard), nameof(BlockMouseButton));
                Patch(harmony, "Wish.PlayerInput", "GetMouseButtonDown", new[] { typeof(int) }, mouse);
                Patch(harmony, "Wish.PlayerInput", "GetMouseButton", new[] { typeof(int) }, mouse);
                Patch(harmony, "Wish.PlayerInput", "GetMouseButtonUp", new[] { typeof(int) }, mouse);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[VaultPointer] Failed to patch inventory clicks: {ex.Message}");
            }
        }

        /// <summary>Prefix for <c>ItemIcon.OnPointerDown</c> and <c>Slot.OnPointerDown</c>.</summary>
        public static bool BlockPointerDown(PointerEventData eventData)
        {
            if (eventData != null && IsScreenPointOverWindow(eventData.position))
                return false;
            return true;
        }

        /// <summary>Prefix for <c>PlayerInput.GetMouseButton*</c>. Skips the original and reports no click.</summary>
        public static bool BlockMouseButton(ref bool __result)
        {
            if (!IsScreenPointOverWindow(Input.mousePosition))
                return true;

            __result = false;
            return false;
        }

        private static void Patch(Harmony harmony, string typeName, string methodName, Type[] parameters, HarmonyMethod prefix)
        {
            var type = AccessTools.TypeByName(typeName);
            var method = type != null ? AccessTools.Method(type, methodName, parameters) : null;
            if (method == null)
            {
                Plugin.Log?.LogWarning($"[VaultPointer] Could not find {typeName}.{methodName}");
                return;
            }

            harmony.Patch(method, prefix: prefix);
            Plugin.Log?.LogDebug($"[VaultPointer] Patched {typeName}.{methodName}");
        }

        private static void CreateBlocker()
        {
            _blocker = new GameObject("TheVault_PointerBlocker");
            UnityEngine.Object.DontDestroyOnLoad(_blocker);
            _blocker.hideFlags = HideFlags.HideAndDontSave;

            var canvas = _blocker.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 32000;
            _blocker.AddComponent<GraphicRaycaster>();

            var hit = new GameObject("Hit");
            hit.transform.SetParent(_blocker.transform, false);
            _hitRect = hit.AddComponent<RectTransform>();
            _hitRect.anchorMin = Vector2.zero;
            _hitRect.anchorMax = Vector2.zero;
            _hitRect.pivot = Vector2.zero;

            var image = hit.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;

            SceneRootSurvivor.TryRegisterPersistentRunnerGameObject(_blocker);
        }
    }
}
