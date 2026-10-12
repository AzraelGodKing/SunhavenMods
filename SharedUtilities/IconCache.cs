using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine;
using HarmonyLib;
using BepInEx.Logging;

namespace SunhavenMods.Shared
{
    /// <summary>
    /// Caches item icons from the game's Database for use in IMGUI.
    /// Loads icons asynchronously and converts Sprites to Texture2D for IMGUI compatibility.
    /// Shared between TheVault and SunHavenMuseumUtilityTracker.
    /// </summary>
    public static class IconCache
    {
        /// <summary>
        /// Full-atlas sprites reference <see cref="Sprite.texture"/> directly; those assets must never be <see cref="UnityEngine.Object.Destroy"/>ed.
        /// Only textures we allocate (<see cref="ExtractSpriteTexture"/>) are owned and safe to destroy on eviction.
        /// </summary>
        private struct CachedIcon
        {
            public Texture2D Texture;
            /// <summary>True if this mod allocated the texture and must Destroy it on eviction/clear.</summary>
            public bool OwnsTexture;
        }

        private static readonly Dictionary<int, CachedIcon> _iconCache = new Dictionary<int, CachedIcon>();
        private const int MaxCacheSize = 200;
        private static readonly HashSet<int> _loadingItems = new HashSet<int>();
        private static readonly HashSet<int> _failedItems = new HashSet<int>();
        private static readonly Dictionary<string, int> _currencyToItemId = new Dictionary<string, int>();

        private static Texture2D _fallbackTexture;
        private static ManualLogSource _log;
        private static Type _databaseType;
        private static Type _itemDataType;
        private static MethodInfo _getDataMethod;
        private static bool _reflectionInitialized;
        private static bool _initialized;
        private static bool _iconsLoaded;
        private static int[] _pendingPreloadItemIds;

        /// <summary>
        /// Initialize the icon cache. Call during plugin initialization.
        /// </summary>
        /// <param name="log">Logger for diagnostics (can be null)</param>
        /// <param name="preloadItemIds">Optional item IDs to preload (e.g. currency IDs). If null, icons load on demand.</param>
        public static void Initialize(ManualLogSource log, int[] preloadItemIds = null)
        {
            _log = log;
            if (_initialized)
            {
                _log?.LogDebug("[IconCache] Already initialized");
                return;
            }
            _initialized = true;

            _log?.LogInfo("[IconCache] Initializing icon cache...");
            _fallbackTexture = CreateFallbackTexture();
            _log?.LogInfo("[IconCache] Created fallback texture");

            if (preloadItemIds != null && preloadItemIds.Length > 0)
            {
                _pendingPreloadItemIds = (int[])preloadItemIds.Clone();
                _log?.LogInfo($"[IconCache] Preload: {_pendingPreloadItemIds.Length} item ID(s) will queue with LoadAllIcons");
            }
        }

        /// <summary>
        /// Register a currency ID to item ID mapping. Used by TheVault.
        /// </summary>
        public static void RegisterCurrency(string currencyId, int itemId)
        {
            _currencyToItemId[currencyId] = itemId;
        }

        /// <summary>
        /// Load all registered currency icons. Call after game is ready (e.g. player initialized).
        /// </summary>
        public static void LoadAllIcons()
        {
            if (_iconsLoaded)
            {
                _log?.LogDebug("[IconCache] Icons already loaded, skipping");
                return;
            }

            if (!InitializeReflection())
            {
                _log?.LogError("[IconCache] Failed to initialize reflection, cannot load icons");
                _iconsLoaded = true;
                return;
            }

            foreach (var kvp in _currencyToItemId)
            {
                _log?.LogDebug($"[IconCache] Queuing load for: {kvp.Key} (ItemID: {kvp.Value})");
                LoadIcon(kvp.Value);
            }

            if (_pendingPreloadItemIds != null)
            {
                foreach (int pid in _pendingPreloadItemIds)
                {
                    if (pid > 0)
                    {
                        _log?.LogDebug($"[IconCache] Queuing preload item ID: {pid}");
                        LoadIcon(pid);
                    }
                }
            }

            _iconsLoaded = true;
            _log?.LogInfo($"[IconCache] Queued {_currencyToItemId.Count} currency icon(s); preload queue processed.");
        }

