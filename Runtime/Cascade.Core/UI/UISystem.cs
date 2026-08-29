using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Core
{
    public sealed class UISystem : IUISystem
    {
        private const float DefaultCacheTtlSeconds = 10f;
        private static readonly Color ModalMaskColor = new Color(0f, 0f, 0f, 0.9f);

        private readonly IResourceService _resources;
        private readonly UIRootRuntime _root;
        private readonly UIRegistry _registry;
        private readonly ILogService _log;
        private readonly IAssetHandle<GameObject> _rootHandle;
        private readonly GameObject _rootObject;
        private readonly IDisposable _updateRegistration;
        private readonly Dictionary<UIEntryKey, UIEntry> _entries = new Dictionary<UIEntryKey, UIEntry>();
        private readonly Dictionary<UIStackKey, List<UIEntry>> _stacks = new Dictionary<UIStackKey, List<UIEntry>>();
        private readonly Dictionary<UIEntryKey, UniTaskCompletionSource<UIBase>> _inflight =
            new Dictionary<UIEntryKey, UniTaskCompletionSource<UIBase>>();
        private readonly Dictionary<UIEntryKey, GameObject> _overlayShields =
            new Dictionary<UIEntryKey, GameObject>();
        private object _pageContext;
        private UIContextId? _activeContext;
        private bool _disposed;

        public UISystem(
            IResourceService resources,
            UIRootRuntime root,
            UIRegistry registry,
            IUpdateLoop updateLoop = null,
            ILogService log = null)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _log = log;
            _rootHandle = null;
            _rootObject = null;
            if (updateLoop != null)
                _updateRegistration = updateLoop.RegisterUpdate(OnUpdate);
        }

        private UISystem(
            IResourceService resources,
            UIRootRuntime root,
            UIRegistry registry,
            IAssetHandle<GameObject> rootHandle,
            GameObject rootObject,
            IUpdateLoop updateLoop,
            ILogService log)
            : this(resources, root, registry, updateLoop, log)
        {
            _rootHandle = rootHandle;
            _rootObject = rootObject;
        }

        public UIContextId? ActiveContext => _activeContext;

        public static async UniTask<UISystem> CreateAsync(
            IResourceService resources,
            UIRegistry registry,
            string rootAddress,
            IUpdateLoop updateLoop = null,
            ILogService log = null,
            CancellationToken cancellationToken = default)
        {
            if (resources == null)
                throw new ArgumentNullException(nameof(resources));
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));

            var rootHandle = await resources.LoadAssetAsync<GameObject>(rootAddress, cancellationToken);
            GameObject rootObject = null;
            try
            {
                rootObject = UnityEngine.Object.Instantiate(rootHandle.Asset);
                var rootComponent = rootObject.GetComponent<UIRoot>();
                if (rootComponent == null)
                    throw new InvalidOperationException("UIRootPrefab must contain a UIRoot component.");
                UnityEngine.Object.DontDestroyOnLoad(rootObject);
                return new UISystem(
                    resources,
                    rootComponent.BuildRuntime(),
                    registry,
                    rootHandle,
                    rootObject,
                    updateLoop,
                    log);
            }
            catch
            {
                if (rootObject != null)
                    DestroyObject(rootObject);
                rootHandle.Release();
                throw;
            }
        }

        public void SetActiveContext(UIContextId context)
        {
            ThrowIfDisposed();
            if (context == UIContextId.Global)
                throw new ArgumentException("ActiveContext cannot be Global.", nameof(context));

            if (_activeContext == context)
                return;

            var previous = _activeContext;
            if (previous.HasValue)
            {
                HideContextPages(previous.Value);
                _root.SetContextActive(previous.Value, false);
            }

            _activeContext = context;
            _root.SetContextActive(context, true);
            RecomputeContext(context);
        }

        public IWorldUIHost GetWorldUIHost(UIContextId? context = null)
        {
            ThrowIfDisposed();
            var ctx = context ?? _activeContext;
            if (!ctx.HasValue || ctx.Value == UIContextId.Global)
                throw new InvalidOperationException("WorldUIHost requires Main or MiniGame context.");
            return _root.GetWorldUIHost(ctx.Value);
        }


        public UniTask<TUI> OpenUI<TUI>(
            IUIArgs<TUI> args,
            CancellationToken cancellationToken = default)
            where TUI : UIBase
        {
            return OpenUI(args, RequireActiveContext(), cancellationToken);
        }

        public async UniTask<TUI> OpenUI<TUI>(
            IUIArgs<TUI> args,
            UIContextId context,
            CancellationToken cancellationToken = default)
            where TUI : UIBase
        {
            ThrowIfDisposed();
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            var registration = _registry.Get<TUI>();
            EnsureSupported(registration);
            var key = new UIEntryKey(typeof(TUI), context);

            while (_inflight.TryGetValue(key, out var pending))
            {
                await pending.Task.AttachExternalCancellation(cancellationToken);
            }

            if (_entries.TryGetValue(key, out var existing) && existing.IsOpen)
            {
                if (IsStacked(existing) && !IsTop(existing))
                    return await PopToExistingAsync<TUI>(existing, args, cancellationToken);

                OpenExisting(existing, args);
                RecomputeContext(context);
                return (TUI)existing.Page;
            }

            var tcs = new UniTaskCompletionSource<UIBase>();
            _inflight[key] = tcs;
            try
            {
                UIEntry entry;
                if (_entries.TryGetValue(key, out var cached) && !cached.IsOpen)
                {
                    entry = cached;
                    PrepareRootHidden(entry.Root);
                    ApplySafeArea(entry);
                    OpenEntry(entry, args, pushStack: true);
                }
                else
                {
                    entry = await CreateEntryAsync<TUI>(registration, context, cancellationToken);
                    try
                    {
                        OpenEntry(entry, args, pushStack: true);
                        _entries[key] = entry;
                    }
                    catch
                    {
                        DestroyEntry(entry);
                        throw;
                    }
                }

                RecomputeContext(context);
                _inflight.Remove(key);
                tcs.TrySetResult(entry.Page);
                return (TUI)entry.Page;
            }
            catch (Exception exception)
            {
                _inflight.Remove(key);
                tcs.TrySetException(exception);
                throw;
            }
            finally
            {
                _inflight.Remove(key);
            }
        }

        public UniTask<TUI> ReplaceUI<TUI>(
            IUIArgs<TUI> args,
            CancellationToken cancellationToken = default)
            where TUI : UIBase
        {
            return ReplaceUI(args, RequireActiveContext(), cancellationToken);
        }

        public async UniTask<TUI> ReplaceUI<TUI>(
            IUIArgs<TUI> args,
            UIContextId context,
            CancellationToken cancellationToken = default)
            where TUI : UIBase
        {
            ThrowIfDisposed();
            if (args == null)
                throw new ArgumentNullException(nameof(args));

            var registration = _registry.Get<TUI>();
            EnsureSupported(registration);
            if (registration.Presentation != UIPresentation.Screen)
                throw new InvalidOperationException("ReplaceUI only supports Screen pages.");

            var stack = GetStack(registration.Layer, context, UIPresentation.Screen);
            var current = stack.Count == 0 ? null : stack[stack.Count - 1];
            var key = new UIEntryKey(typeof(TUI), context);

            if (current != null && current.Page.GetType() == typeof(TUI))
            {
                OpenExisting(current, args);
                RecomputeContext(context);
                return (TUI)current.Page;
            }

            if (_entries.TryGetValue(key, out var existing) && existing.IsOpen)
                throw new InvalidOperationException(
                    $"ReplaceUI cannot target a page already lower in the stack: {typeof(TUI).FullName}");

            UIEntry entry;
            if (_entries.TryGetValue(key, out var cached) && !cached.IsOpen)
            {
                entry = cached;
                PrepareRootHidden(entry.Root);
                ApplySafeArea(entry);
            }
            else
            {
                entry = await CreateEntryAsync<TUI>(registration, context, cancellationToken);
            }

            try
            {
                OpenEntry(entry, args, pushStack: true);
                if (!_entries.ContainsKey(key))
                    _entries.Add(key, entry);

                if (current != null)
                    CloseEntry(current.Key, destroy: false);

                RecomputeContext(context);
                return (TUI)entry.Page;
            }
            catch
            {
                if (!_entries.ContainsKey(key) || _entries[key] != entry)
                    DestroyEntry(entry);
                else if (!entry.IsOpen)
                    DestroyEntry(entry);
                throw;
            }
        }

        public bool CloseUI<TUI>() where TUI : UIBase
        {
            return CloseUI<TUI>(RequireActiveContext());
        }

        public bool CloseUI<TUI>(UIContextId context) where TUI : UIBase
        {
            ThrowIfDisposed();
            return CloseEntry(new UIEntryKey(typeof(TUI), context), destroy: false);
        }

        public bool Back()
        {
            ThrowIfDisposed();
            if (TryBackContext(UIContextId.Global))
                return true;
            if (_activeContext.HasValue && TryBackContext(_activeContext.Value))
                return true;
            return false;
        }

        public bool Back(UIContextId context)
        {
            ThrowIfDisposed();
            return TryBackContext(context);
        }

        public void CloseAll(UIContextId context)
        {
            ThrowIfDisposed();
            var snapshot = new List<UIEntry>(_entries.Values);
            for (var i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i].Key.Context != context)
                    continue;
                CloseEntry(snapshot[i].Key, destroy: true);
            }

            if (context != UIContextId.Global
                && _root.TryGetWorldContext(context, out var world))
                world.Host.Clear();
        }

        public bool IsOpen<TUI>() where TUI : UIBase
        {
            return IsOpen<TUI>(RequireActiveContext());
        }

        public bool IsOpen<TUI>(UIContextId context) where TUI : UIBase
        {
            ThrowIfDisposed();
            return _entries.TryGetValue(new UIEntryKey(typeof(TUI), context), out var entry) && entry.IsOpen;
        }

        public void BindPageContext(object context)
        {
            ThrowIfDisposed();
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (_pageContext != null && !ReferenceEquals(_pageContext, context))
                throw new InvalidOperationException("UI page context is already bound.");
            _pageContext = context;
        }

        public void ResetPageContext()
        {
            ThrowIfDisposed();
            var snapshot = new List<UIEntry>(_entries.Values);
            for (var i = 0; i < snapshot.Count; i++)
                CloseEntry(snapshot[i].Key, destroy: true);
            _pageContext = null;
        }

        // Test seam: advances Auto cache TTL with a controlled delta.
        public void AdvanceCacheTimeForTests(float deltaTime)
        {
            ThrowIfDisposed();
            if (deltaTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            TickCache(deltaTime);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _updateRegistration?.Dispose();

            var snapshot = new List<UIEntry>(_entries.Values);
            for (var i = 0; i < snapshot.Count; i++)
                DestroyEntry(snapshot[i]);
            _entries.Clear();
            _stacks.Clear();
            foreach (var shield in _overlayShields.Values)
                DestroyObject(shield);
            _overlayShields.Clear();
            _root.Dispose();
            _pageContext = null;
            _activeContext = null;

            if (_rootObject != null)
                DestroyObject(_rootObject);
            _rootHandle?.Release();
        }

        private void OnUpdate()
        {
            if (_disposed)
                return;
            TickCache(Time.unscaledDeltaTime);
        }

        private void TickCache(float deltaTime)
        {
            var snapshot = new List<UIEntry>(_entries.Values);
            for (var i = 0; i < snapshot.Count; i++)
            {
                var entry = snapshot[i];
                if (entry.IsOpen || entry.Registration.Cache != UICachePolicy.Auto)
                    continue;
                entry.CachedSeconds += deltaTime;
                if (entry.CachedSeconds >= DefaultCacheTtlSeconds)
                    DestroyEntry(entry);
            }
        }

        private async UniTask<TUI> PopToExistingAsync<TUI>(
            UIEntry target,
            object args,
            CancellationToken cancellationToken)
            where TUI : UIBase
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenExisting(target, args);

            var stack = GetStack(target.Registration.Layer, target.Key.Context, target.Registration.Presentation);
            var toClose = new List<UIEntry>();
            for (var i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i] == target)
                    break;
                toClose.Add(stack[i]);
            }

            for (var i = 0; i < toClose.Count; i++)
                CloseEntry(toClose[i].Key, destroy: false);

            RecomputeContext(target.Key.Context);
            return (TUI)target.Page;
        }

        private async UniTask<UIEntry> CreateEntryAsync<TUI>(
            UIPageRegistration registration,
            UIContextId context,
            CancellationToken cancellationToken)
            where TUI : UIBase
        {
            var rootNodes = _root.GetContext(registration.Layer, context);
            var handle = await _resources.LoadAssetAsync<GameObject>(registration.Address, cancellationToken);
            GameObject pageRoot = null;
            UIBase page = null;
            try
            {
                var parent = GetParent(registration, rootNodes);
                pageRoot = UnityEngine.Object.Instantiate(handle.Asset, parent, false);
                PrepareRootHidden(pageRoot);
                page = registration.Factory.Create(
                    pageRoot,
                    pageRoot.GetComponent<UIBindingHost>(),
                    _pageContext);
                if (page == null || page.GetType() != typeof(TUI))
                    throw new InvalidOperationException($"UI factory returned the wrong page type: {registration.PageType.FullName}");

                var entry = new UIEntry(new UIEntryKey(typeof(TUI), context), page, pageRoot, handle, registration, rootNodes);
                ApplySafeArea(entry);
                return entry;
            }
            catch
            {
                page?.NotifyRemove();
                if (pageRoot != null)
                    DestroyObject(pageRoot);
                handle.Release();
                throw;
            }
        }

        private void ApplySafeArea(UIEntry entry)
        {
            if (entry.Registration.SafeArea == UISafeAreaPolicy.None)
                return;

            var target = entry.Root;
            if (!string.IsNullOrEmpty(entry.Registration.SafeAreaPath))
            {
                var child = entry.Root.transform.Find(entry.Registration.SafeAreaPath);
                if (child == null)
                    throw new InvalidOperationException(
                        $"SafeAreaPath '{entry.Registration.SafeAreaPath}' was not found on {entry.Registration.PageType.Name}.");
                target = child.gameObject;
            }

            if (!(target.transform is RectTransform))
                throw new InvalidOperationException(
                    $"SafeArea target is not a RectTransform: {entry.Registration.PageType.Name}");

            // Cached pages already have SafeArea from the first open — reconfigure, don't throw.
            // Prefab-authored SafeArea is also accepted (Configure overwrites policy).
            var safeArea = target.GetComponent<SafeArea>() ?? target.AddComponent<SafeArea>();
            safeArea.Configure(entry.Registration.SafeArea, _root.SafeAreaMaskSprite);
        }

        private void OpenExisting(UIEntry entry, object args)
        {
            if (!entry.IsOpen)
            {
                OpenEntry(entry, args, pushStack: true);
                return;
            }

            if (IsStacked(entry))
            {
                var stack = GetStack(entry.Registration.Layer, entry.Key.Context, entry.Registration.Presentation);
                if (stack[stack.Count - 1] != entry)
                {
                    stack.Remove(entry);
                    stack.Add(entry);
                }
            }

            entry.Page.NotifyOpen(args);
        }

        private void OpenEntry(UIEntry entry, object args, bool pushStack)
        {
            entry.IsOpen = true;
            entry.CachedSeconds = 0f;
            if (pushStack && IsStacked(entry))
            {
                var stack = GetStack(entry.Registration.Layer, entry.Key.Context, entry.Registration.Presentation);
                stack.Remove(entry);
                stack.Add(entry);
            }

            entry.Page.NotifyOpen(args);
        }

        private bool CloseEntry(UIEntryKey key, bool destroy)
        {
            if (!_entries.TryGetValue(key, out var entry))
                return false;

            var context = key.Context;
            if (entry.IsOpen)
            {
                if (IsStacked(entry))
                {
                    var stack = GetStack(entry.Registration.Layer, key.Context, entry.Registration.Presentation);
                    stack.Remove(entry);
                }

                entry.IsOpen = false;
                try
                {
                    entry.Page.NotifyClose();
                }
                catch (Exception exception)
                {
                    _log?.Exception("UISystem", exception, entry.Registration.PageType.FullName);
                }

                RemoveOverlayShield(entry);
            }

            if (destroy || entry.Registration.Cache == UICachePolicy.None)
                DestroyEntry(entry);
            else
            {
                entry.CachedSeconds = 0f;
                SetRootActive(entry, false);
            }

            RecomputeContext(context);
            return true;
        }

        private void DestroyEntry(UIEntry entry)
        {
            if (entry == null)
                return;
            if (IsStacked(entry))
                GetStack(entry.Registration.Layer, entry.Key.Context, entry.Registration.Presentation).Remove(entry);
            _entries.Remove(entry.Key);
            RemoveOverlayShield(entry);
            try
            {
                entry.Page.NotifyRemove();
            }
            catch (Exception exception)
            {
                _log?.Exception("UISystem", exception, entry.Registration.PageType.FullName);
            }

            DestroyObject(entry.Root);
            entry.Handle.Release();
        }

        private void HideContextPages(UIContextId context)
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.Key.Context != context || !entry.IsOpen)
                    continue;
                if (entry.Page.State == UIPageState.Active)
                {
                    try
                    {
                        entry.Page.NotifyHide();
                    }
                    catch (Exception exception)
                    {
                        _log?.Exception("UISystem", exception, entry.Registration.PageType.FullName);
                    }
                }

                SetRootActive(entry, false);
            }
        }

        private void RecomputeContext(UIContextId context)
        {
            var contextActive = context == UIContextId.Global ||
                                (_activeContext.HasValue && _activeContext.Value == context);

            var open = new List<UIEntry>();
            foreach (var entry in _entries.Values)
            {
                if (entry.Key.Context == context && entry.IsOpen)
                    open.Add(entry);
            }

            open.Sort(CompareRenderOrder);

            var topFullScreenOrder = int.MinValue;
            for (var i = 0; i < open.Count; i++)
            {
                if (IsEffectivelyFullScreen(open[i]))
                    topFullScreenOrder = Math.Max(topFullScreenOrder, GetRenderOrder(open[i]));
            }

            for (var i = 0; i < open.Count; i++)
            {
                var entry = open[i];
                var shouldShow = contextActive &&
                                 IsStackVisibleCandidate(entry) &&
                                 GetRenderOrder(entry) >= topFullScreenOrder;

                ApplyVisibility(entry, shouldShow);
            }

            UpdateModalMask(context);
            UpdateOverlayShields(context, contextActive);
        }

        private void ApplyVisibility(UIEntry entry, bool shouldShow)
        {
            if (shouldShow)
            {
                SetRootActive(entry, true);
                if (entry.Page.State != UIPageState.Active)
                {
                    try
                    {
                        entry.Page.NotifyShow();
                    }
                    catch (Exception exception)
                    {
                        _log?.Exception("UISystem", exception, entry.Registration.PageType.FullName);
                        SetRootActive(entry, false);
                        CloseEntry(entry.Key, destroy: true);
                    }
                }
            }
            else
            {
                if (entry.Page.State == UIPageState.Active)
                {
                    try
                    {
                        entry.Page.NotifyHide();
                    }
                    catch (Exception exception)
                    {
                        _log?.Exception("UISystem", exception, entry.Registration.PageType.FullName);
                    }
                }

                SetRootActive(entry, false);
            }
        }

        private bool IsStackVisibleCandidate(UIEntry entry)
        {
            if (!IsStacked(entry))
                return true;
            return IsTop(entry);
        }

        private static bool IsEffectivelyFullScreen(UIEntry entry)
        {
            if (entry.Registration.Presentation == UIPresentation.Screen)
                return true;
            return entry.Registration.FullScreen;
        }

        private void UpdateModalMask(UIContextId context)
        {
            foreach (var layer in new[] { UILayer.Top, UILayer.Normal })
            {
                if (!_root.TryGetContext(layer, context, out var nodes))
                    continue;

                var stack = GetStack(layer, context, UIPresentation.Modal);
                if (stack.Count == 0)
                {
                    nodes.SetModalMask(false, false, false, ModalMaskColor, null);
                    continue;
                }

                var top = stack[stack.Count - 1];
                top.Root.transform.SetAsLastSibling();

                var block = top.Registration.Input == UIInputPolicy.Block;
                var visual = top.Registration.Mask;
                Action click = null;
                if (top.Registration.CloseOnMaskClick)
                    click = () => CloseEntry(top.Key, destroy: false);

                nodes.SetModalMask(block || visual, block, visual, ModalMaskColor, click);
            }
        }

        private void UpdateOverlayShields(UIContextId context, bool contextActive)
        {
            foreach (var entry in _entries.Values)
            {
                if (entry.Key.Context != context)
                    continue;
                if (entry.Registration.Presentation != UIPresentation.Overlay)
                    continue;

                var needShield = contextActive &&
                                 entry.IsOpen &&
                                 entry.Registration.Input == UIInputPolicy.Block &&
                                 entry.Page.State == UIPageState.Active;
                if (needShield)
                    EnsureOverlayShield(entry);
                else
                    RemoveOverlayShield(entry);
            }
        }

        private void EnsureOverlayShield(UIEntry entry)
        {
            if (_overlayShields.ContainsKey(entry.Key))
                return;

            var shield = new GameObject("InputShield", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)shield.transform;
            rect.SetParent(entry.Root.transform.parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = shield.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            shield.transform.SetSiblingIndex(entry.Root.transform.GetSiblingIndex());
            _overlayShields[entry.Key] = shield;
        }

        private void RemoveOverlayShield(UIEntry entry)
        {
            if (!_overlayShields.TryGetValue(entry.Key, out var shield))
                return;
            _overlayShields.Remove(entry.Key);
            DestroyObject(shield);
        }

        private bool TryBackContext(UIContextId context)
        {
            foreach (var layer in new[] { UILayer.Top, UILayer.Normal })
            {
                var stack = GetStack(layer, context, UIPresentation.Modal);
                if (stack.Count == 0)
                    continue;
                var entry = stack[stack.Count - 1];
                if (entry.Registration.Back == UIBackPolicy.Block)
                    return true;
                CloseEntry(entry.Key, destroy: false);
                return true;
            }

            foreach (var layer in new[] { UILayer.Top, UILayer.Normal })
            {
                var stack = GetStack(layer, context, UIPresentation.Screen);
                if (stack.Count == 0)
                    continue;
                var entry = stack[stack.Count - 1];
                if (entry.Registration.Back == UIBackPolicy.Block)
                    return true;
                CloseEntry(entry.Key, destroy: false);
                return true;
            }

            return false;
        }

        private List<UIEntry> GetStack(UILayer layer, UIContextId context, UIPresentation presentation)
        {
            var key = new UIStackKey(layer, context, presentation);
            if (!_stacks.TryGetValue(key, out var stack))
            {
                stack = new List<UIEntry>();
                _stacks.Add(key, stack);
            }

            return stack;
        }

        private static Transform GetParent(UIPageRegistration registration, UIContextNodes nodes)
        {
            switch (registration.Presentation)
            {
                case UIPresentation.Screen:
                    return nodes.Screens;
                case UIPresentation.Modal:
                    return nodes.Modals;
                case UIPresentation.Overlay:
                    return nodes.Overlays;
                default:
                    throw new InvalidOperationException($"Unsupported presentation: {registration.Presentation}");
            }
        }

        private static bool IsStacked(UIEntry entry)
        {
            return entry.Registration.Presentation == UIPresentation.Screen ||
                   entry.Registration.Presentation == UIPresentation.Modal;
        }

        private bool IsTop(UIEntry entry)
        {
            if (!IsStacked(entry))
                return false;
            var stack = GetStack(entry.Registration.Layer, entry.Key.Context, entry.Registration.Presentation);
            return stack.Count > 0 && stack[stack.Count - 1] == entry;
        }

        private static int CompareRenderOrder(UIEntry left, UIEntry right)
        {
            return GetRenderOrder(left).CompareTo(GetRenderOrder(right));
        }

        private static int GetRenderOrder(UIEntry entry)
        {
            var layer = (int)entry.Registration.Layer * 1000;
            var presentation = entry.Registration.Presentation == UIPresentation.Screen ? 0 :
                entry.Registration.Presentation == UIPresentation.Modal ? 100 :
                entry.Registration.Presentation == UIPresentation.Overlay ? 200 : 300;
            return layer + presentation;
        }

        private UIContextId RequireActiveContext()
        {
            if (!_activeContext.HasValue)
                throw new InvalidOperationException("UI ActiveContext is not set. GameFlow must call SetActiveContext.");
            return _activeContext.Value;
        }

        private static void EnsureSupported(UIPageRegistration registration)
        {
            if (registration.Presentation == UIPresentation.WorldWidget)
                throw new NotSupportedException("WorldWidget is not supported in T05a.");
        }

        private static void PrepareRootHidden(GameObject root)
        {
            if (root != null)
                root.SetActive(false);
        }

        private static void SetRootActive(UIEntry entry, bool active)
        {
            if (entry.Root != null && entry.Root.activeSelf != active)
                entry.Root.SetActive(active);
        }

        private static void DestroyObject(GameObject value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(value);
            else
                UnityEngine.Object.DestroyImmediate(value);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UISystem));
        }

        private sealed class UIEntry
        {
            public UIEntry(
                UIEntryKey key,
                UIBase page,
                GameObject root,
                IAssetHandle<GameObject> handle,
                UIPageRegistration registration,
                UIContextNodes nodes)
            {
                Key = key;
                Page = page;
                Root = root;
                Handle = handle;
                Registration = registration;
                Nodes = nodes;
            }

            public UIEntryKey Key { get; }
            public UIBase Page { get; }
            public GameObject Root { get; }
            public IAssetHandle<GameObject> Handle { get; }
            public UIPageRegistration Registration { get; }
            public UIContextNodes Nodes { get; }
            public bool IsOpen { get; set; }
            public float CachedSeconds { get; set; }
        }

        private readonly struct UIEntryKey : IEquatable<UIEntryKey>
        {
            public UIEntryKey(Type pageType, UIContextId context)
            {
                PageType = pageType;
                Context = context;
            }

            public Type PageType { get; }
            public UIContextId Context { get; }

            public bool Equals(UIEntryKey other)
            {
                return PageType == other.PageType && Context == other.Context;
            }

            public override bool Equals(object obj)
            {
                return obj is UIEntryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return ((PageType?.GetHashCode() ?? 0) * 397) ^ (int)Context;
            }
        }

        private readonly struct UIStackKey : IEquatable<UIStackKey>
        {
            public UIStackKey(UILayer layer, UIContextId context, UIPresentation presentation)
            {
                Layer = layer;
                Context = context;
                Presentation = presentation;
            }

            public UILayer Layer { get; }
            public UIContextId Context { get; }
            public UIPresentation Presentation { get; }

            public bool Equals(UIStackKey other)
            {
                return Layer == other.Layer && Context == other.Context && Presentation == other.Presentation;
            }

            public override bool Equals(object obj)
            {
                return obj is UIStackKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (((int)Layer * 397) ^ (int)Context) * 397 ^ (int)Presentation;
            }
        }
    }
}
