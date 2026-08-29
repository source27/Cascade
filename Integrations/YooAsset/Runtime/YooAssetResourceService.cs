using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using YooAsset;
using Cascade.Service;

namespace Cascade.Service.YooAsset
{
    /// <summary>YooAsset resource play modes.</summary>
    public enum YooAssetResourcePlayMode
    {
        EditorSimulate,
        Offline,
        Host
    }

    /// <summary>YooAsset-specific resource initialization options.</summary>
    public sealed class YooAssetResourceInitOptions : ResourceInitOptions
    {
        public YooAssetResourceInitOptions(
            string packageName,
            YooAssetResourcePlayMode playMode,
            string remoteRoot = null,
            bool useBuiltinPackage = false)
        {
            PackageName = packageName;
            PlayMode = playMode;
            RemoteRoot = remoteRoot;
            UseBuiltinPackage = useBuiltinPackage;
        }

        public string PackageName { get; }
        public YooAssetResourcePlayMode PlayMode { get; }
        public string RemoteRoot { get; }
        public bool UseBuiltinPackage { get; }
    }

    public sealed class YooAssetResourceService : IResourceService, IDisposable
    {
        private ResourcePackage _package;
        private YooAssetResourceInitOptions _options;
        private ResourceDownloaderOperation _pendingDownloader;
        private bool _isUsingLocalVersion;
        private bool _disposed;

        public bool IsInitialized => _package != null && _package.InitializeStatus == EOperationStatus.Succeeded;
        public bool IsUsingLocalVersion => _isUsingLocalVersion;

        public string ActivePackageVersion
        {
            get
            {
                if (!IsInitialized)
                    return string.Empty;
                try
                {
                    return _package.GetPackageVersion() ?? string.Empty;
                }
                catch
                {
                    return string.Empty;
                }
            }
        }

