using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Service
{
    /// <summary>
    /// Provider-neutral resource initialization options. Resource providers (YooAsset,
    /// Addressables, …) subclass this with their own configuration; the composition root
    /// assigns a concrete instance to <c>Cascade.Launcher.BootstrapConfiguration.ResourceInitOptions</c>.
    /// </summary>
    public class ResourceInitOptions
    {
    }

    public readonly struct ResourceDownloadProgress
    {
        public ResourceDownloadProgress(int totalCount, int currentCount, long totalBytes, long currentBytes)
        {
            TotalCount = totalCount;
            CurrentCount = currentCount;
            TotalBytes = totalBytes;
            CurrentBytes = currentBytes;
        }

        public int TotalCount { get; }
        public int CurrentCount { get; }
        public long TotalBytes { get; }
        public long CurrentBytes { get; }

        public float NormalizedProgress
        {
            get
            {
                if (TotalBytes > 0)
                    return Mathf.Clamp01((float)CurrentBytes / TotalBytes);
                if (TotalCount > 0)
                    return Mathf.Clamp01((float)CurrentCount / TotalCount);
                return 0f;
            }
        }
    }

    public readonly struct ResourceDownloadPlan
    {
        public ResourceDownloadPlan(int totalCount, long totalBytes)
        {
            TotalCount = totalCount;
            TotalBytes = totalBytes;
        }

        public int TotalCount { get; }
        public long TotalBytes { get; }
        public bool NeedsDownload => TotalCount > 0 && TotalBytes > 0;
    }

    public interface IAssetHandle<out T> : IDisposable where T : UnityEngine.Object
    {
        T Asset { get; }
        bool IsValid { get; }
        void Release();
    }

    public enum ResourceSceneLoadMode
    {
        Single = 0,
        Additive = 1
    }

    public interface ISceneHandle
    {
        string SceneName { get; }
        bool IsValid { get; }
        UniTask UnloadAsync(CancellationToken cancellationToken = default);
        void Release();
    }

    public interface IResourceService
    {
        bool IsInitialized { get; }
        string ActivePackageVersion { get; }
        bool IsUsingLocalVersion { get; }

        UniTask InitializeAsync(ResourceInitOptions options, CancellationToken cancellationToken = default);
        UniTask<string> RequestVersionAsync(CancellationToken cancellationToken = default);
        UniTask UpdateManifestAsync(string version, CancellationToken cancellationToken = default);
        ResourceDownloadPlan PrepareDownload();
        UniTask DownloadAsync(IProgress<ResourceDownloadProgress> progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears unused bundle and manifest cache against the active package manifest.
        /// </summary>
        UniTask ClearUnusedCacheAsync(CancellationToken cancellationToken = default);

        UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object;

        UniTask<ISceneHandle> LoadSceneAsync(
            string location,
            ResourceSceneLoadMode loadMode = ResourceSceneLoadMode.Single,
            CancellationToken cancellationToken = default);

        UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken cancellationToken = default);

        void UnloadUnused();
    }
}
