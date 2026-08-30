using System;
using Cascade.Launcher;
using Cascade.Service;
using Cascade.Service.YooAsset;
using UnityEngine;

namespace CascadeExample
{
    /// <summary>
    /// 组合根：提供 YooAsset 资源提供者（client 同款管线）。
    /// 资源初始化选项按 Bootstrap 场景的 playMode 决定：
    /// EditorSimulate（编辑器虚拟资源）/ Offline（内置包）/ Host（DevCDN 远端 + 缓存）。
    /// </summary>
    public sealed class ExampleBootstrapEntry : BootstrapEntry
    {
        private const string PackageName = "CascadePak";
        private const string DevCdnRoot = "http://127.0.0.1:2727/Cascade/";

        protected override IResourceService CreateResourceService() => new YooAssetResourceService();

        protected override ResourceInitOptions CreateResourceInitOptions()
        {
            switch (InspectorPlayMode)
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
