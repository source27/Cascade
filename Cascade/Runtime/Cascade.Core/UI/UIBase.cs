using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Core
{
    public abstract class UIBase
    {
        private readonly UIDisposableScope _instanceScope = new UIDisposableScope();
        private readonly UIDisposableScope _openScope = new UIDisposableScope();
        private readonly UIDisposableScope _visibleScope = new UIDisposableScope();
        private CancellationTokenSource _instanceCts;
        private CancellationTokenSource _openCts;
        private CancellationTokenSource _visibleCts;

        public GameObject Root { get; private set; }
        public object Bindings { get; private set; }
        public object Ctx { get; private set; }
        public UIPageState State { get; private set; } = UIPageState.Destroyed;

        protected CancellationToken InstanceToken =>
            _instanceCts?.Token ?? CancellationToken.None;

        protected CancellationToken OpenToken =>
            _openCts?.Token ?? CancellationToken.None;

        protected CancellationToken VisibleToken =>
            _visibleCts?.Token ?? CancellationToken.None;

        public void Initialize(GameObject root, object bindings = null, object pageContext = null)
        {
            if (State != UIPageState.Destroyed)
                throw new InvalidOperationException($"UI page is already initialized: {GetType().FullName}");
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Bindings = bindings;
            Ctx = pageContext;
            State = UIPageState.Created;
            _instanceCts = new CancellationTokenSource();
            OnCreate();
        }

        internal void BeginOpenGeneration()
        {
            EnsureInitialized();
            _openScope.DisposeAll();
            CancelAndReplace(ref _openCts);
        }

        internal void BeginVisibleGeneration()
        {
            EnsureInitialized();
            _visibleScope.DisposeAll();
            CancelAndReplace(ref _visibleCts);
        }

        internal void NotifyOpen(object args)
        {
            EnsureInitialized();
            BeginOpenGeneration();
            try
            {
                OnOpenUntyped(args);
            }
            catch
            {
                _openScope.DisposeAll();
                CancelSource(ref _openCts);
                throw;
            }
        }

        internal void NotifyHide()
        {
            EnsureInitialized();
            if (State == UIPageState.Hidden || State == UIPageState.CachedHidden || State == UIPageState.Destroyed)
                return;
            if (State != UIPageState.Active)
            {
                State = UIPageState.Hidden;
                return;
            }

            try
            {
                OnHide();
            }
            finally
            {
                _visibleScope.DisposeAll();
                CancelSource(ref _visibleCts);
                State = UIPageState.Hidden;
            }
        }

        internal void NotifyShow()
        {
            EnsureInitialized();
            if (State == UIPageState.Active)
                return;

            BeginVisibleGeneration();
            State = UIPageState.Active;
            try
            {
                OnShow();
            }
            catch
            {
                _visibleScope.DisposeAll();
                CancelSource(ref _visibleCts);
                State = UIPageState.Hidden;
                throw;
            }
        }

        internal void NotifyClose()
        {
            EnsureInitialized();
            if (State == UIPageState.Active)
                NotifyHide();

            try
            {
                OnClose();
            }
            finally
            {
                _openScope.DisposeAll();
                CancelSource(ref _openCts);
                State = UIPageState.CachedHidden;
            }
        }

        internal void NotifyRemove()
        {
            if (State == UIPageState.Destroyed)
                return;

            if (State == UIPageState.Active)
                NotifyHide();
            if (State != UIPageState.CachedHidden && State != UIPageState.Destroyed && State != UIPageState.Created)
            {
                try
                {
                    OnClose();
                }
                catch
                {
                    // Cleanup must continue.
                }
                finally
                {
                    _openScope.DisposeAll();
                    CancelSource(ref _openCts);
                }
            }

            try
            {
                OnRemove();
            }
            finally
            {
                _visibleScope.DisposeAll();
                _openScope.DisposeAll();
                _instanceScope.DisposeAll();
                CancelSource(ref _visibleCts);
                CancelSource(ref _openCts);
                CancelSource(ref _instanceCts);
                State = UIPageState.Destroyed;
                Root = null;
                Bindings = null;
                Ctx = null;
            }
        }

        protected T TrackInstance<T>(T disposable) where T : IDisposable
        {
            _instanceScope.Add(disposable);
            return disposable;
        }

        protected T TrackOpen<T>(T disposable) where T : IDisposable
        {
            _openScope.Add(disposable);
            return disposable;
        }

        protected T TrackVisible<T>(T disposable) where T : IDisposable
        {
            _visibleScope.Add(disposable);
            return disposable;
        }

        protected void RunInstance(Func<CancellationToken, UniTask> operation) =>
            RunScoped(InstanceToken, operation);

        protected void RunOpen(Func<CancellationToken, UniTask> operation) =>
            RunScoped(OpenToken, operation);

        protected void RunVisible(Func<CancellationToken, UniTask> operation) =>
            RunScoped(VisibleToken, operation);

        protected TPageContext RequirePageContext<TPageContext>() where TPageContext : class
        {
            if (Ctx is TPageContext typed)
                return typed;
            throw new InvalidOperationException(
                $"UI page context is not available or has the wrong type. Expected {typeof(TPageContext).FullName}.");
        }

        protected virtual void OnCreate() { }
        protected virtual void OnHide() { }
        protected virtual void OnShow() { }
        protected virtual void OnClose() { }
        protected virtual void OnRemove() { }

        protected abstract void OnOpenUntyped(object args);

        private void RunScoped(CancellationToken token, Func<CancellationToken, UniTask> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            RunScopedAsync(token, operation).Forget();
        }

        private async UniTaskVoid RunScopedAsync(CancellationToken token, Func<CancellationToken, UniTask> operation)
        {
            try
            {
                await operation(token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // Logged by UISystem when it owns a log; pages without host log still swallow to protect callers.
            }
        }

        private void EnsureInitialized()
        {
            if (State == UIPageState.Destroyed)
                throw new InvalidOperationException($"UI page is not initialized: {GetType().FullName}");
        }

        private static void CancelAndReplace(ref CancellationTokenSource source)
        {
            CancelSource(ref source);
            source = new CancellationTokenSource();
        }

        private static void CancelSource(ref CancellationTokenSource source)
        {
            if (source == null)
                return;
            try
            {
                if (!source.IsCancellationRequested)
                    source.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            source.Dispose();
            source = null;
        }

        private sealed class UIDisposableScope
        {
            private readonly global::System.Collections.Generic.List<IDisposable> _items =
                new global::System.Collections.Generic.List<IDisposable>();

            public void Add(IDisposable disposable)
            {
                if (disposable == null)
                    throw new ArgumentNullException(nameof(disposable));
                _items.Add(disposable);
            }

            public void DisposeAll()
            {
                for (var i = _items.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        _items[i]?.Dispose();
                    }
                    catch
                    {
                    }
                }

                _items.Clear();
            }
        }
    }

    public abstract class UIBase<TArgs> : UIBase
    {
        protected sealed override void OnOpenUntyped(object args)
        {
            if (!(args is TArgs typedArgs))
                throw new ArgumentException(
                    $"Invalid arguments for {GetType().FullName}. Expected {typeof(TArgs).FullName}.",
                    nameof(args));
            OnOpen(typedArgs);
        }

        protected virtual void OnOpen(TArgs args) { }
    }
}
