using HarmonyLib;
using SunhavenMods.Shared;
using System;
using System.Reflection;
using UnityEngine;

namespace HavensBirthright
{
    /// <summary>
    /// Notification stack + Wish.Item reflection + inventory AddItem(int,...) — consolidated from patches and runner.
    /// </summary>
    internal static partial class GameApis
    {
        private static object _notificationStackInstance;
        private static MethodInfo _sendNotificationMethod;
        private static bool _notificationInitialized;

        private static bool _reflectionInitialized;
        private static bool _reflectionInitFailed;
        private static int _reflectionInitAttempts;
        private const int MaxReflectionInitAttempts = 3;
        private static FieldInfo _cachedItemIdField;
        private static PropertyInfo _cachedItemIdProperty;
        private static MethodInfo _cachedItemIdMethod;

        private static MethodInfo _cachedAddItemIntMethod;
        private static int _cachedAddItemArgCount;

        private static void InitializeNotificationSystem()
        {
            if (_notificationInitialized) return;
            _notificationInitialized = true;

            try
            {
                var notifStackType = AccessTools.TypeByName("Wish.NotificationStack");
                if (notifStackType == null)
                {
                    Plugin.Log?.LogWarning("[GameApis] NotificationStack type not found");
                    return;
                }

                _sendNotificationMethod = AccessTools.Method(notifStackType, "SendNotification",
                    new[] { typeof(string), typeof(int), typeof(int), typeof(bool), typeof(bool) });

                if (_sendNotificationMethod == null)
                {
                    Plugin.Log?.LogWarning("[GameApis] SendNotification(string,int,int,bool,bool) not found");
                    return;
                }

                TryGetNotificationInstance(notifStackType);

                Plugin.Log?.LogDebug("[GameApis] Notification system initialized" +
                    (_notificationStackInstance != null ? " (instance ready)" : " (instance deferred)"));
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[GameApis] Notification init failed: {ex.Message}");
            }
        }

