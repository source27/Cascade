using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cascade.Service
{
    /// <summary>
    /// Zero-dependency default provider: <c>UnityEngine.Resources</c> + <c>SceneManager</c>.
    /// Everything it can load must live under a <c>Resources/</c> folder (raw bytes: a TextAsset there);
    /// scenes must be in Build Settings. Loads are synchronous — the <see cref="IResourceService"/>
    /// surface stays async so a project can swap in an integration provider (YooAsset / Addressables)
    /// via <c>BootstrapBase.CreateResourceService()</c> without touching call sites.
    /// </summary>
    public sealed class UnityResourcesService : IResourceService, IDisposable
    {
        private bool _initialized;
        private bool _disposed;

        public bool IsInitialized => _initialized;

        /// <summary>There is nothing to configure for Resources; options are ignored.</summary>
        public UniTask InitializeAsync(ResourceInitOptions options, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            _initialized = true;
            return UniTask.CompletedTask;
        }

        public UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            EnsureInitialized();
            ValidateLocation(location);
            cancellationToken.ThrowIfCancellationRequested();

            var asset = Resources.Load<T>(location);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Resources.Load<{typeof(T).Name}>('{location}') returned null. " +
                    "Assets must live under a Resources/ folder, and the generic type must match the asset.");
            }

            return UniTask.FromResult<IAssetHandle<T>>(new UnityResourcesAssetHandle<T>(asset));
        }

        public UniTask<ISceneHandle> LoadSceneAsync(
            string location,
            ResourceSceneLoadMode loadMode = ResourceSceneLoadMode.Single,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateLocation(location);
            cancellationToken.ThrowIfCancellationRequested();

            var mode = loadMode == ResourceSceneLoadMode.Additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            var operation = SceneManager.LoadSceneAsync(location, mode);
            if (operation == null)
            {
                throw new InvalidOperationException(
                    $"SceneManager.LoadSceneAsync('{location}') returned null. " +
                    "The scene must exist in Build Settings.");
            }

            return UniTask.FromResult<ISceneHandle>(new UnityResourcesSceneHandle(location, operation));
        }

        public UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateLocation(location);
            cancellationToken.ThrowIfCancellationRequested();

            var asset = Resources.Load<TextAsset>(location);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"LoadRawBytesAsync expects a TextAsset under a Resources/ folder; " +
                    $"Resources.Load<TextAsset>('{location}') returned null.");
            }

            return UniTask.FromResult(asset.bytes);
        }

        /// <summary>Full <c>Resources.UnloadUnusedAssets()</c> sweep (editor-time cost); call sparingly.</summary>
        public void UnloadUnused()
        {
            Resources.UnloadUnusedAssets();
        }

        public void Dispose()
        {
            _disposed = true;
            _initialized = false;
        }

        private void EnsureInitialized()
        {
            ThrowIfDisposed();
            if (!_initialized)
                throw new InvalidOperationException($"{nameof(UnityResourcesService)} is not initialized.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UnityResourcesService));
        }

        private static void ValidateLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentException("Resources location is required.", nameof(location));
        }

        private sealed class UnityResourcesAssetHandle<T> : IAssetHandle<T> where T : UnityEngine.Object
        {
            private bool _released;

            public UnityResourcesAssetHandle(T asset)
            {
                Asset = asset;
            }

            public T Asset { get; }

            public bool IsValid => !_released && Asset != null;

            /// <summary>
            /// Drops the handle. Unity forbids <c>Resources.UnloadAsset</c> for GameObjects/Components
            /// (their memory is owned by the scene); other asset types are unloaded immediately.
            /// </summary>
            public void Release()
            {
                if (_released)
                    return;
                _released = true;
                if (Asset == null || Asset is GameObject || Asset is Component)
                    return;
                Resources.UnloadAsset(Asset);
            }

            public void Dispose() => Release();
        }

        private sealed class UnityResourcesSceneHandle : ISceneHandle
        {
            private readonly AsyncOperation _operation;
            private readonly string _sceneName;
            private bool _released;

            public UnityResourcesSceneHandle(string sceneName, AsyncOperation operation)
            {
                _sceneName = sceneName;
                _operation = operation;
            }

            public string SceneName => _released ? string.Empty : _sceneName;

            public bool IsValid => !_released;

            public async UniTask UnloadAsync(CancellationToken cancellationToken = default)
            {
                if (_released)
                    return;
                _released = true;

                if (_operation != null && !_operation.isDone)
                    await _operation.ToUniTask(cancellationToken: cancellationToken);

                var scene = SceneManager.GetSceneByName(_sceneName);
                if (!scene.IsValid() || !scene.isLoaded)
                    return;

                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                    await unload.ToUniTask(cancellationToken: cancellationToken);
            }

            /// <summary>Invalidates the handle only — loading a scene is undone by <see cref="UnloadAsync"/>.</summary>
            public void Release() => _released = true;
        }
    }
}
