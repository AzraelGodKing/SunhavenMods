using System.Collections.Generic;
using SunhavenMods.Shared;
using UnityEngine;
using UnityEngine.UI;
using Wish;

namespace SunHavenMuseumUtilityTracker.UI
{
    /// <summary>
    /// Small corner badge for undonated museum items on shop/gift UGUI slots.
    /// </summary>
    internal static class MuseumWishlistBadgeHost
    {
        private const string BadgeObjectName = "SMUT_MuseumWishlistBadge";
        private static readonly Color BadgeFill = new Color(0.85f, 0.55f, 0.25f, 0.95f);
        private static readonly Color BadgeRing = new Color(0.75f, 0.60f, 0.25f, 1f);
        private static readonly Color LabelColor = new Color(0.18f, 0.14f, 0.10f, 1f);

        private static readonly Dictionary<ItemImage, BadgeView> ItemImageBadges = new Dictionary<ItemImage, BadgeView>();
        private static readonly Dictionary<ItemIcon, BadgeView> ItemIconBadges = new Dictionary<ItemIcon, BadgeView>();

        private static Sprite _badgeSprite;
        private static Font _badgeFont;
        private static string _cachedBadgeLabel;
        private static string _cachedBadgeLocaleKey;

        internal static void UpdateItemImage(ItemImage itemImage)
        {
            if (itemImage == null)
                return;

            if (!MuseumWishlistService.ShouldDecorateShopItem(itemImage))
            {
                HideItemImage(itemImage);
                return;
            }

            int gameItemId = MuseumWishlistService.GetGameItemId(itemImage);
            UpdateBadge(ItemImageBadges, itemImage, itemImage.transform, gameItemId);
        }

        internal static void HideItemImage(ItemImage itemImage)
        {
            if (itemImage == null)
                return;

            if (ItemImageBadges.TryGetValue(itemImage, out BadgeView badge))
            {
                badge.Destroy();
                ItemImageBadges.Remove(itemImage);
            }
        }

        internal static void UpdateItemIcon(ItemIcon itemIcon)
        {
            if (itemIcon == null)
                return;

            if (!MuseumWishlistService.ShouldDecorateGiftIcon(itemIcon))
            {
                HideItemIcon(itemIcon);
                return;
            }

            int gameItemId = MuseumWishlistService.GetGameItemId(itemIcon);
            UpdateBadge(ItemIconBadges, itemIcon, itemIcon.transform, gameItemId);
        }

        internal static void HideItemIcon(ItemIcon itemIcon)
        {
            if (itemIcon == null)
                return;

            if (ItemIconBadges.TryGetValue(itemIcon, out BadgeView badge))
            {
                badge.Destroy();
                ItemIconBadges.Remove(itemIcon);
            }
        }

        internal static void RefreshAllLabels()
        {
            _cachedBadgeLabel = null;
            MuseumWishlistTooltipHelper.RefreshTooltipCache();
            PruneDestroyed(ItemImageBadges);
            PruneDestroyed(ItemIconBadges);
            foreach (var badge in ItemImageBadges.Values)
                badge.RefreshLabel();
            foreach (var badge in ItemIconBadges.Values)
                badge.RefreshLabel();
        }

        private static void PruneDestroyed<T>(Dictionary<T, BadgeView> cache)
            where T : Object
        {
            if (cache.Count == 0)
                return;

            List<T> dead = null;
            foreach (var kvp in cache)
            {
                if (kvp.Key == null || kvp.Value == null)
                {
                    dead ??= new List<T>();
                    dead.Add(kvp.Key);
                    kvp.Value?.Destroy();
                }
            }

            if (dead == null)
                return;

            foreach (var key in dead)
                cache.Remove(key);
        }