        public async UniTask InitializeAsync(ResourceInitOptions options, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var opts = options as YooAssetResourceInitOptions
                ?? throw new InvalidOperationException(
                    "YooAssetResourceService requires YooAssetResourceInitOptions (assign one to BootstrapConfiguration.ResourceInitOptions).");
            _options = opts;

            if (!YooAssets.IsInitialized)
                YooAssets.Initialize();

            if (!YooAssets.TryGetPackage(opts.PackageName, out _package))
                _package = YooAssets.CreatePackage(opts.PackageName);

            if (_package.InitializeStatus == EOperationStatus.Succeeded)
                return;

            InitializePackageOperation operation;
            switch (opts.PlayMode)
            {
                case YooAssetResourcePlayMode.EditorSimulate:
#if UNITY_EDITOR
                    var simulateBuild = EditorSimulateBuildInvoker.Build(opts.PackageName, (int)EBundleType.VirtualAssetBundle);
                    var editorOptions = new EditorSimulateModeOptions
                    {
                        EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(simulateBuild.PackageRootDirectory)
                    };
                    editorOptions.EditorFileSystemParameters.AddParameter(EFileSystemParameter.VirtualWebglMode, true);
                    editorOptions.EditorFileSystemParameters.AddParameter(EFileSystemParameter.VirtualDownloadMode, true);
                    editorOptions.EditorFileSystemParameters.AddParameter(EFileSystemParameter.VirtualDownloadSpeed, 1024 * 1000);
                    editorOptions.EditorFileSystemParameters.AddParameter(EFileSystemParameter.AsyncSimulateMinFrame, 1);
                    editorOptions.EditorFileSystemParameters.AddParameter(EFileSystemParameter.AsyncSimulateMaxFrame, 3);
                    operation = _package.InitializePackageAsync(editorOptions);
                    break;
#else
                    throw new InvalidOperationException("EditorSimulate is only available in the Unity Editor.");
#endif
                case YooAssetResourcePlayMode.Offline:
                    operation = _package.InitializePackageAsync(new OfflinePlayModeOptions
                    {
                        BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
                    });
                    break;
                case YooAssetResourcePlayMode.Host:
                    if (string.IsNullOrWhiteSpace(opts.RemoteRoot))
                        throw new InvalidOperationException("Host play mode requires RemoteRoot.");
                    var remoteService = new RemoteService(opts.RemoteRoot);
                    // Host = optional Builtin(StreamingAssets 首包) + Cache(CDN).
                    var hostOptions = new HostPlayModeOptions
                    {
                        BuiltinFileSystemParameters = opts.UseBuiltinPackage
                            ? FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
                            : null,
                        CacheFileSystemParameters = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService)
                    };
                    if (hostOptions.BuiltinFileSystemParameters != null)
                        hostOptions.BuiltinFileSystemParameters.AddParameter(EFileSystemParameter.CopyBuiltinPackageManifest, true);
                    hostOptions.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadMaxConcurrency, 5);
                    hostOptions.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadMaxRequestPerFrame, 1);
                    hostOptions.CacheFileSystemParameters.AddParameter(EFileSystemParameter.DownloadWatchdogTimeout, 10);
                    operation = _package.InitializePackageAsync(hostOptions);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            await WaitAsync(operation, cancellationToken);
            if (operation.Status != EOperationStatus.Succeeded)
                throw new InvalidOperationException(operation.Error);
        }

        public async UniTask<string> RequestVersionAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            _isUsingLocalVersion = false;
            var localVersion = await ResolveLocalPackageVersionAsync(cancellationToken);
            var operation = _package.RequestPackageVersionAsync(new RequestPackageVersionOptions(true, 8));
            await WaitAsync(operation, cancellationToken);
            if (operation.Status == EOperationStatus.Succeeded)
            {
                var remoteVersion = operation.PackageVersion;
                if (!string.IsNullOrWhiteSpace(localVersion) &&
                    ComparePackageVersion(localVersion, remoteVersion) > 0)
                {
                    Debug.Log(
                        $"[YooAssetResourceService] Skip package downgrade: local={localVersion}, remote={remoteVersion}");
                    return localVersion;
                }

                return remoteVersion;
            }

            if (!string.IsNullOrWhiteSpace(localVersion))
            {
                _isUsingLocalVersion = true;
                return localVersion;
            }

            throw new InvalidOperationException(operation.Error);
        }

        public async UniTask UpdateManifestAsync(string version, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Package version is required.", nameof(version));

            var operation = _package.LoadPackageManifestAsync(new LoadPackageManifestOptions(version, 15));
            await WaitAsync(operation, cancellationToken);
            if (operation.Status == EOperationStatus.Succeeded)
                return;

            if (!_isUsingLocalVersion)
            {
                var localVersion = await ResolveLocalPackageVersionAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(localVersion) && !string.Equals(localVersion, version, StringComparison.Ordinal))
                {
                    var fallback = _package.LoadPackageManifestAsync(new LoadPackageManifestOptions(localVersion, 15));
                    await WaitAsync(fallback, cancellationToken);
                    if (fallback.Status == EOperationStatus.Succeeded)
                    {
                        _isUsingLocalVersion = true;
                        return;
                    }
                }
            }

            throw new InvalidOperationException(operation.Error);
        }

        public ResourceDownloadPlan PrepareDownload()
        {
            EnsureInitialized();
            _pendingDownloader = _package.CreateResourceDownloader(new ResourceDownloaderOptions(5, 3));
            var plan = new ResourceDownloadPlan(
                _pendingDownloader.TotalDownloadCount,
                _pendingDownloader.TotalDownloadBytes);
            if (!plan.NeedsDownload)
                SaveLastKnownGoodVersion(ActivePackageVersion);
            return plan;
        }

        public async UniTask DownloadAsync(IProgress<ResourceDownloadProgress> progress = null, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            var downloader = _pendingDownloader ?? _package.CreateResourceDownloader(new ResourceDownloaderOptions(5, 3));
            _pendingDownloader = null;

            if (downloader.TotalDownloadCount == 0)
            {
                progress?.Report(new ResourceDownloadProgress(0, 0, 0, 0));
                return;
            }

            downloader.StartDownload();
            while (!downloader.IsDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ResourceDownloadProgress(
                    downloader.TotalDownloadCount,
                    downloader.CurrentDownloadCount,
                    downloader.TotalDownloadBytes,
                    downloader.CurrentDownloadBytes));
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            if (downloader.Status != EOperationStatus.Succeeded)
                throw new InvalidOperationException(downloader.Error);

            SaveLastKnownGoodVersion(ActivePackageVersion);
        }

        public async UniTask ClearUnusedCacheAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            await ClearCacheAsync(ClearCacheMethods.ClearUnusedBundleFiles, cancellationToken);
            await ClearCacheAsync(ClearCacheMethods.ClearUnusedManifestFiles, cancellationToken);
        }

        public async UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentException("Location is required.", nameof(location));

            var handle = _package.LoadAssetAsync<T>(location);
            await WaitHandleAsync(handle, cancellationToken);
            if (handle.Status != EOperationStatus.Succeeded)
            {
                handle.Release();
                throw new InvalidOperationException(handle.Error);
            }

            return new YooAssetHandle<T>(handle);
        }

        public async UniTask<ISceneHandle> LoadSceneAsync(
            string location,
            ResourceSceneLoadMode loadMode = ResourceSceneLoadMode.Single,
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentException("Location is required.", nameof(location));

            var sceneMode = loadMode == ResourceSceneLoadMode.Single
                ? LoadSceneMode.Single
                : loadMode == ResourceSceneLoadMode.Additive
                    ? LoadSceneMode.Additive
                    : throw new ArgumentOutOfRangeException(nameof(loadMode), loadMode, "Unknown scene load mode.");

            // Unity cannot safely cancel a Single load after activation begins: the previous scene
            // may already be gone. Treat scene replacement as an atomic commit once requested.
            cancellationToken.ThrowIfCancellationRequested();
            var handle = _package.LoadSceneAsync(location, sceneMode);
            if (loadMode == ResourceSceneLoadMode.Single)
            {
                await WaitHandleAsync(handle, CancellationToken.None);
            }
            else
            {
                try
                {
                    await WaitHandleAsync(handle, cancellationToken);
                }
                catch
                {
                    try
                    {
                        var unload = handle.UnloadSceneAsync();
                        await WaitAsync(unload, CancellationToken.None);
                    }
                    finally
                    {
                        if (handle.IsValid)
                            handle.Release();
                    }

                    throw;
                }
            }
            if (handle.Status != EOperationStatus.Succeeded)
            {
                handle.Release();
                throw new InvalidOperationException(handle.Error);
            }

            return new YooSceneHandle(handle);
        }

        public async UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentException("Location is required.", nameof(location));

            AssetInfo assetInfo;
            try
            {
                assetInfo = _package.GetAssetInfo(location);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Failed to resolve asset info: {location}", exception);
            }

