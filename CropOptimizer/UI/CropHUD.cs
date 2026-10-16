using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using CropOptimizer.Config;
using CropOptimizer.Data;
using SunhavenMods.Shared;
using UnityEngine;
using UnityEngine.UI;
using Wish;

namespace CropOptimizer.UI
{
    /// <summary>
    /// Sun Haven-styled HUD runner. Owns a persistent ScreenSpaceOverlay canvas that hosts the
    /// summary panel (draggable) and the hover tooltip card. Runs on <see cref="PersistentRunnerBase"/>
    /// so it survives scene loads; the UI is recreated on game/menu transitions so it can't leak or
    /// reference stale objects after the player returns to the main menu.
    /// </summary>
    internal sealed class CropHUD : PersistentRunnerBase
    {
        private CropForecast _forecast;
        private Canvas _canvas;
        private GameObject _canvasGo;
        private CropHudView _hudView;
        private CropTooltipView _tooltipView;

        private float _scale = 1f;
        private bool _isVisible = true;
        private float _initialX = 24f;
        private float _initialY = 80f;
        private bool _hasInitialPlacement;

        private ConfigEntry<bool> _hoverTooltipEnabled;
        private ConfigEntry<float> _hoverTooltipMaxWorldDistance;
        private CropOptimizerConfig _config;
        private GameSelectionHighlightRenderer _fieldHighlights;
        private float _nextHighlightScanTime;
        private IReadOnlyList<CropHighlightTarget> _lastHighlightTargets = System.Array.Empty<CropHighlightTarget>();

        private Component _tooltipContentCrop;
        private TooltipContent _tooltipContentCache;
        private float _nextTooltipContentRebuildTime;

        /// <summary>HUD label refresh — avoids TMP mesh rebuild every frame when counts are unchanged.</summary>
        private int _lastHudTrackedCount = -1;

        private int _lastHudProjectedGold = int.MinValue;
        private int _lastHudProjectedOrbs = int.MinValue;
        private int _lastHudProjectedTickets = int.MinValue;

        /// <summary>Matches what the HUD toggle currently shows — skip redundant TMP/sprite work until config or visibility changes.</summary>
        private bool? _lastHudTooltipShownInUi;

        /// <summary>How often to scan the scene and backfill forecast rows for crops that loaded before Harmony fired.</summary>
        private const float ForecastReconcileSeconds = 1.5f;

        /// <summary>Tooltip rows use reflection and tile probes — limit rebuild rate while the pointer stays on one crop.</summary>
        private const float TooltipContentRefreshSeconds = 0.22f;

        private float _nextCropScanTime;

        /// <summary>Invoked when the player drags the HUD; parent saves X/Y to config (pixels from top-left).</summary>
        public event Action<float, float> PlacementChanged;

        protected override string RunnerName => "CropHUD";

        public void Initialize(CropForecast forecast)
        {
            _forecast = forecast;
        }

        public void SetHoverConfig(ConfigEntry<bool> enabled, ConfigEntry<float> maxWorldDistance)
        {
            _hoverTooltipEnabled = enabled;
            _hoverTooltipMaxWorldDistance = maxWorldDistance;
        }

        public void SetHighlightConfig(CropOptimizerConfig config)
        {
            _config = config;
        }

        public void SetPlacement(float x, float y)
        {
            _initialX = x;
            _initialY = y;
            _hasInitialPlacement = true;
            _hudView?.SetPlacement(x, y);
        }

        public void SetScale(float scale)
        {
            _scale = Mathf.Clamp(scale, 0.5f, 2.5f);
            _hudView?.SetScale(_scale);
            _tooltipView?.SetScale(_scale);
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            _hudView?.SetVisible(visible && IsCharacterSessionActive());
        }

        public void RefreshLocalization()
        {
            if (_hudView == null)
                return;
            bool tooltipCfg = _hoverTooltipEnabled != null && _hoverTooltipEnabled.Value;
            _hudView.RefreshLocalization(tooltipCfg);
            _lastHudTooltipShownInUi = null;
        }

        private static bool IsCharacterSessionActive()
        {
            if (!SceneHelpers.IsInGame())
                return false;
            try
            {
                var gs = GameSave.Instance;
                if (gs == null) return false;
                var save = gs.CurrentSave;
                if (save == null) return false;
                return save.characterData != null;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[CropHUD] Failed to determine character session state: {ex.Message}");
                return false;
            }
        }

