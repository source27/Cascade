using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.U2D;

using Cascade.Service;
namespace Cascade.Service
{
    /// <summary>
    /// SpriteAtlas-backed sprite loader. AtlasMapping.bytes (spriteName -&gt; atlasName)
    /// is loaded once and cached; SpriteAtlas handles are cached for the service lifetime.
    /// </summary>
    public sealed class AtlasSpriteService : IAtlasSpriteService, IDisposable
    {
        public const string IndexAddress = "AtlasMapping";

        private readonly IResourceService _resources;
        private readonly ILogService _log;
        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, string> _spriteToAtlas = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, IAssetHandle<SpriteAtlas>> _atlasHandles = new Dictionary<string, IAssetHandle<SpriteAtlas>>(StringComparer.Ordinal);
        private readonly Dictionary<string, UniTaskCompletionSource<IAssetHandle<SpriteAtlas>>> _pendingAtlasLoads = new Dictionary<string, UniTaskCompletionSource<IAssetHandle<SpriteAtlas>>>(StringComparer.Ordinal);
        private UniTaskCompletionSource _indexLoadSource;
        private bool _disposed;

        public AtlasSpriteService(IResourceService resources, ILogService log = null)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _log = log;
        }

        public async UniTask<Sprite> LoadSpriteAsync(string spriteName, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return null;
            if (string.IsNullOrWhiteSpace(spriteName))
                return null;

            try
            {
                await EnsureIndexLoadedAsync(cancellationToken);

                string atlasName;
                lock (_syncRoot)
                {
                    if (!_spriteToAtlas.TryGetValue(spriteName, out atlasName))
                        return null;
                }

                var handle = await GetOrLoadAtlasAsync(atlasName, cancellationToken);
                var atlas = handle?.Asset;
                if (atlas == null)
                    return null;

                var sprite = atlas.GetSprite(spriteName);
                if (sprite == null)
                {
                    _log?.Warning(nameof(AtlasSpriteService), $"Sprite '{spriteName}' missing in atlas '{atlasName}'.");
                    return null;
                }
                return sprite;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception exception)
            {
                _log?.Exception(nameof(AtlasSpriteService), exception, $"Sprite load failed: '{spriteName}'");
                return null;
            }
        }

        public bool TryGetAtlasName(string spriteName, out string atlasName)
        {
            lock (_syncRoot)
                return _spriteToAtlas.TryGetValue(spriteName, out atlasName);
        }

        public void Reset()
        {
            lock (_syncRoot)
            {
                foreach (var handle in _atlasHandles.Values)
                {
                    try
                    {
                        handle?.Release();
                    }
                    catch
                    {
                        // Teardown must continue for the remaining handles.
                    }
                }
                _atlasHandles.Clear();
                _pendingAtlasLoads.Clear();
                _spriteToAtlas.Clear();
                _indexLoadSource = null;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Reset();
        }

        /// <summary>
        /// Parses AtlasMapping.bytes written by UIAtlasAutoPackGenerator.
        /// Format: int32 count, then count pairs of UTF-8 strings (spriteName, atlasName).
        /// </summary>
        public static Dictionary<string, string> ParseIndex(byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var reader = new BinaryReader(new MemoryStream(bytes), Encoding.UTF8))
            {
                var count = reader.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var spriteName = reader.ReadString();
                    var atlasName = reader.ReadString();
                    if (!string.IsNullOrEmpty(spriteName) && !string.IsNullOrEmpty(atlasName))
                        result[spriteName] = atlasName;
                }
            }
            return result;
        }

        private async UniTask EnsureIndexLoadedAsync(CancellationToken cancellationToken)
        {
            UniTaskCompletionSource source;
            lock (_syncRoot)
            {
                if (_indexLoadSource == null)
                {
                    _indexLoadSource = new UniTaskCompletionSource();
                    LoadIndexAndCompleteAsync(_indexLoadSource).Forget();
                }
                source = _indexLoadSource;
            }

            await source.Task.AttachExternalCancellation(cancellationToken);
        }

        private async UniTaskVoid LoadIndexAndCompleteAsync(UniTaskCompletionSource source)
        {
            try
            {
                var bytes = await _resources.LoadRawBytesAsync(IndexAddress, CancellationToken.None);
                if (bytes == null || bytes.Length == 0)
                    throw new InvalidOperationException($"Empty atlas mapping: {IndexAddress}");

                var parsed = ParseIndex(bytes);
                lock (_syncRoot)
                {
                    _spriteToAtlas.Clear();
                    foreach (var pair in parsed)
                        _spriteToAtlas[pair.Key] = pair.Value;
                }
                source.TrySetResult();
            }
            catch (Exception exception)
            {
                lock (_syncRoot)
                    _indexLoadSource = null;
                source.TrySetException(exception);
            }
        }

        private async UniTask<IAssetHandle<SpriteAtlas>> GetOrLoadAtlasAsync(
            string atlasName,
            CancellationToken cancellationToken)
        {
            UniTaskCompletionSource<IAssetHandle<SpriteAtlas>> pending;
            lock (_syncRoot)
            {
                if (_atlasHandles.TryGetValue(atlasName, out var cached))
                    return cached;
                if (!_pendingAtlasLoads.TryGetValue(atlasName, out pending))
                {
                    pending = new UniTaskCompletionSource<IAssetHandle<SpriteAtlas>>();
                    _pendingAtlasLoads[atlasName] = pending;
                    LoadAtlasAndCompleteAsync(pending, atlasName).Forget();
                }
            }

            return await pending.Task.AttachExternalCancellation(cancellationToken);
        }

        private async UniTaskVoid LoadAtlasAndCompleteAsync(
            UniTaskCompletionSource<IAssetHandle<SpriteAtlas>> pending,
            string atlasName)
        {
            IAssetHandle<SpriteAtlas> handle = null;
            try
            {
                handle = await _resources.LoadAssetAsync<SpriteAtlas>(atlasName, CancellationToken.None);
                if (handle == null || !handle.IsValid || handle.Asset == null)
                {
                    handle?.Release();
                    handle = null;
                    throw new InvalidOperationException($"Failed to load atlas: {atlasName}");
                }

                lock (_syncRoot)
                {
                    _atlasHandles[atlasName] = handle;
                    _pendingAtlasLoads.Remove(atlasName);
                }
                pending.TrySetResult(handle);
            }
            catch (Exception exception)
            {
                lock (_syncRoot)
                    _pendingAtlasLoads.Remove(atlasName);
                pending.TrySetException(exception);
            }
        }
    }
}