#if UNITY_EDITOR
            if (_options != null && _options.PlayMode == YooAssetResourcePlayMode.EditorSimulate)
            {
                if (!assetInfo.IsValid)
                    throw new InvalidOperationException(
                        $"Raw address not found in the editor-simulated package: '{location}'. " +
                        "Check the YooAsset collector setting (BundleCollectorSetting.asset) includes the asset with AddressByFileName.");
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    throw new InvalidOperationException("Unable to resolve project root.");
                return File.ReadAllBytes(Path.Combine(projectRoot, assetInfo.AssetPath));
            }
#endif

            var handle = _package.LoadAssetAsync(assetInfo);
            await WaitHandleAsync(handle, cancellationToken);
            if (handle.Status != EOperationStatus.Succeeded)
            {
                handle.Release();
                throw new InvalidOperationException($"{location}: {handle.Error}");
            }

            try
            {
                var bytes = GetRawAssetBytes(handle.AssetObject);
                if (bytes == null || bytes.Length == 0)
                    throw new InvalidOperationException($"Raw asset is empty: {location}");
                return bytes;
            }
            finally
            {
                handle.Release();
            }
        }

        public void UnloadUnused()
        {
            if (!IsInitialized)
                return;
            _package.UnloadUnusedAssetsAsync();
        }

        public void Dispose()
        {
            _disposed = true;
            _package = null;
            _options = null;
        }

        private void EnsureInitialized()
        {
            ThrowIfDisposed();
            if (!IsInitialized)
                throw new InvalidOperationException("Resource service is not initialized.");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(YooAssetResourceService));
        }

        private async UniTask<string> ResolveLocalPackageVersionAsync(CancellationToken cancellationToken)
        {
            var saved = PlayerPrefs.GetString(GetLastKnownGoodVersionKey(), string.Empty);
            if (!string.IsNullOrWhiteSpace(saved))
                saved = saved.Trim();
            else
                saved = null;

            var builtin = await ReadBuiltinPackageVersionAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(saved))
                return builtin;
            if (string.IsNullOrWhiteSpace(builtin))
                return saved;
            return ComparePackageVersion(saved, builtin) >= 0 ? saved : builtin;
        }

        private async UniTask<string> ReadBuiltinPackageVersionAsync(CancellationToken cancellationToken)
        {
            if (_options == null || !_options.UseBuiltinPackage)
                return null;

            // Builtin root includes YooFolderName (assetpack), then package name.
            var versionUrl = CombineUrl(
                Application.streamingAssetsPath,
                YooAssetConfiguration.GetYooFolderName(),
                _options.PackageName,
                YooAssetConfiguration.GetPackageVersionFileName(_options.PackageName));

            using (var request = UnityWebRequest.Get(versionUrl))
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (request.result != UnityWebRequest.Result.Success)
                    return null;
                var version = request.downloadHandler.text?.Trim();
                return string.IsNullOrWhiteSpace(version) ? null : version;
            }
        }

        /// <summary>
        /// Package versions use appVersion_sequence (e.g. 0.0.1_1). Same app prefix compares sequence as int.
        /// </summary>
        public static int ComparePackageVersion(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) && string.IsNullOrWhiteSpace(right))
                return 0;
            if (string.IsNullOrWhiteSpace(left))
                return -1;
            if (string.IsNullOrWhiteSpace(right))
                return 1;

            var leftText = left.Trim();
            var rightText = right.Trim();
            if (TryParsePackageVersion(leftText, out var leftApp, out var leftSequence) &&
                TryParsePackageVersion(rightText, out var rightApp, out var rightSequence))
            {
                var appCompare = string.CompareOrdinal(leftApp, rightApp);
                if (appCompare != 0)
                    return appCompare;
                return leftSequence.CompareTo(rightSequence);
            }

            return string.CompareOrdinal(leftText, rightText);
        }

        public static bool TryParsePackageVersion(string packageVersion, out string applicationVersion, out int sequence)
        {
            applicationVersion = null;
            sequence = 0;
            if (string.IsNullOrWhiteSpace(packageVersion))
                return false;

            var value = packageVersion.Trim();
            var separator = value.LastIndexOf('_');
            if (separator <= 0 || separator >= value.Length - 1)
                return false;

            applicationVersion = value.Substring(0, separator);
            if (string.IsNullOrWhiteSpace(applicationVersion))
                return false;

            return int.TryParse(
                       value.Substring(separator + 1),
                       System.Globalization.NumberStyles.Integer,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out sequence) &&
                   sequence > 0;
        }

        private void SaveLastKnownGoodVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return;
            PlayerPrefs.SetString(GetLastKnownGoodVersionKey(), version);
            PlayerPrefs.Save();
        }

        private string GetLastKnownGoodVersionKey()
        {
            return $"cascade.yooasset.{Application.version}.{_options.PackageName}.lastKnownGoodVersion";
        }

        private static string CombineUrl(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
                return string.Empty;

            var result = parts[0]?.TrimEnd('/', '\\') ?? string.Empty;
            for (var i = 1; i < parts.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(parts[i]))
                    continue;
                result += "/" + parts[i].Trim('/', '\\');
            }
            return result;
        }

        private async UniTask ClearCacheAsync(string clearMethod, CancellationToken cancellationToken)
        {
            var operation = _package.ClearCacheAsync(new ClearCacheOptions(clearMethod));
            await WaitAsync(operation, cancellationToken);
            if (operation.Status != EOperationStatus.Succeeded)
                throw new InvalidOperationException($"Clear cache failed ({clearMethod}): {operation.Error}");
        }

        private static async UniTask WaitAsync(AsyncOperationBase operation, CancellationToken cancellationToken)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            while (!operation.IsDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private static async UniTask WaitHandleAsync(HandleBase handle, CancellationToken cancellationToken)
        {
            if (handle == null)
                throw new ArgumentNullException(nameof(handle));

            while (!handle.IsDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private static byte[] GetRawAssetBytes(UnityEngine.Object assetObject)
        {
            if (assetObject is RawFileObject rawFile)
                return rawFile.GetBytes();
            if (assetObject is TextAsset textAsset)
                return textAsset.bytes;
            return null;
        }

        private sealed class YooAssetHandle<T> : IAssetHandle<T> where T : UnityEngine.Object
        {
            private AssetHandle _handle;
            private bool _released;

            public YooAssetHandle(AssetHandle handle)
            {
                _handle = handle;
            }

            public T Asset => _released ? null : _handle.AssetObject as T;
            public bool IsValid => !_released && _handle != null && _handle.IsValid;

            public void Release()
            {
                if (_released)
                    return;
                _released = true;
                _handle?.Release();
                _handle = null;
            }

            public void Dispose()
            {
                Release();
            }
        }

        private sealed class YooSceneHandle : ISceneHandle
        {
            private SceneHandle _handle;
            private bool _released;

            public YooSceneHandle(SceneHandle handle)
            {
                _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            }

            public string SceneName => _released ? string.Empty : _handle.SceneName;
            public bool IsValid => !_released && _handle != null && _handle.IsValid;

            public async UniTask UnloadAsync(CancellationToken cancellationToken = default)
            {
                if (_released)
                    return;

                // During editor play-mode teardown YooAssets is destroyed (OnApplicationQuit) before
                // our shutdown chain runs; there is nothing to unload then, just release the handle.
                if (!YooAssets.IsInitialized)
                {
                    Release();
                    return;
                }

                var operation = _handle.UnloadSceneAsync();
                await WaitAsync(operation, cancellationToken);
                if (operation.Status != EOperationStatus.Succeeded)
                    throw new InvalidOperationException(operation.Error);

                _released = true;
                _handle = null;
            }

            public void Release()
            {
                if (_released)
                    return;

                _released = true;
                _handle?.Release();
                _handle = null;
            }
        }

        private sealed class RemoteService : IRemoteService
        {
            private readonly string _remoteRoot;

            public RemoteService(string remoteRoot)
            {
                _remoteRoot = remoteRoot.TrimEnd('/');
            }

            public IReadOnlyList<string> GetRemoteUrls(string fileName)
            {
                return new[] { $"{_remoteRoot}/{fileName}" };
            }
        }
    }
}