        private static void TryGetNotificationInstance(Type notifStackType = null)
        {
            if (_notificationStackInstance != null) return;

            try
            {
                if (notifStackType == null)
                    notifStackType = AccessTools.TypeByName("Wish.NotificationStack");
                if (notifStackType == null) return;

                var singletonType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                if (singletonType != null)
                {
                    var genericSingleton = singletonType.MakeGenericType(notifStackType);
                    var instanceProp = AccessTools.Property(genericSingleton, "Instance");
                    if (instanceProp != null)
                    {
                        _notificationStackInstance = instanceProp.GetValue(null);
                        if (_notificationStackInstance != null) return;
                    }
                }

                _notificationStackInstance = ReflectionHelper.GetSingletonInstance(notifStackType);
                if (_notificationStackInstance != null) return;

                var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType", Type.EmptyTypes);
                if (findMethod != null)
                {
                    var genericFind = findMethod.MakeGenericMethod(notifStackType);
                    _notificationStackInstance = genericFind.Invoke(null, null);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[GameApis] Notification instance lookup failed: {ex.Message}");
            }
        }

        internal static void SendGameNotification(string text, int id = 0, int amount = 0, bool unique = false, bool error = false)
        {
            try
            {
                if (!_notificationInitialized)
                    InitializeNotificationSystem();

                if (_sendNotificationMethod == null) return;

                if (_notificationStackInstance == null)
                    TryGetNotificationInstance();

                if (_notificationStackInstance == null) return;

                _sendNotificationMethod.Invoke(_notificationStackInstance,
                    new object[] { text, id, amount, unique, error });
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[GameApis] Notification send failed: {ex.Message}");
            }
        }

        private static void InitializeReflectionCache()
        {
            if (_reflectionInitialized)
                return;
            if (_reflectionInitFailed)
                return;

            _reflectionInitAttempts++;
            if (_reflectionInitAttempts > MaxReflectionInitAttempts)
            {
                _reflectionInitFailed = true;
                Plugin.Log?.LogError($"[GameApis] Reflection init failed after {MaxReflectionInitAttempts} attempts - giving up");
                return;
            }

            try
            {
                var itemType = AccessTools.TypeByName("Wish.Item");
                if (itemType == null)
                {
                    Plugin.Log?.LogWarning($"[GameApis] Could not find Wish.Item type (attempt {_reflectionInitAttempts}/{MaxReflectionInitAttempts})");
                    return;
                }

                string[] fieldNames = { "id", "_id", "itemId", "_itemId", "ItemId", "m_id" };
                foreach (var name in fieldNames)
                {
                    _cachedItemIdField = AccessTools.Field(itemType, name);
                    if (_cachedItemIdField != null) break;
                }

                string[] propNames = { "id", "Id", "ID", "ItemID", "itemId", "ItemId" };
                foreach (var name in propNames)
                {
                    _cachedItemIdProperty = AccessTools.Property(itemType, name);
                    if (_cachedItemIdProperty != null) break;
                }

                string[] methodNames = { "ID", "GetID", "GetId", "GetItemID", "GetItemId" };
                foreach (var name in methodNames)
                {
                    _cachedItemIdMethod = AccessTools.Method(itemType, name);
                    if (_cachedItemIdMethod != null) break;
                }

                if (_cachedItemIdField == null && _cachedItemIdProperty == null && _cachedItemIdMethod == null)
                {
                    Plugin.Log?.LogWarning($"[GameApis] ALL Item ID lookups failed (attempt {_reflectionInitAttempts}/{MaxReflectionInitAttempts})! Members containing 'id':");
                    foreach (var member in itemType.GetMembers(ReflectionHelper.AllBindingFlags))
                    {
                        if (member.Name.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
                            Plugin.Log?.LogWarning($"  - {member.MemberType} {member.Name}");
                    }
                    return;
                }

                _reflectionInitialized = true;
                Plugin.Log?.LogDebug($"[GameApis] Item ID reflection ready - " +
                    $"field:{_cachedItemIdField?.Name ?? "null"}, " +
                    $"prop:{_cachedItemIdProperty?.Name ?? "null"}, " +
                    $"method:{_cachedItemIdMethod?.Name ?? "null"}");
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[GameApis] Reflection cache init failed (attempt {_reflectionInitAttempts}): {ex.Message}");
            }
        }

        internal static int GetItemId(object item)
        {
            if (item == null) return -1;

            InitializeReflectionCache();

            try
            {
                if (_cachedItemIdField != null)
                {
                    var result = _cachedItemIdField.GetValue(item);
                    if (result is int id) return id;
                }

                if (_cachedItemIdProperty != null)
                {
                    var result = _cachedItemIdProperty.GetValue(item);
                    if (result is int id) return id;
                }

                if (_cachedItemIdMethod != null)
                {
                    var result = _cachedItemIdMethod.Invoke(item, null);
                    if (result is int id) return id;
                }

                return GetItemIdSlow(item);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[GameApis] GetItemId failed: {ex.Message}");
                return -1;
            }
        }

        private static int GetItemIdSlow(object item)
        {
            try
            {
                var type = item.GetType();

                foreach (var field in type.GetFields(ReflectionHelper.AllBindingFlags))
                {
                    if (field.FieldType == typeof(int) &&
                        field.Name.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int val = (int)field.GetValue(item);
                        if (val > 0)
                        {
                            _cachedItemIdField = field;
                            Plugin.Log?.LogDebug($"[GameApis] Slow path item ID via field '{field.Name}' = {val}");
                            return val;
                        }
                    }
                }

                foreach (var prop in type.GetProperties(ReflectionHelper.AllBindingFlags))
                {
                    if (prop.PropertyType == typeof(int) && prop.CanRead &&
                        prop.Name.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        int val = (int)prop.GetValue(item);
                        if (val > 0)
                        {
                            _cachedItemIdProperty = prop;
                            Plugin.Log?.LogDebug($"[GameApis] Slow path item ID via property '{prop.Name}' = {val}");
                            return val;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[GameApis] GetItemIdSlow failed: {ex.Message}");
            }

            return -1;
        }

        internal static MethodInfo GetAddItemIntMethod(object inventory)
        {
            if (_cachedAddItemIntMethod != null)
                return _cachedAddItemIntMethod;

            try
            {
                var invType = inventory.GetType();

                _cachedAddItemIntMethod = AccessTools.Method(invType, "AddItem",
                    new[] { typeof(int), typeof(int), typeof(bool) });
                if (_cachedAddItemIntMethod != null)
                {
                    _cachedAddItemArgCount = 3;
                    Plugin.Log?.LogDebug("[GameApis] Cached Inventory.AddItem(int, int, bool)");
                    return _cachedAddItemIntMethod;
                }

                _cachedAddItemIntMethod = AccessTools.Method(invType, "AddItem",
                    new[] { typeof(int), typeof(int) });
                if (_cachedAddItemIntMethod != null)
                {
                    _cachedAddItemArgCount = 2;
                    Plugin.Log?.LogDebug("[GameApis] Cached Inventory.AddItem(int, int)");
                    return _cachedAddItemIntMethod;
                }

                Plugin.Log?.LogWarning("[GameApis] Could not find Inventory.AddItem(int,...) overloads:");
                foreach (var method in invType.GetMethods(ReflectionHelper.AllBindingFlags))
                {
                    if (method.Name == "AddItem")
                    {
                        var parameters = method.GetParameters();
                        string paramList = string.Join(", ", Array.ConvertAll(parameters, p => $"{p.ParameterType.Name} {p.Name}"));
                        Plugin.Log?.LogWarning($"  - AddItem({paramList})");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[GameApis] Failed to cache AddItem method: {ex.Message}");
            }

            return _cachedAddItemIntMethod;
        }

        internal static bool InvokeAddItem(MethodInfo addMethod, object inventory, int itemId, int amount, bool notify)
        {
            try
            {
                if (_cachedAddItemArgCount == 3)
                {
                    addMethod.Invoke(inventory, new object[] { itemId, amount, notify });
                    return true;
                }
                if (_cachedAddItemArgCount == 2)
                {
                    addMethod.Invoke(inventory, new object[] { itemId, amount });
                    return true;
                }

                Plugin.Log?.LogError($"[GameApis] InvokeAddItem: bad arg count {_cachedAddItemArgCount}");
                return false;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogError($"[GameApis] InvokeAddItem threw: {ex.Message}. Item {itemId} x{amount}");
                return false;
            }
        }

        internal static void ResetItemAndNotificationCaches()
        {
            _notificationStackInstance = null;
            _notificationInitialized = false;
            _sendNotificationMethod = null;

            _reflectionInitialized = false;
            _reflectionInitFailed = false;
            _reflectionInitAttempts = 0;
            _cachedAddItemIntMethod = null;
            _cachedAddItemArgCount = 0;
            _cachedItemIdField = null;
            _cachedItemIdProperty = null;
            _cachedItemIdMethod = null;

            Plugin.Log?.LogDebug("[GameApis] Item + notification caches reset");
        }
    }
}
