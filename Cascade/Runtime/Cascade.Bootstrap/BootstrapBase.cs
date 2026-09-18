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

        protected virtual IResourceService CreateResourceService() => null;

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

        /// <summary>
        /// Creates the frame loop registered as <see cref="IUpdateLoop"/> once <see cref="RegisterServices"/>
        /// has returned (the log service must exist first). An <see cref="IUpdateLoop"/> registered inside
        /// <see cref="RegisterServices"/> wins and this hook is not called.
        /// </summary>
        protected virtual IUpdateLoop CreateUpdateLoop(ILogService log) => new UpdateLoop(log);

        protected virtual void RegisterServices(IServiceRegistry registry)
        {
            var log = CreateLogService()
                ?? throw new InvalidOperationException("CreateLogService() returned null. Provide an ILogService.");
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