        private static void UpdateBadge<T>(Dictionary<T, BadgeView> cache, T key, Transform parent, int gameItemId)
            where T : Object
        {
            if (parent == null)
                return;

            bool show = MuseumWishlistService.NeedsMuseumBadge(gameItemId);

            // Avoid allocating/updating badge GameObjects when the slot does not need one.
            if (!cache.TryGetValue(key, out BadgeView badge) || badge == null)
            {
                if (!show)
                    return;
                badge = CreateBadge(parent);
                cache[key] = badge;
            }

            // Skip work when item id + visibility are unchanged (LateUpdate runs every frame).
            if (badge.Matches(gameItemId, show))
                return;

            badge.Apply(gameItemId, show);
        }

        private static BadgeView CreateBadge(Transform parent)
        {
            var root = new GameObject(BadgeObjectName, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, 0f);
            rootRect.sizeDelta = new Vector2(14f, 14f);
            root.transform.SetAsLastSibling();

            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGo.transform.SetParent(root.transform, false);
            var bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            var bgImage = bgGo.GetComponent<Image>();
            bgImage.sprite = GetBadgeSprite();
            bgImage.type = Image.Type.Simple;
            bgImage.color = Color.white;
            bgImage.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelGo.transform.SetParent(root.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelGo.GetComponent<Text>();
            label.font = GetBadgeFont();
            label.fontSize = 9;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = LabelColor;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            return new BadgeView(root, label);
        }

        private static Sprite GetBadgeSprite()
        {
            if (_badgeSprite != null)
                return _badgeSprite;

            var texture = new Texture2D(12, 12, TextureFormat.RGBA32, false);
            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    float dx = x - 5.5f;
                    float dy = y - 5.5f;
                    float distSq = dx * dx + dy * dy;
                    if (distSq <= 20f)
                        texture.SetPixel(x, y, BadgeFill);
                    else if (distSq <= 30f)
                        texture.SetPixel(x, y, BadgeRing);
                    else
                        texture.SetPixel(x, y, Color.clear);
                }
            }
            texture.Apply();
            _badgeSprite = Sprite.Create(texture, new Rect(0f, 0f, 12f, 12f), new Vector2(0.5f, 0.5f), 100f);
            return _badgeSprite;
        }

        private static Font GetBadgeFont()
        {
            if (_badgeFont != null)
                return _badgeFont;

            _badgeFont = Resources.GetBuiltinResource<Font>("Arial.ttf")
                         ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _badgeFont;
        }

        private static string GetBadgeLabel()
        {
            const string key = "smut.wishlist.badge";
            if (_cachedBadgeLabel != null && _cachedBadgeLocaleKey == key)
                return _cachedBadgeLabel;

            _cachedBadgeLocaleKey = key;
            string localized = ModLocalization.T(key);
            if (string.IsNullOrWhiteSpace(localized))
                localized = "M";

            _cachedBadgeLabel = localized.Length <= 2 ? localized : localized.Substring(0, 1).ToUpperInvariant();
            return _cachedBadgeLabel;
        }

        private sealed class BadgeView
        {
            private readonly GameObject _root;
            private readonly Text _label;
            private int _lastGameItemId = int.MinValue;
            private bool _lastVisible;

            internal BadgeView(GameObject root, Text label)
            {
                _root = root;
                _label = label;
                if (_label != null)
                    _label.text = GetBadgeLabel();
            }

            internal bool Matches(int gameItemId, bool visible) =>
                _lastGameItemId == gameItemId && _lastVisible == visible;

            internal void Apply(int gameItemId, bool visible)
            {
                _lastGameItemId = gameItemId;
                _lastVisible = visible;
                SetVisible(visible);
            }

            internal void SetVisible(bool visible)
            {
                if (_root != null && _root.activeSelf != visible)
                    _root.SetActive(visible);
            }

            internal void RefreshLabel()
            {
                if (_label != null)
                    _label.text = GetBadgeLabel();
            }

            internal void Destroy()
            {
                if (_root != null)
                    UnityEngine.Object.Destroy(_root);
            }
        }
    }
}
