using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using HavensMirror.Config;
using HavensMirror.Gallery;
using HavensMirror.Look;
using UnityEngine;
using UnityEngine.UI;

namespace HavensMirror.Dialogue
{
    /// <summary>
    /// Draws the player's gallery bust on the LEFT half of the dialogue screen.
    /// Vanilla <c>DialogueController._bust</c> lives under <c>BustOffset</c> at a right-biased
    /// anchored X (~512). Parenting under that node (or mirroring NPC X) stacks the player on
    /// the NPC. Placement is always under the root Canvas in canvas-local space: fixed left X,
    /// Y from NPC world center; height matches NPC rect height with width from sprite aspect
    /// (full-body PNGs — never SetNativeSize / never mask-crop).
    /// </summary>
    public sealed class PlayerBustHud : MonoBehaviour
    {
        private const float LeftMargin = 24f;
        private const float LeftFraction = 0.15f;
        /// <summary>Typical NPC dialogue bust height when no NPC Image is available.</summary>
        private const float FallbackBustHeight = 250f;
        private const float FallbackAspect = 0.83f;
        /// <summary>Longer than vanilla's 0.02s CancelDialogue hide delay.</summary>
        private const float HideCheckDelay = 0.1f;
        /// <summary>Never taller than this share of the canvas, whatever the NPC rect reports.</summary>
        private const float MaxHeightFraction = 0.6f;
        /// <summary>How long Present waits for the NPC bust to appear before giving up.</summary>
        private const float NpcWaitSeconds = 3f;

        private static PlayerBustHud _instance;
        private static bool _loggedLayout;

        public static PlayerBustHud Instance => _instance;

        private Image _image;
        private RectTransform _rect;
        private bool _ready;
        private Image _npcBust;
        private Coroutine _relayoutRoutine;
        private Coroutine _hideRoutine;

        /// <summary>
        /// Attach to the DialogueController host (preferred) or any dialogue UI root.
        /// </summary>
        public static PlayerBustHud EnsureOn(GameObject host)
        {
            if (host == null)
                return null;

            if (_instance != null && _instance.gameObject != null)
                return _instance;

            var existing = host.GetComponent<PlayerBustHud>();
            if (existing != null)
            {
                _instance = existing;
                return existing;
            }

            var hud = host.AddComponent<PlayerBustHud>();
            _instance = hud;
            return hud;
        }

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Cache the vanilla NPC bust Image (DialogueController._bust) used as the size/Y template.
        /// </summary>
        public void BindNpcBust(Image npcBust)
        {
            if (npcBust == null)
                return;

            _npcBust = npcBust;
        }

