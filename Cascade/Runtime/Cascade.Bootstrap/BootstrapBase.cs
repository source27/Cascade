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
        private IUpdateLoop _updateLoop;
        private UnityUpdateDriver _updateDriver;
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

            var log = _registry.Get<ILogService>();
            _updateLoop = ResolveUpdateLoop(log);
            _updateDriver = gameObject.GetComponent<UnityUpdateDriver>()
                            ?? gameObject.AddComponent<UnityUpdateDriver>();
            _updateDriver.Bind(_updateLoop);
            _host = new GameHost(_registry);

            try
            {
                var resources = _registry.Get<IResourceService>();
                var options = CreateResourceInitOptions() ?? new ResourceInitOptions();
                await resources.InitializeAsync(options, cancellationToken);

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
        /// The registered <see cref="IResourceService"/>. Defaults to <see cref="UnityResourcesService"/>
        /// (UnityEngine.Resources) so a project runs with no integration package; override to plug in
        /// YooAsset / Addressables. Returning null skips registration (the pipeline then fails on
        /// <c>Services.Get&lt;IResourceService&gt;()</c>).
        /// </summary>
        protected virtual IResourceService CreateResourceService() => new UnityResourcesService();

        /// <summary>
        /// Provider-specific resource options. Environment, version and other project policy live in the
        /// starter subclass (<c>MobileBootstrapEntry</c> / <c>IndieBootstrapEntry</c>), not in the framework.
        /// </summary>
        protected virtual ResourceInitOptions CreateResourceInitOptions() => null;

        /// <summary>
        /// Creates the registered <see cref="ILogService"/>. Log level policy (dev/beta/gold …) belongs to the
        /// subclass; the default is a plain info-level Unity logger.
        /// </summary>
        protected virtual ILogService CreateLogService() => new UnityLogService
        {
            Enabled = true,
            MinimumLevel = LogLevel.Info
        };

        /// <summary>Registered <see cref="ISaveService"/>; default is PlayerPrefs.</summary>
        protected virtual ISaveService CreateSaveService() => new PlayerPrefsSaveService();

        /// <summary>Registered <see cref="IAudioService"/>; default is Resource-backed with a nullable log.</summary>
        protected virtual IAudioService CreateAudioService(IResourceService resources, ILogService log) =>
            new AudioService(resources, log);

        /// <summary>Registered <see cref="INetworkService"/>; default is the null implementation.</summary>
        protected virtual INetworkService CreateNetworkService() => new NullNetworkService();

        /// <summary>Registered <see cref="IAtlasSpriteService"/>; default reads the AtlasMapping index through <see cref="IResourceService"/>.</summary>
        protected virtual IAtlasSpriteService CreateAtlasSpriteService(IResourceService resources, ILogService log) =>
            new AtlasSpriteService(resources, log);

        /// <summary>
        /// Creates the frame loop registered as <see cref="IUpdateLoop"/> once <see cref="RegisterServices"/>
        /// has returned (the log service must exist first). An <see cref="IUpdateLoop"/> registered inside
        /// <see cref="RegisterServices"/> wins and this hook is not called.
        /// </summary>
        protected virtual IUpdateLoop CreateUpdateLoop(ILogService log) => new UpdateLoop(log);

        /// <summary>
        /// Registers the framework defaults. Call <c>base.RegisterServices(registry)</c> first, then add or
        /// replace: every default comes from a <c>Create…</c> hook (override the hook) and extra services are
        /// just <c>registry.Register&lt;T&gt;(…)</c>.
        /// </summary>
        protected virtual void RegisterServices(IServiceRegistry registry)
        {
            var log = CreateLogService()
                ?? throw new InvalidOperationException(
                    "CreateLogService() returned null; the pipeline needs an ILogService.");
            registry.Register<ILogService>(log);
            registry.Register<IEventBus>(new EventBus(log));

            var resources = CreateResourceService()
                ?? throw new InvalidOperationException(
                    "CreateResourceService() returned null; the pipeline needs an IResourceService. " +
                    "Return the default UnityResourcesService or a provider from an integration package.");
            registry.Register<IResourceService>(resources);

            RegisterIfNotNull(registry, CreateAudioService(resources, log));
            RegisterIfNotNull(registry, CreateSaveService());
            RegisterIfNotNull(registry, CreateNetworkService());
            RegisterIfNotNull(registry, CreateAtlasSpriteService(resources, log));
        }

        /// <summary>Optional defaults: a hook returning null simply means “do not register this service”.</summary>
        private static void RegisterIfNotNull<TService>(IServiceRegistry registry, TService service)
            where TService : class
        {
            if (service != null)
                registry.Register(service);
        }

        private void OnDestroy()
        {
            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = null;
            _registry?.Dispose();
        }

        private IUpdateLoop ResolveUpdateLoop(ILogService log)
        {
            if (_registry.TryGet<IUpdateLoop>(out var registered))
                return registered;

            var loop = CreateUpdateLoop(log)
                ?? throw new InvalidOperationException("CreateUpdateLoop() returned null.");
            _registry.Register<IUpdateLoop>(loop);
            return loop;
        }
    }
}
