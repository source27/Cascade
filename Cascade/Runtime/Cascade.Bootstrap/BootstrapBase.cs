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
        [SerializeField] private BootstrapEnvironment environment = BootstrapEnvironment.Dev;
        [SerializeField] private string appVersionOverride = string.Empty;

        private BootstrapConfiguration _configuration;
        private ServiceRegistry _registry;
        private IUpdateLoop _updateLoop;
        private UnityUpdateDriver _updateDriver;
        private LifecycleRunner _lifecycle;
        private IGameHost _host;
        private CancellationTokenSource _runCts;

        public BootstrapEnvironment InspectorEnvironment => environment;
        public string AppVersion =>
            string.IsNullOrWhiteSpace(appVersionOverride) ? Application.version : appVersionOverride;

        protected BootstrapConfiguration Configuration => _configuration;
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
            _configuration = new BootstrapConfiguration(ResolveEnvironment(), AppVersion)
            {
                ResourceInitOptions = CreateResourceInitOptions()
            };

            _registry = new ServiceRegistry();
            RegisterServices(_registry);

            var log = _registry.Get<ILogService>();
            _updateLoop = ResolveUpdateLoop(log);
            _updateDriver = gameObject.GetComponent<UnityUpdateDriver>()
                            ?? gameObject.AddComponent<UnityUpdateDriver>();
            _updateDriver.Bind(_updateLoop);
            _lifecycle = new LifecycleRunner(log);
            _host = new GameHost(_registry);

            try
            {
                var resources = _registry.Get<IResourceService>();
                var options = _configuration.ResourceInitOptions ?? new ResourceInitOptions();
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

        protected virtual IResourceService CreateResourceService() => null;

        protected virtual ResourceInitOptions CreateResourceInitOptions() => null;

        /// <summary>
        /// Creates the frame loop registered as <see cref="IUpdateLoop"/> once <see cref="RegisterServices"/>
        /// has returned (the log service must exist first). An <see cref="IUpdateLoop"/> registered inside
        /// <see cref="RegisterServices"/> wins and this hook is not called.
        /// </summary>
        protected virtual IUpdateLoop CreateUpdateLoop(ILogService log) => new UpdateLoop(log);

        protected virtual void RegisterServices(IServiceRegistry registry)
        {
            var log = new UnityLogService
            {
                Enabled = true,
                MinimumLevel = BootstrapConfiguration.DefaultLogLevel(_configuration.Environment)
            };
            registry.Register<ILogService>(log);
            registry.Register<IEventBus>(new EventBus(log));
            var resources = CreateResourceService()
                ?? throw new InvalidOperationException(
                    "CreateResourceService() returned null. Provide an IResourceService from an integration package.");
            registry.Register<IResourceService>(resources);
            registry.Register<IAudioService>(new AudioService(resources, log));
            registry.Register<ISaveService>(new PlayerPrefsSaveService());
            registry.Register<INetworkService>(new NullNetworkService());
            registry.Register<IAtlasSpriteService>(new AtlasSpriteService(resources, log));
        }

        private void OnDestroy()
        {
            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = null;
            _lifecycle?.StopAll();
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

        private BootstrapEnvironment ResolveEnvironment()
        {
#if UNITY_EDITOR
            return environment;
#else
            return BootstrapConfiguration.ResolvePlayerEnvironment();
#endif
        }
    }
}