        private bool TryResolveNpcBust()
        {
            if (_npcBust != null)
                return true;

            try
            {
                Type dialogueType = AccessTools.TypeByName("Wish.DialogueController")
                                     ?? AccessTools.TypeByName("DialogueController");
                if (dialogueType == null)
                    return false;

                object controller = null;
                FieldInfo instanceField = AccessTools.Field(dialogueType, "Instance");
                if (instanceField != null)
                    controller = instanceField.GetValue(null);

                if (controller == null)
                {
                    PropertyInfo instanceProp = AccessTools.Property(dialogueType, "Instance");
                    if (instanceProp != null)
                        controller = instanceProp.GetValue(null, null);
                }

                if (controller == null)
                    return false;

                FieldInfo bustField = AccessTools.Field(dialogueType, "_bust")
                                      ?? AccessTools.Field(dialogueType, "bust")
                                      ?? AccessTools.Field(dialogueType, "Bust");
                if (bustField != null)
                    _npcBust = bustField.GetValue(controller) as Image;

                if (_npcBust == null)
                {
                    PropertyInfo bustProp = AccessTools.Property(dialogueType, "Bust");
                    if (bustProp != null)
                        _npcBust = bustProp.GetValue(controller, null) as Image;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[Dialogue] Resolve NPC bust: {ex.Message}");
            }

            return _npcBust != null;
        }

        private void EnsureVisual()
        {
            if (_ready && _image != null && _rect != null)
                return;

            try
            {
                TryResolveNpcBust();

                RectTransform layoutParent = ResolveLayoutParent();
                Transform parent = layoutParent != null ? (Transform)layoutParent : transform;

                var go = new GameObject("HavensMirror_PlayerBust", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                _image = go.GetComponent<Image>();
                _rect = go.GetComponent<RectTransform>();

                _image.type = Image.Type.Simple;
                _image.preserveAspect = true;
                _image.raycastTarget = false;
                _image.color = Color.white;
                // Never mask/crop full-body gallery PNGs.
                var mask = go.GetComponent<RectMask2D>();
                if (mask != null)
                    UnityEngine.Object.Destroy(mask);
                go.AddComponent<BustVisibilityWatch>().Hud = this;
                go.SetActive(false);

                PlaceOnLeftHalf();
                _ready = _image != null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"[Dialogue] PlayerBustHud setup failed: {ex.Message}");
                _ready = false;
            }
        }

        /// <summary>
        /// Root canvas (or DialogueController) — never BustOffset / never NPC's parent.
        /// </summary>
        private RectTransform ResolveLayoutParent()
        {
            TryResolveNpcBust();

            Canvas canvas = null;
            if (_npcBust != null)
                canvas = _npcBust.GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas = canvas.rootCanvas != null ? canvas.rootCanvas : canvas;

            RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect != null && !IsBustOffset(canvasRect))
                return canvasRect;

            // DialogueController host (this component) — still never BustOffset.
            if (transform is RectTransform hostRect && !IsBustOffset(hostRect) && !IsUnderBustOffset(hostRect))
                return hostRect;

            // Walk up from NPC past BustOffset to a safe ancestor.
            if (_npcBust != null)
            {
                Transform t = _npcBust.transform.parent;
                while (t != null)
                {
                    if (t is RectTransform rt && !IsBustOffset(rt) && !string.Equals(t.name, "Bust", StringComparison.OrdinalIgnoreCase))
                    {
                        // Prefer the first non-offset ancestor that is not the NPC image itself.
                        if (t != _npcBust.transform)
                            return rt;
                    }

                    t = t.parent;
                }
            }

            return transform as RectTransform;
        }

