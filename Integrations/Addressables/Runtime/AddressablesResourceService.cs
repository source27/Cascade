using System;
using System.Threading;
using Cascade.Service;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Cascade.Service.Addressables
{
    /// <summary>Slim init options; most behavior comes from AddressableAssetSettings.</summary>
    public sealed class AddressablesResourceInitOptions
    {
        public AddressablesResourceInitOptions(bool autoReleaseInitHandle = true)
        {
            AutoReleaseInitHandle = autoReleaseInitHandle;
        }

        public bool AutoReleaseInitHandle { get; }
    }

    /// <summary>
    /// Load-only Addressables provider. No catalog-update APIs on this type.
    /// LoadRawBytesAsync treats location as a TextAsset address.
    /// UnloadUnused is a documented no-op (rely on handle Release refcounts).
    /// </summary>
    public sealed class AddressablesResourceService : IResourceService, IDisposable
    {
        private readonly AddressablesResourceInitOptions _options;
        private bool _initialized;
        private bool _disposed;

        /// <summary>Options are construction-time state; <c>null</c> means "Addressables defaults".</summary>
        public AddressablesResourceService(AddressablesResourceInitOptions options = null)
        {
            _options = options ?? new AddressablesResourceInitOptions();
        }

        public bool IsInitialized => _initialized;

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var handle = UnityEngine.AddressableAssets.Addressables.InitializeAsync(_options.AutoReleaseInitHandle);
            await handle.ToUniTask(cancellationToken: cancellationToken);
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException($"Addressables.InitializeAsync failed: {handle.OperationException}");
            _initialized = true;
        }

        public async UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            EnsureInitialized();
            ValidateLocation(location);

            AsyncOperationHandle<T> handle = default;
            try
            {
                handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(location);
                await handle.ToUniTask(cancellationToken: cancellationToken);
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    var detail = FormatOperationFailure(handle.Status, handle.OperationException);
                    ReleaseIfValid(handle);
                    throw new InvalidOperationException(
                        $"Failed to load Addressables asset type={typeof(T).Name} location='{location}': {detail}");
                }

                return new AddressablesAssetHandle<T>(handle);
            }
            catch (OperationCanceledException)
            {
                ReleaseIfValid(handle);
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ReleaseIfValid(handle);
                throw new InvalidOperationException(
                    $"Failed to load Addressables asset type={typeof(T).Name} location='{location}'.",
                    exception);
            }
        }

        public async UniTask<ISceneHandle> LoadSceneAsync(
            string location,
            ResourceSceneLoadMode loadMode = ResourceSceneLoadMode.Single,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateLocation(location);

            var mode = loadMode == ResourceSceneLoadMode.Additive ? LoadSceneMode.Additive : LoadSceneMode.Single;
            AsyncOperationHandle<SceneInstance> handle = default;
            try
            {
                handle = UnityEngine.AddressableAssets.Addressables.LoadSceneAsync(location, mode);
                await handle.ToUniTask(cancellationToken: cancellationToken);
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    var detail = FormatOperationFailure(handle.Status, handle.OperationException);
                    ReleaseIfValid(handle);
                    throw new InvalidOperationException(
                        $"Failed to load Addressables scene location='{location}': {detail}");
                }

                return new AddressablesSceneHandle(handle);
            }
            catch (OperationCanceledException)
            {
                ReleaseIfValid(handle);
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ReleaseIfValid(handle);
                throw new InvalidOperationException(
                    $"Failed to load Addressables scene location='{location}'.",
                    exception);
            }
        }

        public async UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ValidateLocation(location);

            AsyncOperationHandle<TextAsset> handle = default;
            try
            {
                handle = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<TextAsset>(location);
                await handle.ToUniTask(cancellationToken: cancellationToken);
                if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
                {
                    var detail = FormatOperationFailure(handle.Status, handle.OperationException);
                    throw new InvalidOperationException(
                        $"LoadRawBytesAsync expects a TextAsset address; failed type=TextAsset location='{location}': {detail}");
                }

                return handle.Result.bytes;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"LoadRawBytesAsync failed type=TextAsset location='{location}'.",
                    exception);
            }
            finally
            {
                ReleaseIfValid(handle);
            }
        }


        /// <summary>No-op: Addressables unloads via handle Release refcounts.</summary>
        public void UnloadUnused()
        {
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
                throw new InvalidOperationException("AddressablesResourceService is not initialized.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AddressablesResourceService));
        }

        private static void ValidateLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentException("Addressables location is required.", nameof(location));
        }

        private static string FormatOperationFailure(AsyncOperationStatus status, Exception operationException)
        {
            if (operationException != null)
                return operationException.ToString();
            return $"status={status}";
        }

        private static void ReleaseIfValid<T>(AsyncOperationHandle<T> handle)
        {
            if (handle.IsValid())
                UnityEngine.AddressableAssets.Addressables.Release(handle);
        }


        private sealed class AddressablesAssetHandle<T> : IAssetHandle<T> where T : UnityEngine.Object
        {
            private AsyncOperationHandle<T> _handle;
            private bool _released;

            public AddressablesAssetHandle(AsyncOperationHandle<T> handle)
            {
                _handle = handle;
            }

            public T Asset => _released || !_handle.IsValid() ? null : _handle.Result;
            public bool IsValid => !_released && _handle.IsValid() && _handle.Status == AsyncOperationStatus.Succeeded;

            public void Release()
            {
                if (_released)
                    return;
                _released = true;
                if (_handle.IsValid())
                    UnityEngine.AddressableAssets.Addressables.Release(_handle);
            }

            public void Dispose() => Release();
        }

        private sealed class AddressablesSceneHandle : ISceneHandle
        {
            private AsyncOperationHandle<SceneInstance> _handle;
            private bool _released;

            public AddressablesSceneHandle(AsyncOperationHandle<SceneInstance> handle)
            {
                _handle = handle;
            }

            public string SceneName =>
                _released || !_handle.IsValid() ? string.Empty : _handle.Result.Scene.name;

            public bool IsValid => !_released && _handle.IsValid() && _handle.Status == AsyncOperationStatus.Succeeded;

            public async UniTask UnloadAsync(CancellationToken cancellationToken = default)
            {
                if (_released || !_handle.IsValid())
                    return;
                var unload = UnityEngine.AddressableAssets.Addressables.UnloadSceneAsync(_handle);
                await unload.ToUniTask(cancellationToken: cancellationToken);
                _released = true;
            }

            public void Release()
            {
                if (_released)
                    return;
                if (_handle.IsValid())
                    UnityEngine.AddressableAssets.Addressables.Release(_handle);
                _released = true;
            }
        }
    }
}
