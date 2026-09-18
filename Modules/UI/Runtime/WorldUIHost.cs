using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Modules.UI
{
    /// <summary>
    /// Projects tracked widgets from world anchors into this UI root each LateUpdate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldUIHost : MonoBehaviour, IWorldUIHost
    {
        private readonly List<Entry> _entries = new List<Entry>(16);
        private RectTransform _root;
        private Canvas _canvas;
        private Camera _worldCamera;

        public RectTransform Root
        {
            get
            {
                EnsureRoot();
                return _root;
            }
        }

        public Camera WorldCamera
        {
            get => _worldCamera;
            set => _worldCamera = value;
        }

        private void Awake()
        {
            EnsureRoot();
        }

        private void LateUpdate()
        {
            TickNow();
        }

        public IWorldUIHandle Track(GameObject widgetInstance, in WorldUiTrackOptions options)
        {
            if (widgetInstance == null)
                throw new ArgumentNullException(nameof(widgetInstance));

            var rt = widgetInstance.transform as RectTransform;
            if (rt == null)
                rt = widgetInstance.AddComponent<RectTransform>();

            return Track(rt, options);
        }

        public IWorldUIHandle Track(RectTransform widget, in WorldUiTrackOptions options)
        {
            if (widget == null)
                throw new ArgumentNullException(nameof(widget));

            EnsureRoot();
            PrepareWidget(widget);

            widget.SetParent(_root, false);
            widget.gameObject.SetActive(true);

            var entry = new Entry(this, widget, options);
            _entries.Add(entry);
            entry.Apply(force: true);
            return entry;
        }

        public void Untrack(IWorldUIHandle handle)
        {
            if (handle is not Entry entry || !entry.IsValid)
                return;

            _entries.Remove(entry);
            entry.Invalidate(destroyWidget: true);
        }

        public void Clear()
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
                _entries[i].Invalidate(destroyWidget: true);
            _entries.Clear();
        }

        public void TickNow()
        {
            if (_entries.Count == 0)
                return;

            var cam = _worldCamera != null ? _worldCamera : Camera.main;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (!e.IsValid)
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                e.WorldCameraOverride = cam;
                e.Apply(force: false);
            }
        }

        private void EnsureRoot()
        {
            if (_root == null)
            {
                _root = transform as RectTransform;
                if (_root == null)
                    _root = gameObject.AddComponent<RectTransform>();
            }

            _canvas = GetComponentInParent<Canvas>();
            EnsureParentCanvasIsOverlay();
        }

        /// <summary>
        /// UIRoot.World must be Overlay and sort under Normal/Top page canvases.
        /// </summary>
        private void EnsureParentCanvasIsOverlay()
        {
            if (_canvas == null)
                return;

            if (_canvas.renderMode == RenderMode.WorldSpace)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.worldCamera = null;

                var rt = _canvas.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.localScale = Vector3.one;
                    rt.localRotation = Quaternion.identity;
                    rt.anchoredPosition = Vector2.zero;
                    rt.sizeDelta = Vector2.zero;
                }
            }

            // World tracked UI stays under Normal(0)/Top(100).
            _canvas.overrideSorting = true;
            if (_canvas.sortingOrder >= 0)
                _canvas.sortingOrder = -100;
        }


        private static void PrepareWidget(RectTransform widget)
        {
            // Drop nested world canvases; host parent canvas owns rendering.
            var nested = widget.GetComponentsInChildren<Canvas>(true);
            for (var i = 0; i < nested.Length; i++)
            {
                var c = nested[i];
                if (c == null)
                    continue;
                if (c.transform == widget)
                    c.enabled = false;
            }

            var raycasters = widget.GetComponentsInChildren<GraphicRaycaster>(true);
            for (var i = 0; i < raycasters.Length; i++)
            {
                if (raycasters[i] != null)
                    raycasters[i].enabled = false;
            }

            widget.localScale = Vector3.one;
            widget.localRotation = Quaternion.identity;
            widget.anchorMin = new Vector2(0.5f, 0.5f);
            widget.anchorMax = new Vector2(0.5f, 0.5f);
            widget.pivot = new Vector2(0.5f, 1f); // top-center: offset down from feet
            if (widget.sizeDelta.sqrMagnitude < 1f)
                widget.sizeDelta = new Vector2(200f, 40f);
        }


        private void OnDestroy()
        {
            Clear();
        }

        private sealed class Entry : IWorldUIHandle
        {
            private WorldUIHost _host;
            private RectTransform _widget;
            private Transform _anchor;
            private Func<Vector3> _positionProvider;
            private Vector2 _offsetPx;
            private readonly bool _hideOffscreen;
            private readonly bool _hideBehind;
            private bool _visible = true;
            private bool _disposed;

            public Camera WorldCameraOverride;

            public Entry(WorldUIHost host, RectTransform widget, in WorldUiTrackOptions options)
            {
                _host = host;
                _widget = widget;
                _anchor = options.WorldAnchor;
                _positionProvider = options.WorldPositionProvider;
                _offsetPx = options.ScreenOffsetPx;
                _hideOffscreen = options.HideWhenOffscreen;
                _hideBehind = options.HideWhenBehindCamera;
            }

            public RectTransform Widget => _widget;
            public bool IsValid => !_disposed && _widget != null && _host != null;

            public void SetVisible(bool visible)
            {
                _visible = visible;
                if (_widget != null)
                    _widget.gameObject.SetActive(visible);
            }

            public void SetScreenOffset(Vector2 offsetPx) => _offsetPx = offsetPx;

            public void SetWorldAnchor(Transform anchor) => _anchor = anchor;

            public void Dispose()
            {
                if (_disposed)
                    return;
                _host?.Untrack(this);
            }

            public void Invalidate(bool destroyWidget)
            {
                if (_disposed)
                    return;
                _disposed = true;
                if (destroyWidget && _widget != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(_widget.gameObject);
                    else
                        UnityEngine.Object.DestroyImmediate(_widget.gameObject);
                }

                _widget = null;
                _host = null;
                _anchor = null;
                _positionProvider = null;
            }

            public void Apply(bool force)
            {
                if (!IsValid || !_visible)
                    return;

                if (!TryResolveWorld(out var worldPos))
                {
                    SetActive(false);
                    return;
                }

                var cam = WorldCameraOverride != null ? WorldCameraOverride : _host.WorldCamera;
                if (cam == null)
                    cam = Camera.main;
                if (cam == null)
                {
                    SetActive(false);
                    return;
                }

                if (!UICoordUtility.TryWorldToCanvasLocal(
                        cam,
                        _host.Root,
                        _host._canvas,
                        worldPos,
                        out var local,
                        out var inFront))
                {
                    SetActive(false);
                    return;
                }

                if (_hideBehind && !inFront)
                {
                    SetActive(false);
                    return;
                }

                if (!UICoordUtility.TryWorldToScreen(cam, worldPos, out var screen, out _))
                {
                    SetActive(false);
                    return;
                }

                // Apply pixel offset in screen space then convert again for stable px offset.
                screen += _offsetPx;
                Camera eventCam = null;
                if (_host._canvas != null && _host._canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    eventCam = _host._canvas.worldCamera != null ? _host._canvas.worldCamera : cam;

                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _host.Root,
                        screen,
                        eventCam,
                        out local))
                {
                    SetActive(false);
                    return;
                }

                if (_hideOffscreen && !UICoordUtility.IsInsidePixelRect(cam, screen, 64f))
                {
                    SetActive(false);
                    return;
                }

                SetActive(true);
                _widget.anchoredPosition = local;
            }

            private bool TryResolveWorld(out Vector3 world)
            {
                if (_anchor != null)
                {
                    // Inactive/destroyed units must not keep floating widgets.
                    if (!_anchor.gameObject.activeInHierarchy)
                    {
                        world = default;
                        return false;
                    }

                    world = _anchor.position;
                    return true;
                }

                if (_positionProvider != null)
                {
                    world = _positionProvider();
                    return true;
                }

                world = default;
                return false;
            }

            private void SetActive(bool on)
            {
                if (_widget != null && _widget.gameObject.activeSelf != on)
                    _widget.gameObject.SetActive(on);
            }
        }
    }
}
