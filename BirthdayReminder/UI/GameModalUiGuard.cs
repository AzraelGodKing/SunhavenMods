using System;
using System.Reflection;
using HarmonyLib;

namespace BirthdayReminder.UI
{
    /// <summary>
    /// Suppresses Birthday Reminder IMGUI while Sun Haven has vanilla blocking UI open (inventory,
    /// external panels such as journal/calendar, dialogue, etc.). Prevents z-order / hit-test clashes
    /// between IMGUI overlays and uGUI (reported as calendar visual glitches when the HUD was visible).
    /// </summary>
    internal static class GameModalUiGuard
    {
        private static Type _uiHandlerType;
        private static FieldInfo _instanceField;
        private static PropertyInfo _inventoryUiBlockingProp;
        private static bool _resolveAttempted;

        // AZR-734: OnGUI calls this several times per frame (Layout, Repaint, input events).
        // The answer cannot change within a frame, so reflect + unbox once per frame.
        private static int _cachedFrame = -1;
        private static bool _cachedResult;

        /// <summary>
        /// True when <see cref="Wish.UIHandler.InventoryUiActiveInHierarchy"/> would return true:
        /// inventory, external UI (journal/calendar path), dialogue, quest rewards, etc.
        /// </summary>
        public static bool ShouldSuppressBirthdayImGui()
        {
            int frame = UnityEngine.Time.frameCount;
            if (frame == _cachedFrame)
                return _cachedResult;

            _cachedResult = ShouldSuppressBirthdayImGuiUncached();
            _cachedFrame = frame;
            return _cachedResult;
        }

        private static bool ShouldSuppressBirthdayImGuiUncached()
        {
            try
            {
                EnsureCached();
                if (_instanceField == null || _inventoryUiBlockingProp == null)
                    return false;

                object handler = _instanceField.GetValue(null);
                if (handler == null)
                    return false;

                object v = _inventoryUiBlockingProp.GetValue(handler);
                return v is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureCached()
        {
            if (_resolveAttempted)
                return;
            _resolveAttempted = true;

            try
            {
                _uiHandlerType = AccessTools.TypeByName("Wish.UIHandler");
                if (_uiHandlerType == null)
                    return;

                _instanceField = AccessTools.Field(_uiHandlerType, "Instance");
                _inventoryUiBlockingProp = AccessTools.Property(_uiHandlerType, "InventoryUiActiveInHierarchy");
            }
            catch
            {
                // Leave fields null; suppression stays off.
            }
        }
    }
}
