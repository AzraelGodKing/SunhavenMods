using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace SunhavenMods.Shared
{
    /// <summary>
    /// Utility methods for common reflection operations in Sun Haven mods.
    /// Wraps Harmony's AccessTools with additional fallback strategies.
    /// </summary>
    public static class ReflectionHelper
    {
        /// <summary>
        /// Binding flags for finding public and private members.
        /// </summary>
        public static readonly BindingFlags AllBindingFlags =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.FlattenHierarchy;

        /// <summary>
        /// Finds a type by name, searching across all loaded assemblies if needed.
        /// </summary>
        /// <param name="typeName">The full or partial type name</param>
        /// <param name="namespaces">Optional namespace prefixes to try</param>
        /// <returns>The found Type, or null if not found</returns>
        public static Type FindType(string typeName, params string[] namespaces)
        {
            var hasNamespaces = namespaces != null && namespaces.Length > 0;

            // When namespaces are supplied, resolve qualified names first so short names like
            // "Plugin" do not match the wrong mod (e.g. HavenDevTools.Plugin).
            if (hasNamespaces)
            {
                foreach (var ns in namespaces)
                {
                    var qualified = AccessTools.TypeByName($"{ns}.{typeName}");
                    if (qualified != null)
                        return qualified;
                }

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var ns in namespaces)
                    {
                        try
                        {
                            var qualified = assembly.GetType($"{ns}.{typeName}", throwOnError: false);
                            if (qualified != null)
                                return qualified;
                        }
                        catch
                        {
                            // Skip assemblies that fail type enumeration.
                        }
                    }
                }

                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var match = assembly.GetTypes().FirstOrDefault(t =>
                            namespaces.Any(ns =>
                                string.Equals(t.FullName, $"{ns}.{typeName}", StringComparison.Ordinal)
                                || (t.Name == typeName && string.Equals(t.Namespace, ns, StringComparison.Ordinal))));
                        if (match != null)
                            return match;
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        var match = ex.Types?.FirstOrDefault(t =>
                            t != null && namespaces.Any(ns =>
                                string.Equals(t.FullName, $"{ns}.{typeName}", StringComparison.Ordinal)
                                || (t.Name == typeName && string.Equals(t.Namespace, ns, StringComparison.Ordinal))));
                        if (match != null)
                            return match;
                    }
                }

                return null;
            }

            var type = AccessTools.TypeByName(typeName);
            if (type != null)
                return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetTypes().FirstOrDefault(t =>
                        t.Name == typeName || t.FullName == typeName);
                    if (type != null)
                        return type;
                }
                catch (ReflectionTypeLoadException)
                {
                    // Some assemblies may have loading issues - skip them
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the main <c>Plugin</c> type for a mod assembly (e.g. GiftingAssistant → GiftingAssistant.Plugin).
        /// </summary>
        public static Type FindModPlugin(string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(assemblyName))
                return null;

            var byNamespace = FindType("Plugin", assemblyName);
            if (byNamespace != null
                && string.Equals(byNamespace.Assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return byNamespace;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var direct = assembly.GetType($"{assemblyName}.Plugin", throwOnError: false);
                if (direct != null)
                    return direct;

                try
                {
                    var plugin = assembly.GetTypes().FirstOrDefault(t =>
                        t.IsClass && !t.IsAbstract && t.Name == "Plugin");
                    if (plugin != null)
                        return plugin;
                }
                catch (ReflectionTypeLoadException ex)
                {
                    var plugin = ex.Types?.FirstOrDefault(t =>
                        t != null && t.IsClass && !t.IsAbstract && t.Name == "Plugin");
                    if (plugin != null)
                        return plugin;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a static method on a type, including internal methods.
        /// </summary>
        public static MethodInfo GetStaticMethod(Type type, string methodName)
        {
            return type?.GetMethod(methodName, AllBindingFlags);
        }

        /// <summary>
        /// Finds a type in the Wish namespace (common Sun Haven namespace).
        /// </summary>
        public static Type FindWishType(string typeName)
        {
            return FindType(typeName, "Wish");
        }

        /// <summary>
        /// Gets the value of a static property or field from a type.
        /// Useful for accessing singleton instances.
        /// If multiple static properties share a name (rare) or an indexer confuses resolution,
        /// this returns null (or use an explicit <c>GetProperty</c> with parameter types).
        /// </summary>
        public static object GetStaticValue(Type type, string memberName)
        {
            if (type == null)
                return null;

            try
            {
                var prop = type.GetProperty(memberName, AllBindingFlags);
                if (prop != null && prop.GetMethod != null && prop.GetIndexParameters().Length == 0)
                    return prop.GetValue(null);
            }
            catch (AmbiguousMatchException)
            {
                return null;
            }

            // Try field
            var field = type.GetField(memberName, AllBindingFlags);
            if (field != null)
                return field.GetValue(null);

            return null;
        }

        /// <summary>
        /// Gets a singleton instance from a type (looks for Instance, instance, or _instance).
        /// </summary>
        public static object GetSingletonInstance(Type type)
        {
            if (type == null)
                return null;

            var instanceNames = new[] { "Instance", "instance", "_instance", "Singleton", "singleton" };

            foreach (var name in instanceNames)
            {
                var value = GetStaticValue(type, name);
                if (value != null)
                    return value;
            }

            return null;
        }

        /// <summary>
        /// Gets the value of an instance property or field.
        /// </summary>
        public static object GetInstanceValue(object instance, string memberName)
        {
            if (instance == null)
                return null;

            var type = instance.GetType();

            // Walk up the inheritance chain
            while (type != null)
            {
                // Try property
                var prop = type.GetProperty(memberName, AllBindingFlags);
                if (prop != null && prop.GetMethod != null)
                    return prop.GetValue(instance);

                // Try field
                var field = type.GetField(memberName, AllBindingFlags);
                if (field != null)
                    return field.GetValue(instance);

                type = type.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Sets the value of an instance property or field.
        /// </summary>
        public static bool SetInstanceValue(object instance, string memberName, object value)
        {
            if (instance == null)
                return false;

            var type = instance.GetType();

            // Walk up the inheritance chain
            while (type != null)
            {
                // Try property
                var prop = type.GetProperty(memberName, AllBindingFlags);
                if (prop != null && prop.SetMethod != null)
                {
                    prop.SetValue(instance, value);
                    return true;
                }

                // Try field
                var field = type.GetField(memberName, AllBindingFlags);
                if (field != null)
                {
                    field.SetValue(instance, value);
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        /// <summary>
        /// Invokes a method on an object with the given arguments.
        /// </summary>
        public static object InvokeMethod(object instance, string methodName, params object[] args)
        {
            if (instance == null)
                return null;

            var type = instance.GetType();
            var argTypes = args?.Select(a => a?.GetType() ?? typeof(object)).ToArray() ?? Type.EmptyTypes;

            // Try to find the method with matching parameter types
            var method = AccessTools.Method(type, methodName, argTypes);

            // Fallback to finding by name only
            if (method == null)
                method = type.GetMethod(methodName, AllBindingFlags);

            if (method == null)
                return null;

            return method.Invoke(instance, args);
        }

        /// <summary>
        /// Invokes a static method on a type with the given arguments.
        /// </summary>
        public static object InvokeStaticMethod(Type type, string methodName, params object[] args)
        {
            if (type == null)
                return null;

            var argTypes = args?.Select(a => a?.GetType() ?? typeof(object)).ToArray() ?? Type.EmptyTypes;
            var method = AccessTools.Method(type, methodName, argTypes);

            if (method == null)
                method = type.GetMethod(methodName, AllBindingFlags);

            if (method == null)
                return null;

            return method.Invoke(null, args);
        }

        /// <summary>
        /// Safely tries to get a value via reflection, returning default on failure.
        /// </summary>
        public static T TryGetValue<T>(object instance, string memberName, T defaultValue = default)
        {
            try
            {
                var value = GetInstanceValue(instance, memberName);
                if (value is T typedValue)
                    return typedValue;

                if (value != null && typeof(T).IsAssignableFrom(value.GetType()))
                    return (T)value;

                return defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
