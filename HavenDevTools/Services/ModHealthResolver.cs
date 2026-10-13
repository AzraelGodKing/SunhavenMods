using System;
using BepInEx.Bootstrap;

namespace HavenDevTools.Services
{
    internal static class ModHealthResolver
    {
        public static string ResolveDisplayName(string pluginGuid)
        {
            if (string.IsNullOrEmpty(pluginGuid))
                return null;

            try
            {
                if (Chainloader.PluginInfos != null
                    && Chainloader.PluginInfos.TryGetValue(pluginGuid, out var info)
                    && info?.Metadata != null
                    && !string.IsNullOrEmpty(info.Metadata.Name))
                {
                    return info.Metadata.Name;
                }
            }
            catch
            {
                // Chainloader may not be ready in exotic startup paths.
            }

            return null;
        }

        public static string ResolveInstalledVersion(string pluginGuid)
        {
            if (string.IsNullOrEmpty(pluginGuid))
                return null;

            try
            {
                if (Chainloader.PluginInfos != null
                    && Chainloader.PluginInfos.TryGetValue(pluginGuid, out var info)
                    && info?.Metadata != null)
                {
                    return info.Metadata.Version.ToString();
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }
    }
}
