using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cascade.Modules.UI
{
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private Sprite safeAreaMaskSprite;
        [SerializeField] private Color modalMaskColor = new Color(0f, 0f, 0f, 0.9f);

        private UIRootRuntime _runtime;

        public Sprite SafeAreaMaskSprite => safeAreaMaskSprite;
        public Color ModalMaskColor => modalMaskColor;

        public UIRootRuntime BuildRuntime()
        {
            if (_runtime == null)
                _runtime = new UIRootRuntime(transform, modalMaskColor, safeAreaMaskSprite);
            return _runtime;
        }

        private void OnDestroy()
        {
            _runtime?.Dispose();
            _runtime = null;
        }
    }

    public sealed class UIRootRuntime : IDisposable
    {
        private readonly Dictionary<UIContextKey, UIContextNodes> _contexts =
            new Dictionary<UIContextKey, UIContextNodes>();
        private readonly Dictionary<UIContextId, WorldUiContext> _worldContexts =
            new Dictionary<UIContextId, WorldUiContext>();
        private readonly List<GameObject> _createdObjects = new List<GameObject>();
        private readonly Transform _normal;
        private readonly Transform _top;
        private readonly Transform _world;
        private readonly Color _modalMaskColor;
        private readonly Sprite _safeAreaMaskSprite;
        private bool _disposed;

        public UIRootRuntime(Transform root, Color modalMaskColor, Sprite safeAreaMaskSprite)
        {
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            _modalMaskColor = modalMaskColor;
            _safeAreaMaskSprite = safeAreaMaskSprite;
            _normal = FindLayer(root, "Normal");
            _top = FindLayer(root, "Top");
            _world = FindLayer(root, "World");
            BuildContexts(_normal, includeGlobal: true);
            BuildContexts(_top, includeGlobal: true);
            BuildWorldContexts(_world);
        }

        // Test helper that matches previous constructor shape.
        public UIRootRuntime(Transform root)
            : this(root, new Color(0f, 0f, 0f, 0.9f), null)
        {
        }

        public Sprite SafeAreaMaskSprite => _safeAreaMaskSprite;

        public bool TryGetContext(UILayer layer, UIContextId context, out UIContextNodes nodes)
        {
            return _contexts.TryGetValue(new UIContextKey(layer, context), out nodes);
        }

        public UIContextNodes GetContext(UILayer layer, UIContextId context)
        {
            if (!TryGetContext(layer, context, out var nodes))
                throw new InvalidOperationException($"UI context is unavailable: {layer}/{context}");
            return nodes;
        }

        public bool TryGetWorldContext(UIContextId context, out WorldUiContext nodes)
        {
            return _worldContexts.TryGetValue(context, out nodes);
        }

        public IWorldUIHost GetWorldUIHost(UIContextId context)
        {
            if (!_worldContexts.TryGetValue(context, out var nodes) || nodes.Host == null)
                throw new InvalidOperationException($"World UI host unavailable: {context}");
            return nodes.Host;
        }

        public void SetContextActive(UIContextId context, bool active)
        {
            if (context == UIContextId.Global)
                return;

            foreach (var pair in _contexts)
            {
                if (pair.Key.Context == context)
                    pair.Value.Root.gameObject.SetActive(active);
            }

            if (_worldContexts.TryGetValue(context, out var world))
                world.Root.gameObject.SetActive(active);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            foreach (var pair in _worldContexts)
                pair.Value.Host?.Clear();

            for (var i = _createdObjects.Count - 1; i >= 0; i--)
            {
                if (_createdObjects[i] != null)
                    DestroyObject(_createdObjects[i]);
            }

            _createdObjects.Clear();
            _contexts.Clear();
            _worldContexts.Clear();
        }

        private void BuildContexts(Transform layerRoot, bool includeGlobal)
        {
            if (includeGlobal)
                BuildContext(layerRoot, UIContextId.Global);
            BuildContext(layerRoot, UIContextId.Main);
            BuildContext(layerRoot, UIContextId.MiniGame);
        }

        private void BuildWorldContexts(Transform layerRoot)
        {
            // World is for projected widgets only — no Screens/Modals/Overlays page stacks.
            BuildWorldContext(layerRoot, UIContextId.Main);
            BuildWorldContext(layerRoot, UIContextId.MiniGame);
        }

        private void BuildWorldContext(Transform layerRoot, UIContextId context)
        {
            var contextObject = CreateObject(context.ToString(), layerRoot);
            var hostGo = CreateObject("WorldUIHost", contextObject.transform);
            var host = hostGo.AddComponent<WorldUIHost>();
            _worldContexts.Add(context, new WorldUiContext(contextObject.transform, host));
        }

        private void BuildContext(Transform layerRoot, UIContextId context)
        {
            var contextObject = CreateObject(context.ToString(), layerRoot);
            var screens = CreateObject("Screens", contextObject.transform);
            var modalMask = CreateModalMask(contextObject.transform);
            var modals = CreateObject("Modals", contextObject.transform);
            var overlays = CreateObject("Overlays", contextObject.transform);
            _contexts.Add(
                new UIContextKey(GetLayer(layerRoot), context),
                new UIContextNodes(
                    contextObject.transform,
                    screens.transform,
                    modalMask,
                    modals.transform,
                    overlays.transform));
        }


        private GameObject CreateModalMask(Transform parent)
        {
            var value = new GameObject("ModalMask", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)value.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            var image = value.GetComponent<Image>();
            image.color = _modalMaskColor;
            image.raycastTarget = true;
            value.SetActive(false);
            _createdObjects.Add(value);
            return value;
        }

        private UILayer GetLayer(Transform layerRoot)
        {
            if (layerRoot == _normal)
                return UILayer.Normal;
            if (layerRoot == _top)
                return UILayer.Top;
            return UILayer.World;
        }

        private GameObject CreateObject(string name, Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)value.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            _createdObjects.Add(value);
            return value;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static Transform FindLayer(Transform root, string name)
        {
            var layer = root.Find(name);
            if (layer == null)
                throw new InvalidOperationException($"UIRoot is missing layer: {name}");
            return layer;
        }

        private static void DestroyObject(GameObject value)
        {
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }

        internal readonly struct UIContextKey : IEquatable<UIContextKey>
        {
            private readonly UILayer _layer;
            private readonly UIContextId _context;

            public UIContextKey(UILayer layer, UIContextId context)
            {
                _layer = layer;
                _context = context;
            }

            public UIContextId Context => _context;

            public bool Equals(UIContextKey other)
            {
                return _layer == other._layer && _context == other._context;
            }

            public override bool Equals(object obj)
            {
                return obj is UIContextKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return ((int)_layer * 397) ^ (int)_context;
            }
        }
    }

    public sealed class UIContextNodes
    {
        private readonly Image _modalMaskImage;
        private Action _maskClickHandler;

        internal UIContextNodes(
            Transform root,
            Transform screens,
            GameObject modalMask,
            Transform modals,
            Transform overlays)
        {
            Root = root;
            Screens = screens;
            ModalMask = modalMask.transform;
            Modals = modals;
            Overlays = overlays;
            _modalMaskImage = modalMask.GetComponent<Image>();
            var trigger = modalMask.GetComponent<EventTrigger>() ?? modalMask.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ => _maskClickHandler?.Invoke());
            trigger.triggers.Add(entry);
        }

        public Transform Root { get; }
        public Transform Screens { get; }
        public Transform ModalMask { get; }
        public Transform Modals { get; }
        public Transform Overlays { get; }

        public void SetModalMask(bool visible, bool raycast, bool visual, Color color, Action onClick)
        {
            ModalMask.gameObject.SetActive(visible);
            if (!visible)
            {
                _maskClickHandler = null;
                return;
            }

            _modalMaskImage.raycastTarget = raycast;
            _modalMaskImage.color = visual ? color : new Color(0f, 0f, 0f, 0f);
            _maskClickHandler = onClick;
        }
    }

    /// <summary>World layer context: no page stacks, only projected widget host.</summary>
    public sealed class WorldUiContext
    {
        public WorldUiContext(Transform root, IWorldUIHost host)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public Transform Root { get; }
        public IWorldUIHost Host { get; }
    }

}
