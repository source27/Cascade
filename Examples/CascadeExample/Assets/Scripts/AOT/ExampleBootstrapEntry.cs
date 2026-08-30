using System;
using System.Threading;
using Cascade.Bootstrap;
using Cascade.Core;
using Cascade.Launcher;
using Cascade.Service;
using Cascade.Service.YooAsset;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CascadeExample
{
    public sealed class ExampleBootstrapEntry : BootstrapEntry
    {
        private const string PackageName = "CascadePak";
        private const string DevCdnRoot = "http://127.0.0.1:2727/Cascade/";

        [SerializeField] private BootstrapPlayMode playMode = BootstrapPlayMode.Host;
        [SerializeField] private PatchWindow patchWindow;

        public BootstrapPlayMode InspectorPlayMode => playMode;

        protected override IResourceService CreateResourceService() => new YooAssetResourceService();

        protected override ResourceInitOptions CreateResourceInitOptions()
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
                Configuration.Environment,
                ResolvePlayMode(),
                AppVersion)
            {
                ResourceInitOptions = Configuration.ResourceInitOptions
            };

            LauncherText.Initialize(Services.Get<ISaveService>().GetString(LocalizationService.LocaleSaveKey));

            if (patchWindow == null)
                patchWindow = FindObjectOfType<PatchWindow>();

            var flow = new LauncherFlow(mobileConfig, Services, host, patchWindow);
            flow.Start();
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
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