        private static bool IsBustOffset(Transform t)
        {
            return t != null && t.name != null
                   && t.name.IndexOf("BustOffset", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUnderBustOffset(Transform t)
        {
            while (t != null)
            {
                if (IsBustOffset(t))
                    return true;
                t = t.parent;
            }

            return false;
        }

        /// <summary>
        /// Screen-space left placement under root Canvas (never BustOffset).
        /// X from Screen.width * LeftFraction — not from parent rect xMin (the 'UI' node can be
        /// right-biased; left-of-UI still stacks on the NPC). Y/size from NPC only.
        /// </summary>
        private void PlaceOnLeftHalf()
        {
            if (_rect == null || _image == null)
                return;

            TryResolveNpcBust();
            RectTransform npcRect = _npcBust != null ? _npcBust.rectTransform : null;
            RectTransform layoutParent = ResolveLayoutParent();
            Canvas canvas = layoutParent != null ? layoutParent.GetComponentInParent<Canvas>() : null;
            if (canvas != null && canvas.rootCanvas != null)
                canvas = canvas.rootCanvas;

            if (layoutParent != null && _rect.parent != layoutParent)
                _rect.SetParent(layoutParent, false);

            // Center anchors: anchoredPosition is parent-local, filled from screen points below.
            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.localScale = Vector3.one;
            _rect.localRotation = Quaternion.identity;

            Vector2 size = ResolvePlayerSize(npcRect);
            _rect.sizeDelta = size;

            Camera eventCam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCam = canvas.worldCamera;

            // size is in canvas units; screen math below is in pixels. Convert so a large UI scale
            // (canvas scale factor > 1) does not push the bust off the left edge or onto the NPC.
            float halfWidthPx = size.x * 0.5f * CanvasUnitsToPixels(layoutParent, eventCam);
            float screenX = Mathf.Max(Screen.width * LeftFraction, halfWidthPx + LeftMargin);
            // Hard cap: stay in the left 45% of the screen.
            float screenMaxX = Screen.width * 0.45f - halfWidthPx;
            if (screenX > screenMaxX)
                screenX = Mathf.Max(halfWidthPx + LeftMargin, screenMaxX);

            float screenY;
            if (npcRect != null)
            {
                Vector3 npcWorldCenter = npcRect.TransformPoint(npcRect.rect.center);
                Vector2 npcScreen = RectTransformUtility.WorldToScreenPoint(eventCam, npcWorldCenter);
                screenY = npcScreen.y;
            }
            else
            {
                screenY = Screen.height * 0.5f;
            }

            // Keep the whole bust on screen (a stale or oversized NPC rect once pinned it to the bottom).
            float halfHeightPx = size.y * 0.5f * CanvasUnitsToPixels(layoutParent, eventCam);
            if (Screen.height > halfHeightPx * 2f)
                screenY = Mathf.Clamp(screenY, halfHeightPx, Screen.height - halfHeightPx);
            else
                screenY = Screen.height * 0.5f;

            Vector2 screenPoint = new Vector2(screenX, screenY);
            Vector2 localPoint;
            if (layoutParent != null
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    layoutParent, screenPoint, eventCam, out localPoint))
            {
                _rect.anchoredPosition = localPoint;
            }
            else
            {
                // Fallback if conversion fails: left fraction of parent rect (still never NPC.x).
                Rect canvasRect = layoutParent != null
                    ? layoutParent.rect
                    : new Rect(-960f, -540f, 1920f, 1080f);
                float x = canvasRect.xMin + canvasRect.width * LeftFraction;
                float y = 0f;
                if (npcRect != null && layoutParent != null)
                {
                    Vector3 npcWorldCenter = npcRect.TransformPoint(npcRect.rect.center);
                    Vector3 npcLocal = layoutParent.InverseTransformPoint(npcWorldCenter);
                    y = npcLocal.y - canvasRect.center.y;
                }

                _rect.anchoredPosition = new Vector2(x, y);
            }

            _image.type = Image.Type.Simple;
            _image.color = Color.white;
            _image.preserveAspect = true;
            _image.raycastTarget = false;
            _rect.SetAsLastSibling();
        }

        /// <summary>Screen pixels per local unit of <paramref name="space"/> along X (1 when unknown).</summary>
        private static float CanvasUnitsToPixels(RectTransform space, Camera eventCam)
        {
            if (space == null)
                return 1f;

            Vector2 a = RectTransformUtility.WorldToScreenPoint(eventCam, space.TransformPoint(Vector3.zero));
            Vector2 b = RectTransformUtility.WorldToScreenPoint(eventCam, space.TransformPoint(new Vector3(100f, 0f, 0f)));
            float perUnit = Mathf.Abs(b.x - a.x) / 100f;
            return perUnit > 0.0001f ? perUnit : 1f;
        }

        /// <summary>
        /// Match NPC bust height on screen; width from gallery sprite aspect.
        /// Full-body PNGs must not use SetNativeSize (huge pixels → screen crop).
        /// </summary>
        private Vector2 ResolvePlayerSize(RectTransform npcRect)
        {
            float targetHeight = FallbackBustHeight;

            if (npcRect != null)
            {
                // Measure the NPC bust in the player's parent space (root canvas). rect.height is in
                // the NPC's own local units, which differ from canvas units whenever BustOffset or
                // another ancestor is scaled (e.g. the in-game UI scale slider).
                float h = 0f;
                if (_rect != null && _rect.parent is RectTransform parent)
                {
                    Vector3[] corners = new Vector3[4];
                    npcRect.GetWorldCorners(corners);
                    Vector3 local0 = parent.InverseTransformPoint(corners[0]);
                    Vector3 local1 = parent.InverseTransformPoint(corners[1]);
                    h = Mathf.Abs(local1.y - local0.y);
                }

                if (h <= 1f)
                    h = Mathf.Abs(npcRect.rect.height);
                if (h <= 1f)
                    h = Mathf.Abs(npcRect.sizeDelta.y);

                if (h > 1f)
                    targetHeight = h;
            }

            if (_rect != null && _rect.parent is RectTransform space)
            {
                float maxHeight = Mathf.Abs(space.rect.height) * MaxHeightFraction;
                if (maxHeight > 1f && targetHeight > maxHeight)
                    targetHeight = maxHeight;
            }

            float aspect = FallbackAspect;
            if (_image != null && _image.sprite != null)
            {
                Rect sr = _image.sprite.rect;
                if (sr.height > 0.01f)
                    aspect = sr.width / sr.height;
            }

            return new Vector2(targetHeight * aspect, targetHeight);
        }

        /// <summary>True when the vanilla NPC bust is on screen with a sprite and a real size.</summary>
        private bool IsNpcBustShowing()
        {
            if (_npcBust == null || !_npcBust.isActiveAndEnabled || _npcBust.sprite == null)
                return false;

            Rect r = _npcBust.rectTransform.rect;
            return Mathf.Abs(r.width) > 1f && Mathf.Abs(r.height) > 1f;
        }

        /// <summary>
        /// Called every frame while the player bust is visible. Hides it once the NPC bust has been
        /// gone for <see cref="HideCheckDelay"/>, even when the UI closed without CancelDialogue.
        /// </summary>
        private void TickVisibleWatch(ref float npcGoneSince)
        {
            if (_relayoutRoutine != null || _npcBust == null || IsNpcBustShowing())
            {
                npcGoneSince = -1f;
                return;
            }

            if (npcGoneSince < 0f)
            {
                npcGoneSince = Time.unscaledTime;
                return;
            }

            if (Time.unscaledTime - npcGoneSince >= HideCheckDelay)
            {
                npcGoneSince = -1f;
                Plugin.Log?.LogDebug("[Dialogue] NPC portrait closed — hiding player bust.");
                Hide();
            }
        }

        /// <summary>
        /// Lives on the player bust GameObject so it ticks exactly while the bust is visible,
        /// even if the DialogueController host is inactive.
        /// </summary>
        private sealed class BustVisibilityWatch : MonoBehaviour
        {
            public PlayerBustHud Hud;
            private float _npcGoneSince = -1f;

            private void OnEnable()
            {
                _npcGoneSince = -1f;
            }

            private void Update()
            {
                if (Hud == null)
                {
                    gameObject.SetActive(false);
                    return;
                }

                Hud.TickVisibleWatch(ref _npcGoneSince);
            }
        }

        public void Hide()
        {
            CancelPendingHide();
            if (_relayoutRoutine != null)
            {
                StopCoroutine(_relayoutRoutine);
                _relayoutRoutine = null;
            }

            if (_image != null)
                _image.gameObject.SetActive(false);
        }

        /// <summary>
        /// Vanilla hides the NPC bust in a short delayed call that the next PushDialogue
        /// kills, so the bust survives option-to-node transitions. Follow that outcome.
        /// </summary>
        public void HideWhenNpcBustCloses()
        {
            if (!isActiveAndEnabled)
            {
                Hide();
                return;
            }

            CancelPendingHide();
            _hideRoutine = StartCoroutine(HideIfNpcBustClosed());
        }

        private IEnumerator HideIfNpcBustClosed()
        {
            yield return new WaitForSecondsRealtime(HideCheckDelay);
            _hideRoutine = null;
            if (_npcBust == null || !_npcBust.gameObject.activeInHierarchy)
                Hide();
        }

        private void CancelPendingHide()
        {
            if (_hideRoutine == null)
                return;

            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        /// <summary>
        /// Show the player's gallery bust. Vanilla <c>isRefreshBust</c> is intentionally ignored —
        /// NPCAI.Interact always passes true (NPC bust flicker during addressable load).
        /// </summary>
        public void Present(bool vows, bool shore, bool costume)
        {
            if (MirrorOptions.Enabled == null || !MirrorOptions.Enabled.Value)
            {
                Hide();
                return;
            }

            CancelPendingHide();
            EnsureVisual();
            if (!_ready || _image == null)
            {
                Plugin.Log?.LogWarning("[Dialogue] Present: PlayerBustHud image not ready.");
                return;
            }

            Plugin.EnsureGalleryCurrent();
            var shelf = Plugin.Shelf;
            if (shelf == null || !shelf.HasAny)
            {
                Plugin.Log?.LogDebug("[Dialogue] Present: shelf empty — hiding.");
                Hide();
                return;
            }

            LookSlot slot = ForcedLookResolver.ResolveActiveLook(vows, shore, costume);
            Sprite sprite = shelf.Get(slot);
            if (sprite == null)
            {
                Plugin.Log?.LogWarning($"[Dialogue] Present: no sprite for look '{slot}'.");
                Hide();
                return;
            }

            _image.sprite = sprite;
            _image.type = Image.Type.Simple;
            _image.preserveAspect = true;
            _image.color = Color.white;
            // Do not SetNativeSize — gallery PNGs are full-body at large pixel sizes and
            // would overflow the screen. Size is set in PlaceOnLeftHalf from NPC height.
            // Stay hidden until final left placement so we never flash on the NPC side.
            _image.gameObject.SetActive(false);

            if (_relayoutRoutine != null)
                StopCoroutine(_relayoutRoutine);
            _relayoutRoutine = StartCoroutine(PresentAfterNpcSettles(slot, shelf.LoadedFrom));
        }

        private IEnumerator PresentAfterNpcSettles(LookSlot slot, string loadedFrom)
        {
            // Wait for vanilla LoadBust + ActivateBustsNextFrame (size/active). Usually a few frames;
            // a cold addressable load can take longer, so keep waiting (up to NpcWaitSeconds) while
            // the NPC bust is still hidden instead of giving up and showing the player alone.
            float deadline = Time.unscaledTime + NpcWaitSeconds;
            for (int i = 0; ; i++)
            {
                yield return null;
                if (_npcBust == null)
                {
                    if (i >= 45)
                        break;
                    continue;
                }

                if (IsNpcBustShowing() && (_npcBust.rectTransform.sizeDelta.x > 1f || i >= 45))
                    break;
                if (Time.unscaledTime >= deadline)
                    break;
            }

            // One more frame so NPC size/offset stick before we sample size + Y.
            yield return null;

            // Museum donations and similar popups run the bust setter without showing an NPC
            // portrait. With nothing to line up with (and nothing whose close would hide us),
            // the player bust used to appear oversized at the bottom and stay up. Stay hidden.
            if (_npcBust != null && !IsNpcBustShowing())
            {
                Plugin.Log?.LogDebug("[Dialogue] No NPC portrait on screen — player bust stays hidden.");
                _relayoutRoutine = null;
                yield break;
            }

            PlaceOnLeftHalf();
            if (_image != null)
                _image.gameObject.SetActive(true);

            if (!_loggedLayout && _rect != null)
            {
                _loggedLayout = true;
                string parentName = _rect.parent != null ? _rect.parent.name : "(null)";
                string npcParent = _npcBust != null && _npcBust.rectTransform.parent != null
                    ? _npcBust.rectTransform.parent.name
                    : "(null)";
                Vector2 npcPos = _npcBust != null
                    ? _npcBust.rectTransform.anchoredPosition
                    : Vector2.zero;
                float npcH = 0f;
                if (_npcBust != null)
                {
                    npcH = Mathf.Abs(_npcBust.rectTransform.rect.height);
                    if (npcH <= 1f)
                        npcH = Mathf.Abs(_npcBust.rectTransform.sizeDelta.y);
                }

                Vector2 spritePx = Vector2.zero;
                if (_image != null && _image.sprite != null)
                {
                    Rect sr = _image.sprite.rect;
                    spritePx = new Vector2(sr.width, sr.height);
                }

                Vector3 playerScreen = RectTransformUtility.WorldToScreenPoint(null, _rect.position);
                Plugin.Log?.LogInfo(
                    $"[Dialogue] Player bust layout: parent='{parentName}' anchoredPosition={_rect.anchoredPosition} " +
                    $"sizeDelta={_rect.sizeDelta} spritePx={spritePx} npcHeight={npcH} " +
                    $"anchors=({_rect.anchorMin},{_rect.anchorMax}) " +
                    $"screen={playerScreen} npcParent='{npcParent}' npcAnchored={npcPos} " +
                    $"siblingIndex={_rect.GetSiblingIndex()}");
            }

            Plugin.Log?.LogInfo($"[Dialogue] Present player bust '{slot}' from '{loadedFrom}'.");
            _relayoutRoutine = null;
        }
    }
}
