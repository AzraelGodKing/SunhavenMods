using System;
using System.Collections.Generic;

namespace HavenDevTools.API
{
    /// <summary>
    /// Static registry for third-party dev tool panels.
    /// Mods can register via DevToolsRegistry.Register(panel) to add custom panels to the Extensions tab.
    /// </summary>
    public static class DevToolsRegistry
    {
        private static readonly List<IDevToolsPanel> _panels = new List<IDevToolsPanel>();
        private static readonly object _lock = new object();

        /// <summary>All registered panels</summary>
        public static IReadOnlyList<IDevToolsPanel> Panels
        {
            get
            {
                lock (_lock)
                {
                    return _panels.ToArray();
                }
            }
        }

        /// <summary>
        /// Register a custom panel. Call from your mod's Awake() or when you detect HavenDevTools is loaded.
        /// </summary>
        public static void Register(IDevToolsPanel panel)
        {
            if (panel == null) return;

            lock (_lock)
            {
                if (_panels.Exists(p => p.ModGuid == panel.ModGuid))
                {
                    HavenDevTools.Plugin.Log?.LogWarning($"[DevToolsRegistry] Panel for {panel.ModGuid} already registered");
                    return;
                }
                _panels.Add(panel);
                HavenDevTools.Plugin.Log?.LogDebug($"[DevToolsRegistry] Registered panel: {panel.DisplayName} ({panel.ModGuid})");
            }
        }

        /// <summary>Unregister a panel by GUID (e.g. when mod unloads)</summary>
        public static void Unregister(string modGuid)
        {
            lock (_lock)
            {
                _panels.RemoveAll(p => p.ModGuid == modGuid);
            }
        }
    }
}