        private void EnsureCanvas()
        {
            if (_canvas != null && _canvasGo != null) return;

            _canvasGo = new GameObject("CropOptimizer_HUDCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(_canvasGo);

            _canvas = _canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5000; // above most game HUD, below modal dialogs

            var scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _hudView = new CropHudView(_canvasGo.transform);
            _hudView.PlacementChanged += (x, y) => PlacementChanged?.Invoke(x, y);
            _hudView.TooltipToggleClicked += OnTooltipToggleClicked;
            _hudView.SetScale(_scale);
            if (_hasInitialPlacement) _hudView.SetPlacement(_initialX, _initialY);
            _hudView.SetVisible(_isVisible);
            _hudView.SetTooltipEnabled(_hoverTooltipEnabled != null && _hoverTooltipEnabled.Value);

            _tooltipView = new CropTooltipView(_canvasGo.transform);
            _tooltipView.SetScale(_scale);
            _tooltipView.SetVisible(false);

            _fieldHighlights = new GameSelectionHighlightRenderer();
            _fieldHighlights.EnsureCreated(_canvasGo.transform);
        }

        private void RebuildCanvas()
        {
            _fieldHighlights?.Destroy();
            _fieldHighlights = null;

            if (_canvasGo != null)
            {
                UnityEngine.Object.Destroy(_canvasGo);
                _canvasGo = null;
                _canvas = null;
                _hudView = null;
                _tooltipView = null;
            }
            EnsureCanvas();
        }

        protected override void OnUpdate()
        {
            // Toggle lives here (not Plugin.Update) so F3 still works after a
            // scene change destroys the plugin MonoBehaviour. CropHUD is a
            // PersistentRunnerBase and keeps the ConfigEntry reference.
            HandleHudToggle();

            if (_forecast == null) return;
            EnsureCanvas();

            bool sessionLive = IsCharacterSessionActive();

            if (sessionLive)
            {
                float now = Time.unscaledTime;
                if (now >= _nextCropScanTime)
                {
                    _nextCropScanTime = now + ForecastReconcileSeconds;
                    CropSceneCache.GetCrops(ForecastReconcileSeconds);
                }
            }

            // Summary panel
            if (_hudView != null)
            {
                bool showHud = _isVisible && sessionLive;
                _hudView.SetVisible(showHud);
                if (showHud)
                {
                    var snap = _forecast.Snapshot();
                    int count = snap.Count;
                    CropShopValue projected = _forecast.GetProjectedShopValue();
                    if (count != _lastHudTrackedCount
                        || projected.Gold != _lastHudProjectedGold
                        || projected.Orbs != _lastHudProjectedOrbs
                        || projected.Tickets != _lastHudProjectedTickets)
                    {
                        _lastHudTrackedCount = count;
                        _lastHudProjectedGold = projected.Gold;
                        _lastHudProjectedOrbs = projected.Orbs;
                        _lastHudProjectedTickets = projected.Tickets;
                        _hudView.UpdateStats(count, projected);
                    }

                    bool tooltipCfg = _hoverTooltipEnabled != null && _hoverTooltipEnabled.Value;
                    if (!_lastHudTooltipShownInUi.HasValue || tooltipCfg != _lastHudTooltipShownInUi.Value)
                    {
                        _lastHudTooltipShownInUi = tooltipCfg;
                        _hudView.SetTooltipEnabled(tooltipCfg);
                    }
                }
                else
                {
                    _lastHudTrackedCount = -1;
                    _lastHudProjectedGold = int.MinValue;
                    _lastHudProjectedOrbs = int.MinValue;
                    _lastHudProjectedTickets = int.MinValue;
                    _lastHudTooltipShownInUi = null;
                }
            }

            // Hover tooltip
            UpdateHoverTooltip(sessionLive);
            UpdateFieldHighlights(sessionLive);
        }

        private void HandleHudToggle()
        {
            if (_config?.ToggleHudKey == null)
                return;

            if (TextInputFocusGuard.ShouldDeferModHotkeys(Plugin.Log))
                return;

            KeyCode key = _config.ToggleHudKey.Value;
            if (key != KeyCode.None && Input.GetKeyDown(key))
                SetVisible(!_isVisible);
        }

        private void UpdateFieldHighlights(bool sessionLive)
        {
            if (_fieldHighlights == null || _config == null)
                return;

            if (!sessionLive
                || (!_config.HighlightDryTiles.Value
                    && !_config.HighlightUnfertilizedTiles.Value
                    && !_config.HighlightUninfusedManaCrops.Value))
            {
                _fieldHighlights.SetVisible(false);
                return;
            }

            bool toolGate = _config.HighlightOnlyWhenHoldingTool.Value;
            bool requireMouse = _config.HighlightRequireMouseButton.Value;
            bool mouseDown = Input.GetMouseButton(0);

            bool showDry = _config.HighlightDryTiles.Value
                && (!toolGate || HeldItemProbe.IsWateringCanSelected())
                && (!toolGate || !requireMouse || mouseDown);

            bool showFertilizer = _config.HighlightUnfertilizedTiles.Value
                && (!toolGate || HeldItemProbe.IsFertilizerSelected())
                && (!toolGate || !requireMouse || mouseDown);

            bool showMana = _config.HighlightUninfusedManaCrops.Value;

            if (!showDry && !showFertilizer && !showMana)
            {
                _fieldHighlights.SetVisible(false);
                return;
            }

            _fieldHighlights.SetVisible(true);
            float now = Time.unscaledTime;
            float interval = _config.HighlightRefreshSeconds?.Value ?? 0.35f;
            if (now >= _nextHighlightScanTime || _lastHighlightTargets.Count == 0)
            {
                _nextHighlightScanTime = now + interval;
                _lastHighlightTargets = CropFieldHighlightScanner.Scan(showDry, showFertilizer, showMana);
            }

            _fieldHighlights.Sync(_lastHighlightTargets);
        }

        private void OnTooltipToggleClicked()
        {
            if (_hoverTooltipEnabled == null) return;
            _hoverTooltipEnabled.Value = !_hoverTooltipEnabled.Value;
            _lastHudTooltipShownInUi = _hoverTooltipEnabled.Value;
            _hudView?.SetTooltipEnabled(_hoverTooltipEnabled.Value);
            // BepInEx writes config changes to disk lazily; ConfigFile.Save forces a flush so
            // the new value survives a game restart.
            try
            {
                _hoverTooltipEnabled.ConfigFile?.Save();
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogDebug($"[CropHUD] Failed to flush hover-tooltip config change: {ex.Message}");
            }
        }

        private void UpdateHoverTooltip(bool sessionLive)
        {
            if (_tooltipView == null) return;
            if (!sessionLive || _hoverTooltipEnabled == null || !_hoverTooltipEnabled.Value)
            {
                ClearTooltipContentState();
                _tooltipView.SetVisible(false);
                return;
            }

            // Don't pop the tooltip while the mouse is over our own HUD panel.
            if (_hudView != null && _hudView.IsVisible)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(_hudView.Rect, Input.mousePosition))
                {
                    ClearTooltipContentState();
                    _tooltipView.SetVisible(false);
                    return;
                }
            }

