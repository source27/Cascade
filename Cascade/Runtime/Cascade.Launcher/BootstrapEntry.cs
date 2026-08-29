using System;
using System.Globalization;
using System.Threading;
using Cascade.Service;
using Cascade.Core;
using UnityEngine;

namespace Cascade.Launcher
{
    public class BootstrapEntry : MonoBehaviour
    {
        [SerializeField] private BootstrapEnvironment environment = BootstrapEnvironment.Dev;
        [SerializeField] private BootstrapPlayMode playMode = BootstrapPlayMode.Host;
        [SerializeField] private string appVersionOverride = string.Empty;
        [SerializeField] private PatchWindow patchWindow;

        private BootstrapConfiguration _configuration;
        private ServiceRegistry _registry;
        private UpdateLoop _updateLoop;
        private UnityUpdateDriver _updateDriver;
        private LifecycleRunner _lifecycle;
        private LauncherFlow _flow;

        public BootstrapEnvironment InspectorEnvironment => environment;
        public BootstrapPlayMode InspectorPlayMode => playMode;
        public string AppVersion => string.IsNullOrWhiteSpace(appVersionOverride) ? Application.version : appVersionOverride;
        public LauncherFlow Flow => _flow;

        private void Awake()
        {
            // Config/save/network numbers use '.' decimal; pin culture so regional devices do not break Parse.
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
            _configuration = new BootstrapConfiguration(
                ResolveEnvironment(),
                ResolvePlayMode(),
                AppVersion);

            _registry = new ServiceRegistry();
            RegisterServices(_registry);

            // 补丁界面语言在资源系统就绪前就需要显示：用脚本内嵌迷你词表，不走 IO。
            LauncherText.Initialize(_registry.Get<ISaveService>().GetString(LocalizationService.LocaleSaveKey));

            var log = _registry.Get<ILogService>();
            _updateLoop = new UpdateLoop(log);
            _updateDriver = gameObject.GetComponent<UnityUpdateDriver>() ?? gameObject.AddComponent<UnityUpdateDriver>();
            _updateDriver.Bind(_updateLoop);

            _lifecycle = new LifecycleRunner(log);

            if (patchWindow == null)
                patchWindow = FindObjectOfType<PatchWindow>();

            var host = new GameHost(_registry, _updateLoop);
            _flow = new LauncherFlow(_configuration, _registry, host, patchWindow);
            _flow.Start();
        }

        /// <summary>
        /// Creates the resource provider. Override in a subclass to provide a concrete
        /// implementation (a demo service, the YooAsset integration service, …).
        /// </summary>
        protected virtual IResourceService CreateResourceService() => null;

        /// <summary>
        /// Registers the framework's default service set. Override to customize.
        /// </summary>
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
                    "CreateResourceService() returned null. Provide an IResourceService (e.g. a demo resource service or the YooAsset integration).");
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
            _flow?.Shutdown();
            _flow = null;
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

        private BootstrapPlayMode ResolvePlayMode()
        {
#if UNITY_EDITOR
            return playMode;
#else
            return BootstrapPlayMode.Host;
#endif
        }
    }
}
