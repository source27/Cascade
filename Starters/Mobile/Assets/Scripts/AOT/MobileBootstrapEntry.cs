using System;
using System.Threading;
using Cascade.Bootstrap;
using Cascade.Core;
using Cascade.Modules.Audio;
using Cascade.Modules.Localization;
using Cascade.Mobile;
using Cascade.Service;
using Cascade.Service.YooAsset;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Mobile
{
    public sealed class MobileBootstrapEntry : BootstrapBase
    {
        private const string PackageName = "CascadePak";
        private const string DevCdnRoot = "http://127.0.0.1:2727/Cascade/";

        [SerializeField] private BootstrapEnvironment environment = BootstrapEnvironment.Dev;
        [SerializeField] private string appVersionOverride = string.Empty;
        [SerializeField] private BootstrapPlayMode playMode = BootstrapPlayMode.Host;
        [SerializeField] private PatchWindow patchWindow;

        public BootstrapEnvironment InspectorEnvironment => environment;
        public BootstrapPlayMode InspectorPlayMode => playMode;
        public string AppVersion =>
            string.IsNullOrWhiteSpace(appVersionOverride) ? Application.version : appVersionOverride;

        protected override void RegisterServices(IServiceRegistry registry)
        {
            registry.Register<ILogService>(new UnityLogService
            {
                Enabled = true,
                MinimumLevel = ResolveMinimumLogLevel()
            });
            registry.Register<IResourceService>(new YooAssetResourceService(CreateYooOptions()));
            registry.Register<IAudioService>(new AudioService(
                registry.Get<IResourceService>(), registry.Get<ILogService>()));
        }

        private YooAssetResourceInitOptions CreateYooOptions()
        {
            switch (playMode)
            {
                case BootstrapPlayMode.EditorSimulate:
                    return new YooAssetResourceInitOptions(PackageName, YooAssetResourcePlayMode.EditorSimulate);
                case BootstrapPlayMode.Offline:
                    return new YooAssetResourceInitOptions(PackageName, YooAssetResourcePlayMode.Offline);
                case BootstrapPlayMode.Host:
                    return new YooAssetResourceInitOptions(
                        PackageName,
                        YooAssetResourcePlayMode.Host,
                        $"{DevCdnRoot}{GetPlatformFolder()}/{Application.version}");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override UniTask RunGameAsync(IGameHost host, CancellationToken cancellationToken)
        {
            var mobileConfig = new MobileBootstrapConfiguration(
                ResolveEnvironment(),
                ResolvePlayMode(),
                AppVersion);

            LauncherText.Initialize(Services.Get<ISaveService>().GetString(LocalizationService.LocaleSaveKey));

            if (patchWindow == null)
                patchWindow = FindObjectOfType<PatchWindow>();

            var flow = new LauncherFlow(mobileConfig, Services, host, patchWindow);
            flow.Start();
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        private BootstrapEnvironment ResolveEnvironment()
        {
#if UNITY_EDITOR
            return environment;
#else
            return BootstrapEnvironment.Gold;
#endif
        }

        private LogLevel ResolveMinimumLogLevel()
        {
            switch (ResolveEnvironment())
            {
                case BootstrapEnvironment.Dev:
                    return LogLevel.Trace;
                case BootstrapEnvironment.Beta:
                    return LogLevel.Debug;
                default:
                    return LogLevel.Info;
            }
        }

        private BootstrapPlayMode ResolvePlayMode()
        {
#if UNITY_EDITOR
            return playMode;
#else
            return BootstrapPlayMode.Host;
#endif
        }

        private static string GetPlatformFolder()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android: return "Android";
                case RuntimePlatform.IPhonePlayer: return "IPhone";
                case RuntimePlatform.WebGLPlayer: return "WebGL";
                default: return "PC";
            }
        }
    }
}