            if (!CropSceneCache.HasActiveSceneCrops(CropHoverQuery.CropCacheRefreshSeconds))
            {
                ClearTooltipContentState();
                _tooltipView.SetVisible(false);
                return;
            }

            Camera cam = CropHoverQuery.ResolveGameplayCamera();
            if (cam == null)
            {
                ClearTooltipContentState();
                _tooltipView.SetVisible(false);
                return;
            }

            float maxDist = _hoverTooltipMaxWorldDistance != null ? _hoverTooltipMaxWorldDistance.Value : 5f;
            if (!CropHoverQuery.TryGetClosestCropNearMouse(cam, maxDist, out Component crop))
            {
                ClearTooltipContentState();
                _tooltipView.SetVisible(false);
                return;
            }

            float now = Time.unscaledTime;
            bool cropChanged = crop != _tooltipContentCrop;
            bool needRebuild = cropChanged || _tooltipContentCache == null || now >= _nextTooltipContentRebuildTime;
            if (needRebuild)
            {
                _tooltipContentCache = CropHoverQuery.BuildTooltipContent(crop, _forecast);
                _tooltipContentCrop = crop;
                _nextTooltipContentRebuildTime = now + TooltipContentRefreshSeconds;
            }

            if (_tooltipContentCache == null)
            {
                ClearTooltipContentState();
                _tooltipView.SetVisible(false);
                return;
            }

            _tooltipView.SetVisible(true);
            // ApplyContent touches every visible TMP row + may ForceMeshUpdate on extras — only when
            // data actually refreshed (crop/timer), not every frame while the mouse sits on one crop.
            if (needRebuild)
                _tooltipView.ApplyContent(_tooltipContentCache);
            _tooltipView.SetScreenPosition(Input.mousePosition);
        }

        private void ClearTooltipContentState()
        {
            _tooltipContentCrop = null;
            _tooltipContentCache = null;
            _nextTooltipContentRebuildTime = 0f;
        }

        protected override void OnGameTransition()
        {
            CropHoverQuery.InvalidateGameplayCameraCache();
            CropHoverQuery.InvalidateHoverAssist();
            CropSceneCache.Invalidate();
            _nextHighlightScanTime = 0f;
            _lastHighlightTargets = System.Array.Empty<CropHighlightTarget>();
            ClearTooltipContentState();
            LocalizationBootstrap.EnsureInitialized(
                PluginInfo.PLUGIN_GUID,
                null,
                Plugin.Log,
                typeof(Plugin).Assembly);
            // Canvas + TMP font should be live now; recreate the UI to pick up fresh styles.
            RebuildCanvas();
            RefreshLocalization();
        }

        protected override void OnMenuTransition()
        {
            CropHoverQuery.InvalidateGameplayCameraCache();
            CropHoverQuery.InvalidateHoverAssist();
            CropSceneCache.Invalidate();
            _nextHighlightScanTime = 0f;
            _lastHighlightTargets = System.Array.Empty<CropHighlightTarget>();
            ClearTooltipContentState();
            _fieldHighlights?.SetVisible(false);
            // Hide but keep canvas around; UI will be rebuilt if the player starts a new session.
            _hudView?.SetVisible(false);
            _tooltipView?.SetVisible(false);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_canvasGo != null)
            {
                UnityEngine.Object.Destroy(_canvasGo);
                _canvasGo = null;
                _canvas = null;
                _hudView = null;
                _tooltipView = null;
            }

            _fieldHighlights?.Destroy();
            _fieldHighlights = null;
        }

        protected override void Log(string message)
        {
            Plugin.Log?.LogDebug(message);
        }

        protected override void LogWarning(string message)
        {
            Plugin.Log?.LogWarning(message);
        }
    }
}
