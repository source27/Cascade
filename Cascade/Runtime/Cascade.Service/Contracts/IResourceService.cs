using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Service
{
    /// <summary>
    /// Provider-neutral resource initialization options. Resource providers (YooAsset,
    /// Addressables, …) subclass this with their own configuration; the composition root
    /// assigns a concrete instance via Bootstrap configuration.
    /// </summary>
    public class ResourceInitOptions
    {
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

    /// <summary>
    /// Load-only resource contract. Version/download/update APIs live on concrete
    /// integration types (e.g. YooAssetResourceService), not on this interface.
    /// </summary>
    public interface IResourceService
    {
        bool IsInitialized { get; }

        UniTask InitializeAsync(ResourceInitOptions options, CancellationToken cancellationToken = default);

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
