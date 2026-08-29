using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Service
{
    public sealed class AudioService : IAudioService, IDisposable
    {
        private const string LogCategory = "Audio";

        private readonly IResourceService _resources;
        private readonly ILogService _log;
        private readonly Dictionary<string, IAssetHandle<AudioClip>> _clips =
            new Dictionary<string, IAssetHandle<AudioClip>>(StringComparer.Ordinal);
        private readonly Stack<AudioEntity> _pool = new Stack<AudioEntity>();
        private readonly HashSet<AudioEntity> _activeEffects = new HashSet<AudioEntity>();
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly GameObject _root;
        private readonly AudioEntity _bgm;
        private readonly AudioEntity _voice;

        private string _currentBgm = string.Empty;
        private string _currentVoice = string.Empty;
        private float _masterVolume = 1f;
        private float _musicVolume = 1f;
        private float _sfxVolume = 1f;
        private int _bgmRequest;
        private int _voiceRequest;
        private bool _disposed;

        public AudioService(IResourceService resources, ILogService log = null)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _log = log;
            _root = new GameObject("AudioService");
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(_root);
            _bgm = CreateEntity("BGM");
            _voice = CreateEntity("Voice");
        }

        public async UniTask PreloadAsync(string location, CancellationToken cancellationToken = default)
        {
            await LoadClipAsync(location, cancellationToken);
        }

        public async UniTask PlayBgmAsync(
            string location,
            float volume = 1f,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var request = ++_bgmRequest;
            var clip = await LoadClipAsync(location, cancellationToken);
            if (_disposed || request != _bgmRequest)
                return;

            if (_currentBgm == location && _bgm.Source.isPlaying)
                return;

            _bgm.Stop();
            _currentBgm = location;
            _bgm.Play(clip, Mathf.Clamp01(volume), true, false, Vector3.zero, EffectiveMusicVolume);
        }

        public void PauseBgm()
        {
            if (!_disposed && _bgm.Source.isPlaying)
                _bgm.Source.Pause();
        }

        public void ResumeBgm()
        {
            if (!_disposed && _bgm.Source.clip != null && !_bgm.Source.isPlaying)
                _bgm.Source.UnPause();
        }

        public void StopBgm()
        {
            if (_disposed)
                return;
            ++_bgmRequest;
            _bgm.Stop();
            _currentBgm = string.Empty;
        }

        public UniTask<float> PlayOneShotAsync(
            string location,
            float volume = 1f,
            CancellationToken cancellationToken = default)
        {
            return PlayOneShotInternalAsync(location, Vector3.zero, false, volume, cancellationToken);
        }

        public UniTask<float> PlayOneShotAtAsync(
            string location,
            Vector3 position,
            float volume = 1f,
            CancellationToken cancellationToken = default)
        {
            return PlayOneShotInternalAsync(location, position, true, volume, cancellationToken);
        }

        public async UniTask PlayVoiceAsync(
            string location,
            float volume = 1f,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var request = ++_voiceRequest;
            var clip = await LoadClipAsync(location, cancellationToken);
            if (_disposed || request != _voiceRequest)
                return;

            _voice.Stop();
            _currentVoice = location;
            _voice.Play(clip, Mathf.Clamp01(volume), false, false, Vector3.zero, EffectiveSfxVolume);
        }

        public void StopVoice()
        {
            if (_disposed)
                return;
            ++_voiceRequest;
            _voice.Stop();
            _currentVoice = string.Empty;
        }

        public void SetVolume(float volume)
        {
            ThrowIfDisposed();
            _masterVolume = Mathf.Clamp01(volume);
            ApplyVolumes();
        }

        public void SetVolume(AudioChannel channel, float volume)
        {
            ThrowIfDisposed();
            volume = Mathf.Clamp01(volume);
            if (channel == AudioChannel.Music)
                _musicVolume = volume;
            else
                _sfxVolume = volume;
            ApplyVolumes();
        }

        public void Release(string location)
        {
            ThrowIfDisposed();
            ValidateLocation(location);

            if (_currentBgm == location)
                StopBgm();
            if (_currentVoice == location)
                StopVoice();

            ReleaseEffectsUsing(location);
            if (_clips.TryGetValue(location, out var handle))
            {
                handle.Release();
                _clips.Remove(location);
            }
        }

        public void ReleaseAll()
        {
            if (_disposed)
                return;

            if (_root != null)
            {
                StopBgm();
                StopVoice();
                ReleaseAllEffects();
            }
            foreach (var handle in _clips.Values)
                handle.Release();
            _clips.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _lifetime.Cancel();
            ReleaseAll();
            _disposed = true;
            _lifetime.Dispose();
            DestroyObject(_root);
        }

        private async UniTask<float> PlayOneShotInternalAsync(
            string location,
            Vector3 position,
            bool spatial,
            float volume,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            var clip = await LoadClipAsync(location, cancellationToken);
            ThrowIfDisposed();

            var entity = GetEffectEntity();
            entity.Location = location;
            entity.Play(clip, Mathf.Clamp01(volume), false, spatial, position, EffectiveSfxVolume);
            RecycleWhenFinishedAsync(entity, _lifetime.Token).Forget();
            return clip.length;
        }

        private async UniTask<AudioClip> LoadClipAsync(string location, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            ValidateLocation(location);
            if (_clips.TryGetValue(location, out var cached) && cached.IsValid && cached.Asset != null)
                return cached.Asset;

            var handle = await _resources.LoadAssetAsync<AudioClip>(location, cancellationToken);
            if (_disposed)
            {
                handle.Release();
                throw new ObjectDisposedException(nameof(AudioService));
            }

            if (handle.Asset == null)
            {
                handle.Release();
                throw new InvalidOperationException($"Audio clip is null: {location}");
            }

            if (_clips.TryGetValue(location, out cached) && cached.IsValid && cached.Asset != null)
            {
                handle.Release();
                return cached.Asset;
            }

            _clips[location] = handle;
            return handle.Asset;
        }

        private async UniTaskVoid RecycleWhenFinishedAsync(AudioEntity entity, CancellationToken cancellationToken)
        {
            try
            {
                await UniTask.WaitUntil(() => !entity.Source.isPlaying, cancellationToken: cancellationToken);
                if (!_disposed && _activeEffects.Remove(entity))
                    ReturnEffectEntity(entity);
            }
            catch (OperationCanceledException)
            {
            }
            catch (MissingReferenceException)
            {
            }
            catch (Exception exception)
            {
                _log?.Exception(LogCategory, exception, "Failed to recycle an audio source.");
            }
        }

        private AudioEntity GetEffectEntity()
        {
            var entity = _pool.Count > 0 ? _pool.Pop() : CreateEntity("SFX");
            entity.GameObject.SetActive(true);
            _activeEffects.Add(entity);
            return entity;
        }

        private void ReturnEffectEntity(AudioEntity entity)
        {
            entity.Reset();
            entity.GameObject.SetActive(false);
            _pool.Push(entity);
        }

        private AudioEntity CreateEntity(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(_root.transform, false);
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            return new AudioEntity(gameObject, source);
        }

        private void ApplyVolumes()
        {
            _bgm.ApplyChannelVolume(EffectiveMusicVolume);
            _voice.ApplyChannelVolume(EffectiveSfxVolume);
            foreach (var entity in _activeEffects)
                entity.ApplyChannelVolume(EffectiveSfxVolume);
        }

        private void ReleaseEffectsUsing(string location)
        {
            if (_activeEffects.Count == 0)
                return;

            var matches = new List<AudioEntity>();
            foreach (var entity in _activeEffects)
            {
                if (entity.Location == location)
                    matches.Add(entity);
            }

            foreach (var entity in matches)
            {
                entity.Stop();
                _activeEffects.Remove(entity);
                ReturnEffectEntity(entity);
            }
        }

        private void ReleaseAllEffects()
        {
            if (_activeEffects.Count == 0)
                return;

            var active = new List<AudioEntity>(_activeEffects);
            _activeEffects.Clear();
            foreach (var entity in active)
            {
                entity.Stop();
                ReturnEffectEntity(entity);
            }
        }

        private float EffectiveMusicVolume => _masterVolume * _musicVolume;
        private float EffectiveSfxVolume => _masterVolume * _sfxVolume;

        private static void ValidateLocation(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
                throw new ArgumentException("Audio location is required.", nameof(location));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(AudioService));
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(target);
            else
                UnityEngine.Object.DestroyImmediate(target);
        }

        private sealed class AudioEntity
        {
            private float _volume = 1f;

            public AudioEntity(GameObject gameObject, AudioSource source)
            {
                GameObject = gameObject;
                Source = source;
            }

            public GameObject GameObject { get; }
            public AudioSource Source { get; }
            public string Location { get; set; } = string.Empty;

            public void Play(
                AudioClip clip,
                float volume,
                bool loop,
                bool spatial,
                Vector3 position,
                float channelVolume)
            {
                _volume = volume;
                Source.clip = clip;
                Source.loop = loop;
                Source.spatialBlend = spatial ? 0.7f : 0f;
                GameObject.transform.position = spatial ? position : Vector3.zero;
                ApplyChannelVolume(channelVolume);
                Source.Play();
            }

            public void ApplyChannelVolume(float channelVolume)
            {
                Source.volume = _volume * channelVolume;
            }

            public void Stop()
            {
                Source.Stop();
                Source.clip = null;
            }

            public void Reset()
            {
                Stop();
                Source.loop = false;
                Source.spatialBlend = 0f;
                Location = string.Empty;
            }
        }
    }
}
