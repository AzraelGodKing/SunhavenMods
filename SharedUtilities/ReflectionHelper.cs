using System;
using System.Collections.Generic;
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

        // Member lookups by name are cached per (type, name), including misses, so callers that read
        // game state by name every few frames (Haven's Birthright stat cache, combat patches) stop
        // paying for Type.GetProperty / GetField and the base-type walk on every call (AZR-729).
        // Types never change at runtime, so entries never need invalidating.
        private sealed class MemberAccessor
        {
            public static readonly MemberAccessor Missing = new MemberAccessor(null, null);

            public readonly PropertyInfo Property;
            public readonly FieldInfo Field;

            public MemberAccessor(PropertyInfo property, FieldInfo field)
            {
                Property = property;
                Field = field;
            }
        }

        private static readonly object MemberCacheLock = new object();
        private static readonly Dictionary<(Type, string), MemberAccessor> StaticGetCache =
            new Dictionary<(Type, string), MemberAccessor>();
        private static readonly Dictionary<(Type, string), MemberAccessor> InstanceGetCache =
            new Dictionary<(Type, string), MemberAccessor>();
        private static readonly Dictionary<(Type, string), MemberAccessor> InstanceSetCache =
            new Dictionary<(Type, string), MemberAccessor>();

        private static readonly string[] SingletonMemberNames = { "Instance", "instance", "_instance", "Singleton", "singleton" };

        private static bool TryGetCached(Dictionary<(Type, string), MemberAccessor> cache, Type type, string name, out MemberAccessor accessor)
        {
            lock (MemberCacheLock)
                return cache.TryGetValue((type, name), out accessor);
        }

        private static void StoreCached(Dictionary<(Type, string), MemberAccessor> cache, Type type, string name, MemberAccessor accessor)
        {
            lock (MemberCacheLock)
                cache[(type, name)] = accessor;
        }

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
            if (type == null || memberName == null)
                return null;

            if (!TryGetCached(StaticGetCache, type, memberName, out var accessor))
            {
                accessor = ResolveStaticGetter(type, memberName);
                StoreCached(StaticGetCache, type, memberName, accessor);
            }

            if (accessor.Property != null)
                return accessor.Property.GetValue(null);
            if (accessor.Field != null)
                return accessor.Field.GetValue(null);
            return null;
        }

        private static MemberAccessor ResolveStaticGetter(Type type, string memberName)
        {
            try
            {
                var prop = type.GetProperty(memberName, AllBindingFlags);
                if (prop != null && prop.GetMethod != null && prop.GetIndexParameters().Length == 0)
                    return new MemberAccessor(prop, null);
            }
            catch (AmbiguousMatchException)
            {
                // Same as before caching: an ambiguous property name resolves to "no value".
                return MemberAccessor.Missing;
            }

            var field = type.GetField(memberName, AllBindingFlags);
            return field != null ? new MemberAccessor(null, field) : MemberAccessor.Missing;
        }

        /// <summary>
        /// Gets a singleton instance from a type (looks for Instance, instance, or _instance).
        /// </summary>
        public static object GetSingletonInstance(Type type)
        {
            if (type == null)
                return null;

            foreach (var name in SingletonMemberNames)
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
            if (instance == null || memberName == null)
                return null;

            var instanceType = instance.GetType();
            if (!TryGetCached(InstanceGetCache, instanceType, memberName, out var accessor))
            {
                // Resolution can throw AmbiguousMatchException; let it propagate uncached, as before.
                accessor = ResolveInstanceMember(instanceType, memberName, forSet: false);
                StoreCached(InstanceGetCache, instanceType, memberName, accessor);
            }

            if (accessor.Property != null)
                return accessor.Property.GetValue(instance);
            if (accessor.Field != null)
                return accessor.Field.GetValue(instance);
            return null;
        }

        /// <summary>
        /// Walks up the inheritance chain and returns the first property (with a getter or setter,
        /// depending on <paramref name="forSet"/>) or field named <paramref name="memberName"/>.
        /// </summary>
        private static MemberAccessor ResolveInstanceMember(Type type, string memberName, bool forSet)
        {
            while (type != null)
            {
                var prop = type.GetProperty(memberName, AllBindingFlags);
                if (prop != null && (forSet ? prop.SetMethod : prop.GetMethod) != null)
                    return new MemberAccessor(prop, null);

                var field = type.GetField(memberName, AllBindingFlags);
                if (field != null)
                    return new MemberAccessor(null, field);

                type = type.BaseType;
            }

            return MemberAccessor.Missing;
        }

        /// <summary>
        /// Sets the value of an instance property or field.
        /// </summary>
        public static bool SetInstanceValue(object instance, string memberName, object value)
        {
            if (instance == null || memberName == null)
                return false;

            var instanceType = instance.GetType();
            if (!TryGetCached(InstanceSetCache, instanceType, memberName, out var accessor))
            {
                accessor = ResolveInstanceMember(instanceType, memberName, forSet: true);
                StoreCached(InstanceSetCache, instanceType, memberName, accessor);
            }

            if (accessor.Property != null)
            {
                accessor.Property.SetValue(instance, value);
                return true;
            }
            if (accessor.Field != null)
            {
                accessor.Field.SetValue(instance, value);
                return true;
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
        /// Gets all fields of a type (including inherited private fields).
        /// </summary>
        public static FieldInfo[] GetAllFields(Type type)
        {
            if (type == null)
                return Array.Empty<FieldInfo>();

            return type.GetFields(AllBindingFlags)
                .Concat(type.BaseType != null && type.BaseType != typeof(object)
                    ? GetAllFields(type.BaseType)
                    : Enumerable.Empty<FieldInfo>())
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// Gets all properties of a type (including inherited).
        /// </summary>
        public static PropertyInfo[] GetAllProperties(Type type)
        {
            if (type == null)
                return Array.Empty<PropertyInfo>();

            return type.GetProperties(AllBindingFlags)
                .Concat(type.BaseType != null && type.BaseType != typeof(object)
                    ? GetAllProperties(type.BaseType)
                    : Enumerable.Empty<PropertyInfo>())
                .GroupBy(p => p.Name)
                .Select(g => g.First())
                .ToArray();
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
