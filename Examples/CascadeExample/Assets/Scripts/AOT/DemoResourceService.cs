using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;
using UnityEngine;

namespace CascadeExample
{
    /// <summary>
    /// Provider-neutral <see cref="IResourceService"/> for the example project:
    /// - assets (UIRoot/page prefabs, audio clips) load from Resources by address;
    /// - localization catalog + locale tables are embedded (no external data);
    /// - raw bytes (hot-update dll, AOT metadata) read from StreamingAssets.
    /// Version/patch APIs are local no-ops: the launcher runs in local mode.
    /// </summary>
    public sealed class DemoResourceService : IResourceService
    {
        private readonly Dictionary<string, byte[]> _embedded = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["localization_catalog"] = Encoding.UTF8.GetBytes(
                "{\"defaultLocale\":\"en\",\"locales\":[" +
                "{\"code\":\"en\",\"location\":\"localization/en\"}," +
                "{\"code\":\"zh\",\"location\":\"localization/zh\"}]}"),
            ["localization/en"] = Encoding.UTF8.GetBytes(Table(
                ("home.title", "Cascade Demo"),
                ("home.locale", "Switch locale"),
                ("home.save", "Save value"),
                ("home.load", "Load value"),
                ("home.audio", "Play SFX"),
                ("home.counter", "Update counter"),
                ("home.detail", "Open Detail"),
                ("home.back", "Back"),
                ("home.status", "Ready"))),
            ["localization/zh"] = Encoding.UTF8.GetBytes(Table(
                ("home.title", "Cascade 示例"),
                ("home.locale", "切换语言"),
                ("home.save", "保存"),
                ("home.load", "读取"),
                ("home.audio", "播放音效"),
                ("home.counter", "更新计数"),
                ("home.detail", "打开详情"),
                ("home.back", "返回"),
                ("home.status", "就绪"))),
        };

        public bool IsInitialized => true;
        public string ActivePackageVersion => "local";
        public bool IsUsingLocalVersion => true;

        public UniTask InitializeAsync(ResourceInitOptions options, CancellationToken cancellationToken = default)
            => UniTask.CompletedTask;

        public UniTask<string> RequestVersionAsync(CancellationToken cancellationToken = default)
            => UniTask.FromResult("local");

        public UniTask UpdateManifestAsync(string version, CancellationToken cancellationToken = default)
            => UniTask.CompletedTask;

        public ResourceDownloadPlan PrepareDownload() => new ResourceDownloadPlan(0, 0);

        public UniTask DownloadAsync(IProgress<ResourceDownloadProgress> progress = null, CancellationToken cancellationToken = default)
            => UniTask.CompletedTask;

        public UniTask ClearUnusedCacheAsync(CancellationToken cancellationToken = default)
            => UniTask.CompletedTask;

        public UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            var asset = Resources.Load<T>(location);
            return UniTask.FromResult<IAssetHandle<T>>(new Handle<T>(asset));
        }

        public UniTask<ISceneHandle> LoadSceneAsync(
            string location,
            ResourceSceneLoadMode loadMode = ResourceSceneLoadMode.Single,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Scene loading is not part of the demo service.");

        public async UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken cancellationToken = default)
        {
            if (_embedded.TryGetValue(location, out var bytes))
                return bytes;

            var streamingPath = Path.Combine(Application.streamingAssetsPath, location);
            if (File.Exists(streamingPath))
                return await File.ReadAllBytesAsync(streamingPath, cancellationToken);

            throw new FileNotFoundException($"Demo resource not found: {location}");
        }

        public void UnloadUnused()
        {
        }

        private static string Table(params (string id, string value)[] entries)
        {
            var builder = new StringBuilder("{\"entries\":[");
            for (var i = 0; i < entries.Length; i++)
            {
                if (i > 0)
                    builder.Append(',');
                builder.Append("{\"id\":\"")
                    .Append(entries[i].id)
                    .Append("\",\"value\":\"")
                    .Append(entries[i].value)
                    .Append("\"}");
            }
            builder.Append("]}");
            return builder.ToString();
        }

        private sealed class Handle<T> : IAssetHandle<T>
            where T : UnityEngine.Object
        {
            public Handle(T asset)
            {
                Asset = asset;
            }

            public T Asset { get; }
            public bool IsValid => Asset != null;

            public void Release()
            {
            }

            public void Dispose() => Release();
        }
    }
}
