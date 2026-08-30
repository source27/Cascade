using System;
using System.Globalization;
using System.Threading;
using Cascade.Service;
using Cascade.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Bootstrap
{
    public class BootstrapEntry : MonoBehaviour
    {
        [SerializeField] private BootstrapEnvironment environment = BootstrapEnvironment.Dev;
        [SerializeField] private string appVersionOverride = string.Empty;

        private BootstrapConfiguration _configuration;
        private ServiceRegistry _registry;
        private UpdateLoop _updateLoop;
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
            _updateLoop = new UpdateLoop(log);
            _updateDriver = gameObject.GetComponent<UnityUpdateDriver>()
                            ?? gameObject.AddComponent<UnityUpdateDriver>();
            _updateDriver.Bind(_updateLoop);
            _lifecycle = new LifecycleRunner(log);
            _host = new GameHost(_registry, _updateLoop);

            try
            {
                var resources = _registry.Get<IResourceService>();
                var options = _configuration.ResourceInitOptions ?? new ResourceInitOptions();
                await resources.InitializeAsync(options, cancellationToken);

                if (_registry.TryGet<ILocalizationService>(out var localization))
                {
                    await localization.InitializeAsync(cancellationToken);
                    LocalizationAccess.Bind(localization);
                }

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
            host?.Log?.Warning("Bootstrap", "RunGameAsync was not overridden; bootstrap finished with no game entry.");
            return UniTask.CompletedTask;
        }

        protected virtual IResourceService CreateResourceService() => null;

        protected virtual ResourceInitOptions CreateResourceInitOptions() => null;

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
            var save = new PlayerPrefsSaveService();
            registry.Register<ISaveService>(save);
            registry.Register<ILocalizationService>(new LocalizationService(resources, save, log));
            registry.Register<INetworkService>(new NullNetworkService());
            registry.Register<IAtlasSpriteService>(new AtlasSpriteService(resources, log));
        }

        private void OnDestroy()
        {
            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = null;
            _lifecycle?.StopAll();
            if (_registry != null &&
                _registry.TryGet<ILocalizationService>(out var localization))
                LocalizationAccess.Unbind(localization);
            else
                LocalizationAccess.Unbind();
            _registry?.Dispose();
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
