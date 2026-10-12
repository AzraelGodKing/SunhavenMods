using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Logging;

namespace SunhavenMods.Shared
{
    /// <summary>
    /// Shared soft-dependency client for Sun Haven Todo (AZR-255).
    /// Locates the Todo plugin through the Chainloader and hands out a
    /// <see cref="TodoManagerReflection"/> bound to its live TodoManager, so no consumer
    /// references Todo's assembly or reflects into it directly.
    /// </summary>
    public static class TodoSoftClient
    {
        public const string PluginGuid = "com.azraelgodking.sunhaventodo";

        private static TodoManagerReflection _bound;
        private static readonly HashSet<string> Warned = new HashSet<string>(StringComparer.Ordinal);

        public static bool IsAvailable =>
            Chainloader.PluginInfos != null &&
            Chainloader.PluginInfos.ContainsKey(PluginGuid);

        /// <summary>
        /// The live TodoManager, or null when Todo is missing, not initialized yet, or its API
        /// no longer matches. Failures are logged once until the next success.
        /// </summary>
        public static TodoManagerReflection TryGetManager(ManualLogSource log = null)
        {
            Type pluginType = GetPluginInstance()?.GetType();
            if (pluginType == null)
                return null;

            object manager;
            try
            {
                manager = pluginType
                    .GetMethod("GetTodoManager", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)
                    ?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Warn("manager", $"GetTodoManager failed: {ex.Message}", log);
                return null;
            }

            if (manager == null)
            {
                if (Warned.Add("manager-null"))
                    log?.LogDebug("[TodoSoftClient] TodoManager not created yet");
                return null;
            }
            Warned.Remove("manager-null");
            Warned.Remove("manager");

            if (_bound != null && ReferenceEquals(_bound.Manager, manager))
                return _bound;

            _bound = TodoManagerReflection.TryBind(manager, out string error);
            if (_bound == null)
                Warn("bind", $"Sun Haven Todo API changed, integration disabled ({error})", log);
            else
                Warned.Remove("bind");
            return _bound;
        }

        /// <summary>
        /// Returns the manager once <paramref name="characterName"/>'s list is loaded, asking Todo to
        /// load it if needed. An empty name accepts whatever character Todo has loaded.
        /// </summary>
        public static TodoManagerReflection TryGetManagerForCharacter(string characterName, ManualLogSource log = null)
        {
            var todo = TryGetManager(log);
            if (todo == null)
                return null;

            try
            {
                if (IsLoadedFor(todo, characterName))
                {
                    Warned.Remove("data");
                    return todo;
                }

                if (string.IsNullOrEmpty(characterName))
                {
                    Warn("data", "no character loaded in Sun Haven Todo yet", log);
                    return null;
                }

                object instance = GetPluginInstance();
                MethodInfo load = instance?.GetType().GetMethod(
                    "LoadDataForCharacter", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
                if (load == null)
                {
                    Warn("data", "Sun Haven Todo data not loaded and LoadDataForCharacter is unavailable", log);
                    return null;
                }

                load.Invoke(instance, new object[] { characterName });
                if (!IsLoadedFor(todo, characterName))
                {
                    Warn("data", $"failed to load todo data for character \"{characterName}\"", log);
                    return null;
                }

                Warned.Remove("data");
                return todo;
            }
            catch (Exception ex)
            {
                Warn("data", $"loading todo data failed: {ex.Message}", log);
                return null;
            }
        }

        /// <summary>Asks Todo to write pending changes now instead of at its next autosave.</summary>
        public static void Save(ManualLogSource log = null)
        {
            try
            {
                GetPluginInstance()?.GetType()
                    .GetMethod("SaveData", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)
                    ?.Invoke(null, null);
            }
            catch (Exception ex)
            {
                log?.LogWarning($"[TodoSoftClient] SaveData failed: {ex.Message}");
            }
        }

        private static bool IsLoadedFor(TodoManagerReflection todo, string characterName)
        {
            if (!todo.HasData)
                return false;
            return string.IsNullOrEmpty(characterName) ||
                   string.Equals(todo.CurrentCharacter, characterName, StringComparison.OrdinalIgnoreCase);
        }

        private static object GetPluginInstance()
        {
            if (Chainloader.PluginInfos == null ||
                !Chainloader.PluginInfos.TryGetValue(PluginGuid, out var info) || info == null)
                return null;
            return info.Instance;
        }

        private static void Warn(string key, string message, ManualLogSource log)
        {
            if (Warned.Add(key))
                log?.LogWarning($"[TodoSoftClient] {message}");
        }
    }
}