        /// <summary>
        /// Get icon for a currency ID.
        /// </summary>
        public static Texture2D GetIconForCurrency(string currencyId)
        {
            if (_currencyToItemId.TryGetValue(currencyId, out int itemId))
            {
                return GetIcon(itemId);
            }
            return GetFallbackTexture();
        }

        /// <summary>
        /// Get icon for an item ID.
        /// </summary>
        public static Texture2D GetIcon(int itemId)
        {
            if (itemId <= 0)
                return GetFallbackTexture();

            if (_iconCache.TryGetValue(itemId, out CachedIcon cached))
            {
                return cached.Texture;
            }

            if (!_loadingItems.Contains(itemId) && !_failedItems.Contains(itemId))
            {
                LoadIcon(itemId);
            }

            return GetFallbackTexture();
        }

        /// <summary>
        /// Null-safe fallback texture. Ensures callers never get null.
        /// </summary>
        private static Texture2D GetFallbackTexture()
        {
            if (_fallbackTexture == null)
            {
                _fallbackTexture = CreateFallbackTexture();
            }
            return _fallbackTexture;
        }

        /// <summary>
        /// Check if an icon is loaded for an item ID.
        /// </summary>
        public static bool IsIconLoaded(int itemId)
        {
            return _iconCache.ContainsKey(itemId);
        }

        /// <summary>
        /// Check if an icon is loaded for a currency ID.
        /// </summary>
        public static bool IsIconLoaded(string currencyId)
        {
            return _currencyToItemId.TryGetValue(currencyId, out int itemId) && IsIconLoaded(itemId);
        }

        private static bool InitializeReflection()
        {
            if (_reflectionInitialized)
            {
                return _databaseType != null && _itemDataType != null && _getDataMethod != null;
            }
            _reflectionInitialized = true;

            try
            {
                string[] databaseTypeNames = { "Database", "Wish.Database", "PSS.Database", "SunHaven.Database" };
                foreach (var typeName in databaseTypeNames)
                {
                    _databaseType = AccessTools.TypeByName(typeName);
                    if (_databaseType != null)
                    {
                        _log?.LogInfo($"[IconCache] Found Database type: {_databaseType.FullName}");
                        break;
                    }
                }

                if (_databaseType == null)
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            foreach (var type in assembly.GetTypes())
                            {
                                if (type.Name == "Database" && !type.IsNested)
                                {
                                    foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                                    {
                                        if (m.Name == "GetData" && m.IsGenericMethod)
                                        {
                                            _databaseType = type;
                                            _log?.LogInfo($"[IconCache] Found Database type: {type.FullName}");
                                            break;
                                        }
                                    }
                                    if (_databaseType != null) break;
                                }
                            }
                            if (_databaseType != null) break;
                        }
                        catch (Exception ex)
                        {
                            _log?.LogDebug($"[IconCache] Skipping assembly {assembly.GetName().Name}: {ex.Message}");
                        }
                    }
                }

                if (_databaseType == null)
                {
                    _log?.LogError("[IconCache] Could not find Database type");
                    return false;
                }

                _itemDataType = AccessTools.TypeByName("Wish.ItemData");
                if (_itemDataType == null)
                {
                    _log?.LogError("[IconCache] Could not find Wish.ItemData type");
                    return false;
                }

                var methods = _databaseType.GetMethods(BindingFlags.Public | BindingFlags.Static);
                foreach (var method in methods)
                {
                    if (method.Name == "GetData" && method.IsGenericMethod)
                    {
                        var methodParams = method.GetParameters();
                        if (method.GetGenericArguments().Length == 1 && methodParams.Length == 3 &&
                            methodParams[0].ParameterType == typeof(int))
                        {
                            _getDataMethod = method.MakeGenericMethod(_itemDataType);
                            _log?.LogInfo("[IconCache] Found Database.GetData method");
                            break;
                        }
                    }
                }

