using System;
using System.Globalization;
using System.Threading;
using Cascade.Service;
using Cascade.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Bootstrap
{
    public class BootstrapBase : MonoBehaviour
    {
        private ServiceRegistry _registry;
        private IGameHost _host;
        private CancellationTokenSource _runCts;

        protected IServiceRegistry Services => _registry;
        protected IGameHost Host => _host;

        private void Awake()
        {
            var invariant = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentCulture = invariant;
            CultureInfo.DefaultThreadCurrentUICulture = invariant;
            Thread.CurrentThread.CurrentCulture = invariant;
            Thread.CurrentThread.CurrentUICulture = invariant;

            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
        }

        private void Start()
        {
            _runCts = new CancellationTokenSource();
            RunBootstrapAsync(_runCts.Token).Forget();
        }

        private async UniTaskVoid RunBootstrapAsync(CancellationToken cancellationToken)
        {
            _registry = new ServiceRegistry();
            RegisterServices(_registry);
            RegisterDefaultServices(_registry);

            var log = _registry.Get<ILogService>();
            _host = new GameHost(_registry);

            try
            {
                var resources = _registry.Get<IResourceService>();
                await resources.InitializeAsync(cancellationToken);

                await RunGameAsync(_host, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                log.Exception("Bootstrap", exception, "Bootstrap failed");
            }
        }

        protected virtual UniTask RunGameAsync(IGameHost host, CancellationToken cancellationToken)
        {
            if (host != null && host.Services.TryGet<ILogService>(out var log))
                log.Warning("Bootstrap", "RunGameAsync was not overridden; bootstrap finished with no game entry.");
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// The single composition-root extension point: register your own implementations and game services
        /// here (the same <c>registry.Register&lt;T&gt;(…)</c> call for both).
        /// <para>
        /// It runs <b>before</b> the framework defaults, so what you register is what the defaults are built
        /// on — your <see cref="ILogService"/> feeds <c>EventBus</c> — and <see cref="RegisterDefaultServices"/>
        /// only fills contracts you left unregistered. No <c>base</c> call needed.
        /// </para>
        /// <para>
        /// Swap something that is already registered with <c>registry.Replace&lt;T&gt;(…)</c> (it disposes the
        /// replaced instance) and drop a default with <c>registry.Remove&lt;T&gt;()</c>; both are
        /// composition-root-time operations, not runtime re-binding.
        /// </para>
        /// </summary>
        protected virtual void RegisterServices(IServiceRegistry registry)
        {
        }

        /// <summary>
        /// Framework defaults, registered after <see cref="RegisterServices"/> for contracts still missing.
        /// Audio is not here on purpose — it lives in the optional <c>com.source27.cascade.modules.audio</c>.
        /// </summary>
        private void RegisterDefaultServices(IServiceRegistry registry)
        {
            if (!registry.TryGet<ILogService>(out var log))
            {
                log = new UnityLogService { Enabled = true, MinimumLevel = LogLevel.Info };
                registry.Register(log);
            }

            if (!registry.TryGet<IResourceService>(out var resources))
            {
                resources = new UnityResourcesService();
                registry.Register(resources);
            }

            if (!registry.TryGet<IEventBus>(out _))
                registry.Register<IEventBus>(new EventBus(log));

            if (!registry.TryGet<ISaveService>(out _))
                registry.Register<ISaveService>(new PlayerPrefsSaveService());

            // Self-hosted: the loop owns its DontDestroyOnLoad object, so bootstrap holds nothing.
            // Register (or Replace) another IUpdateLoop to drive time differently — nothing else drives it.
            if (!registry.TryGet<IUpdateLoop>(out _))
                registry.Register<IUpdateLoop>(UnityUpdateLoop.Create(log));
        }

        private void OnDestroy()
        {
            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = null;
            _registry?.Dispose();
        }
    }
}