                if (_getDataMethod == null)
                {
                    _log?.LogError("[IconCache] Could not find Database.GetData method");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _log?.LogError($"[IconCache] Error initializing reflection: {ex.Message}");
                return false;
            }
        }

        private static void LoadIcon(int itemId)
        {
            if (itemId <= 0)
                return;
            if (_loadingItems.Contains(itemId) || _iconCache.ContainsKey(itemId))
            {
                return;
            }
            _loadingItems.Add(itemId);

            try
            {
                if (!InitializeReflection() || _getDataMethod == null)
                {
                    _failedItems.Add(itemId);
                    _loadingItems.Remove(itemId);
                    return;
                }

                var callbackDelegateType = typeof(Action<>).MakeGenericType(_itemDataType);
                var itemDataParam = Expression.Parameter(_itemDataType, "itemData");
                var itemIdConst = Expression.Constant(itemId);
                var onLoadedMethod = typeof(IconCache).GetMethod(nameof(OnIconLoadedInternal), BindingFlags.NonPublic | BindingFlags.Static);
                var callExpr = Expression.Call(onLoadedMethod, itemIdConst, Expression.Convert(itemDataParam, typeof(object)));
                var successCallback = Expression.Lambda(callbackDelegateType, callExpr, itemDataParam).Compile();

                var onFailedMethod = typeof(IconCache).GetMethod(nameof(OnIconLoadFailed), BindingFlags.NonPublic | BindingFlags.Static);
                var failCallback = Expression.Lambda<Action>(Expression.Call(onFailedMethod, itemIdConst)).Compile();

                _getDataMethod.Invoke(null, new object[] { itemId, successCallback, failCallback });
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[IconCache] Error loading icon {itemId}: {ex.Message}");
                _failedItems.Add(itemId);
                _loadingItems.Remove(itemId);
            }
        }

        private static void OnIconLoadedInternal(int itemId, object itemData)
        {
            _loadingItems.Remove(itemId);

            if (itemData == null)
            {
                _failedItems.Add(itemId);
                return;
            }

            try
            {
                Type actualType = itemData.GetType();
                object spriteObj = null;

                var bindingFlagSets = new[]
                {
                    BindingFlags.Public | BindingFlags.Instance,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy,
                };

                foreach (var flags in bindingFlagSets)
                {
                    var iconProp = actualType.GetProperty("icon", flags);
                    if (iconProp != null)
                    {
                        spriteObj = iconProp.GetValue(itemData);
                        break;
                    }
                }

                if (spriteObj == null)
                {
                    foreach (var flags in bindingFlagSets)
                    {
                        var iconField = actualType.GetField("icon", flags);
                        if (iconField != null)
                        {
                            spriteObj = iconField.GetValue(itemData);
                            break;
                        }
                    }
                }

                if (spriteObj == null)
                {
                    Type currentType = actualType;
                    while (currentType != null && spriteObj == null)
                    {
                        var props = currentType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        foreach (var prop in props)
                        {
                            if (prop.Name.Equals("icon", StringComparison.OrdinalIgnoreCase))
                            {
                                spriteObj = prop.GetValue(itemData);
                                break;
                            }
                        }
                        if (spriteObj == null)
                        {
                            var fields = currentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                            foreach (var field in fields)
                            {
                                if (field.Name.Equals("icon", StringComparison.OrdinalIgnoreCase))
                                {
                                    spriteObj = field.GetValue(itemData);
                                    break;
                                }
                            }
                        }
                        currentType = currentType.BaseType;
                    }
                }

                if (spriteObj is Sprite sprite)
                {
                    CacheSprite(itemId, sprite);
                }
                else
                {
                    _failedItems.Add(itemId);
                }
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[IconCache] Error processing icon {itemId}: {ex.Message}");
                _failedItems.Add(itemId);
            }
        }

        private static void OnIconLoadFailed(int itemId)
        {
            _loadingItems.Remove(itemId);
            _failedItems.Add(itemId);
        }

        private static void CacheSprite(int itemId, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
            {
                _failedItems.Add(itemId);
                return;
            }

            try
            {
                bool ownsTexture;
                Texture2D texture;
                if (sprite.rect.width == sprite.texture.width && sprite.rect.height == sprite.texture.height)
                {
                    // Reference to game atlas — must not Destroy on eviction.
                    texture = sprite.texture;
                    ownsTexture = false;
                }
                else
                {
                    texture = ExtractSpriteTexture(sprite);
                    ownsTexture = texture != null;
                }

                if (texture != null)
                {
                    _iconCache[itemId] = new CachedIcon { Texture = texture, OwnsTexture = ownsTexture };
                    if (_iconCache.Count > MaxCacheSize)
                    {
                        int evictId = -1;
                        int fallbackEvictId = -1;
                        using (var enumerator = _iconCache.Keys.GetEnumerator())
                        {
                            while (enumerator.MoveNext())
                            {
                                int candidate = enumerator.Current;
                                if (fallbackEvictId < 0)
                                    fallbackEvictId = candidate;
                                if (_loadingItems.Contains(candidate) || _failedItems.Contains(candidate))
                                    continue;
                                evictId = candidate;
                                break;
                            }
                        }

                        if (evictId < 0)
                            evictId = fallbackEvictId;

                        if (evictId >= 0 && _iconCache.TryGetValue(evictId, out CachedIcon evicted))
                        {
                            if (evicted.OwnsTexture && evicted.Texture != null)
                                UnityEngine.Object.Destroy(evicted.Texture);
                            _iconCache.Remove(evictId);
                        }
                    }
                }
                else
                {
                    _failedItems.Add(itemId);
                }
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[IconCache] Error caching sprite {itemId}: {ex.Message}");
                _failedItems.Add(itemId);
            }
        }

        private static Texture2D ExtractSpriteTexture(Sprite sprite)
        {
            try
            {
                Rect rect = sprite.rect;
                int width = (int)rect.width;
                int height = (int)rect.height;

                if (!sprite.texture.isReadable)
                {
                    return CopyTextureViaRenderTexture(sprite);
                }

                Texture2D extracted = new Texture2D(width, height, TextureFormat.RGBA32, false);
                Color[] pixels = sprite.texture.GetPixels((int)rect.x, (int)rect.y, width, height);
                extracted.SetPixels(pixels);
                extracted.Apply();
                return extracted;
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[IconCache] Error extracting sprite texture: {ex.Message}");
                return null;
            }
        }

        private static Texture2D CopyTextureViaRenderTexture(Sprite sprite)
        {
            RenderTexture rt = null;
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                Rect rect = sprite.rect;
                int width = (int)rect.width;
                int height = (int)rect.height;

                rt = RenderTexture.GetTemporary(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(sprite.texture, rt);

                RenderTexture.active = rt;

                Texture2D extracted = new Texture2D(width, height, TextureFormat.RGBA32, false);
                extracted.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
                extracted.Apply();

                return extracted;
            }
            catch (Exception ex)
            {
                _log?.LogDebug($"[IconCache] Error copying texture via RenderTexture: {ex.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static Texture2D CreateFallbackTexture()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size);
            Color bgColor = new Color(0.3f, 0.3f, 0.4f, 0.8f);
            Color borderColor = new Color(0.5f, 0.5f, 0.6f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (x == 0 || x == size - 1 || y == 0 || y == size - 1)
                        tex.SetPixel(x, y, borderColor);
                    else
                        tex.SetPixel(x, y, bgColor);
                }
            }
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Clear the cache. Resets initialized state so next Initialize runs fully.
        /// </summary>
        public static void Clear()
        {
            foreach (var kvp in _iconCache)
            {
                if (kvp.Value.OwnsTexture && kvp.Value.Texture != null)
                    UnityEngine.Object.Destroy(kvp.Value.Texture);
            }
            _iconCache.Clear();
            _loadingItems.Clear();
            _failedItems.Clear();
            _initialized = false;
            _iconsLoaded = false;
            _pendingPreloadItemIds = null;
        }

        /// <summary>
        /// Get cache statistics.
        /// </summary>
        public static (int loaded, int loading, int failed) GetStats()
        {
            return (_iconCache.Count, _loadingItems.Count, _failedItems.Count);
        }

        /// <summary>
        /// Log current cache status.
        /// </summary>
        public static void LogStatus()
        {
            var stats = GetStats();
            _log?.LogInfo($"[IconCache] Loaded: {stats.loaded}, Loading: {stats.loading}, Failed: {stats.failed}");
        }
    }
}
