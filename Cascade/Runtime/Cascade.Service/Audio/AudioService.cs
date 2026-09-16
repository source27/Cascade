using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

namespace Cascade.Service
{
    public sealed class AudioService : IAudioService, IDisposable
    {
        private const string LogCategory = "Audio";

        private readonly IResourceService _resources;
        private readonly ILogService _log;
        private readonly AudioServiceOptions _options;
        private Func<float> _time;

        private IAssetHandle<AudioMixer> _mixerHandle;

        private readonly Dictionary<string, IAssetHandle<AudioClip>> _clips =
            new Dictionary<string, IAssetHandle<AudioClip>>(StringComparer.Ordinal);
        private readonly Stack<AudioEntity> _pool = new Stack<AudioEntity>();
        private readonly HashSet<AudioEntity> _activeEffects = new HashSet<AudioEntity>();
        private readonly Dictionary<string, int> _activeByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> _nextAllowedByKey = new Dictionary<string, float>(StringComparer.Ordinal);
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
        private int _poolCreated;
        private bool _disposed;

        public AudioService(IResourceService resources, ILogService log = null, AudioServiceOptions options = null)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _log = log;
            _options = NormalizeOptions(options);
            _time = _options.UnscaledTime ?? (() => Time.unscaledTime);
            _root = new GameObject("AudioService");
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(_root);
            _bgm = CreateEntity("BGM", _options.MusicGroup, countTowardPool: false);
            _voice = CreateEntity("Voice", _options.VoiceGroup, countTowardPool: false);
            Prewarm(_options.PrewarmCount);
        }

        public async UniTask PreloadAsync(string location, CancellationToken cancellationToken = default)
        {
            await LoadClipAsync(location, cancellationToken);
        }
        public async UniTask BindMixerAsync(string location, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ValidateLocation(location);

            var handle = await _resources.LoadAssetAsync<AudioMixer>(location, cancellationToken);
            if (_disposed)
            {
                handle.Release();
                throw new ObjectDisposedException(nameof(AudioService));
            }

            if (handle.Asset == null)
            {
                handle.Release();
                throw new InvalidOperationException($"Audio mixer is null: {location}");
            }

            _mixerHandle?.Release();
            _mixerHandle = handle;

            var mixer = handle.Asset;
            _options.MusicGroup = FindGroup(mixer, "BGM");
            _options.SfxGroup = FindGroup(mixer, "SFX");
            _options.VoiceGroup = FindGroup(mixer, "Voice");
            ApplyMixerRouting();
        }



        public void Configure(AudioServiceOptions options)
        {
            ThrowIfDisposed();
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var normalized = NormalizeOptions(CopyOptions(options));

            _options.MaxConcurrentOneShots = normalized.MaxConcurrentOneShots;
            _options.MaxInstancesPerKey = normalized.MaxInstancesPerKey;
            _options.PerKeyCooldownSeconds = normalized.PerKeyCooldownSeconds;
            _options.MaxPoolSize = normalized.MaxPoolSize;
            _options.PrewarmCount = normalized.PrewarmCount;

            if (options.UnscaledTime != null)
            {
                _options.UnscaledTime = options.UnscaledTime;
                _time = options.UnscaledTime;
            }

            var routingChanged = false;
            if (options.MusicGroup != null)
            {
                _options.MusicGroup = options.MusicGroup;
                routingChanged = true;
            }
            if (options.SfxGroup != null)
            {
                _options.SfxGroup = options.SfxGroup;
                routingChanged = true;
            }
            if (options.VoiceGroup != null)
            {
                _options.VoiceGroup = options.VoiceGroup;
                routingChanged = true;
            }

            if (routingChanged)
                ApplyMixerRouting();

            Prewarm(normalized.PrewarmCount);
        }

        /// <summary>Create up to <paramref name="count"/> idle SFX sources without exceeding the pool hard max.</summary>
        public void PrewarmSfx(int count) => Prewarm(count);


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
            if (_disposed || _bgm?.Source == null)
                return;
            if (_bgm.Source.isPlaying)
                _bgm.Source.Pause();
        }

        public void ResumeBgm()
        {
            if (_disposed || _bgm?.Source == null)
                return;
            if (_bgm.Source.clip != null && !_bgm.Source.isPlaying)
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

        public float MasterVolume => _masterVolume;

        public float GetVolume(AudioChannel channel) =>
            channel == AudioChannel.Music ? _musicVolume : _sfxVolume;

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
            _nextAllowedByKey.Clear();
            if (_mixerHandle != null)
            {
                _mixerHandle.Release();
                _mixerHandle = null;
            }
            _options.MusicGroup = null;
            _options.SfxGroup = null;
            _options.VoiceGroup = null;
            // Do not ApplyMixerRouting here: on Play Mode exit sources may already be destroyed.
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

            SweepFinishedOneShots();

            if (!CanAcceptOneShot(location))
                return 0f;

            if (!TryGetEffectEntity(out var entity))
                return 0f;

            CommitOneShot(location);
            entity.Location = location;
            var duration = GetPlaybackDuration(clip);
            entity.BeginOneShot(_time(), duration);
            entity.Play(clip, Mathf.Clamp01(volume), false, spatial, position, EffectiveSfxVolume);
            RecycleWhenFinishedAsync(entity, _lifetime.Token).Forget();
            return duration;
        }

        private void SweepFinishedOneShots()
        {
            if (_activeEffects.Count == 0)
                return;

            List<AudioEntity> finished = null;
            foreach (var entity in _activeEffects)
            {
                if (!entity.IsFinished(_time))
                    continue;
                finished ??= new List<AudioEntity>();
                finished.Add(entity);
            }

            if (finished == null)
                return;

            foreach (var entity in finished)
            {
                if (!_activeEffects.Remove(entity))
                    continue;
                entity.Stop();
                CancelAcceptedOneShot(entity.Location);
                ReturnEffectEntity(entity);
            }
        }


        private bool CanAcceptOneShot(string location)
        {
            if (_activeEffects.Count >= _options.MaxConcurrentOneShots)
                return false;

            _activeByKey.TryGetValue(location, out var keyCount);
            if (keyCount >= _options.MaxInstancesPerKey)
                return false;

            if (_options.PerKeyCooldownSeconds > 0f
                && _nextAllowedByKey.TryGetValue(location, out var nextAllowed)
                && _time() < nextAllowed)
                return false;

            return true;
        }

        private void CommitOneShot(string location)
        {
            _activeByKey.TryGetValue(location, out var keyCount);
            _activeByKey[location] = keyCount + 1;
            if (_options.PerKeyCooldownSeconds > 0f)
                _nextAllowedByKey[location] = _time() + _options.PerKeyCooldownSeconds;
        }

        private void CancelAcceptedOneShot(string location)
        {
            if (!_activeByKey.TryGetValue(location, out var keyCount))
                return;

            if (keyCount <= 1)
                _activeByKey.Remove(location);
            else
                _activeByKey[location] = keyCount - 1;
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
                await UniTask.WaitUntil(() => entity.IsFinished(_time), cancellationToken: cancellationToken);
                if (_disposed)
                    return;
                if (_activeEffects.Remove(entity))
                {
                    CancelAcceptedOneShot(entity.Location);
                    ReturnEffectEntity(entity);
                }
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

        private bool TryGetEffectEntity(out AudioEntity entity)
        {
            if (_pool.Count > 0)
            {
                entity = _pool.Pop();
                entity.Source.outputAudioMixerGroup = _options.SfxGroup;
                entity.GameObject.SetActive(true);
                _activeEffects.Add(entity);
                return true;
            }

            if (_poolCreated >= _options.MaxPoolSize)
            {
                entity = null;
                return false;
            }

            entity = CreateEntity("SFX", _options.SfxGroup, countTowardPool: true);
            entity.GameObject.SetActive(true);
            _activeEffects.Add(entity);
            return true;
        }

        private void ReturnEffectEntity(AudioEntity entity)
        {
            entity.Reset();
            entity.GameObject.SetActive(false);
            _pool.Push(entity);
        }

        private void Prewarm(int count)
        {
            if (count <= 0 || _disposed)
                return;

            var target = Math.Min(count, _options.MaxPoolSize);
            while (_poolCreated < target)
            {
                var entity = CreateEntity("SFX", _options.SfxGroup, countTowardPool: true);
                entity.GameObject.SetActive(false);
                _pool.Push(entity);
            }
        }

        private AudioEntity CreateEntity(string name, AudioMixerGroup group, bool countTowardPool)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(_root.transform, false);
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            if (group != null)
                source.outputAudioMixerGroup = group;
            if (countTowardPool)
                _poolCreated++;
            return new AudioEntity(gameObject, source);
        }

        private void ApplyMixerRouting()
        {
            if (_bgm != null && IsAlive(_bgm.Source))
                _bgm.Source.outputAudioMixerGroup = _options.MusicGroup;
            if (_voice != null && IsAlive(_voice.Source))
                _voice.Source.outputAudioMixerGroup = _options.VoiceGroup;

            foreach (var entity in _activeEffects)
            {
                if (IsAlive(entity.Source))
                    entity.Source.outputAudioMixerGroup = _options.SfxGroup;
            }

            foreach (var entity in _pool)
            {
                if (IsAlive(entity.Source))
                    entity.Source.outputAudioMixerGroup = _options.SfxGroup;
            }
        }

        private static bool IsAlive(UnityEngine.Object obj) => obj != null;


        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            if (mixer == null || string.IsNullOrEmpty(name))
                return null;

            var groups = mixer.FindMatchingGroups(name);
            for (var i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && groups[i].name == name)
                    return groups[i];
            }

            return groups.Length > 0 ? groups[0] : null;
        }


        private void ApplyVolumes()
        {
            _bgm?.ApplyChannelVolume(EffectiveMusicVolume);
            _voice?.ApplyChannelVolume(EffectiveSfxVolume);
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
                if (_activeEffects.Remove(entity))
                {
                    CancelAcceptedOneShot(location);
                    ReturnEffectEntity(entity);
                }
            }
        }

        private void ReleaseAllEffects()
        {
            if (_activeEffects.Count == 0)
            {
                _activeByKey.Clear();
                return;
            }

            var active = new List<AudioEntity>(_activeEffects);
            _activeEffects.Clear();
            _activeByKey.Clear();
            foreach (var entity in active)
            {
                entity.Stop();
                ReturnEffectEntity(entity);
            }
        }

        private float EffectiveMusicVolume => _masterVolume * _musicVolume;
        private float EffectiveSfxVolume => _masterVolume * _sfxVolume;

        private static float GetPlaybackDuration(AudioClip clip) => clip != null ? clip.length : 0f;


        private static AudioServiceOptions CopyOptions(AudioServiceOptions source)
        {
            return new AudioServiceOptions
            {
                MaxConcurrentOneShots = source.MaxConcurrentOneShots,
                MaxInstancesPerKey = source.MaxInstancesPerKey,
                PerKeyCooldownSeconds = source.PerKeyCooldownSeconds,
                MaxPoolSize = source.MaxPoolSize,
                PrewarmCount = source.PrewarmCount,
                MusicGroup = source.MusicGroup,
                SfxGroup = source.SfxGroup,
                VoiceGroup = source.VoiceGroup,
                UnscaledTime = source.UnscaledTime
            };
        }

        private static AudioServiceOptions NormalizeOptions(AudioServiceOptions options)
        {
            options ??= new AudioServiceOptions();
            if (options.MaxConcurrentOneShots < 1)
                options.MaxConcurrentOneShots = 1;
            if (options.MaxInstancesPerKey < 1)
                options.MaxInstancesPerKey = 1;
            if (options.PerKeyCooldownSeconds < 0f)
                options.PerKeyCooldownSeconds = 0f;
            if (options.MaxPoolSize < 1)
                options.MaxPoolSize = 1;
            if (options.PrewarmCount < 0)
                options.PrewarmCount = 0;
            return options;
        }


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
            private float _endTime;
            private bool _stopped = true;

            public AudioEntity(GameObject gameObject, AudioSource source)
            {
                GameObject = gameObject;
                Source = source;
            }

            public GameObject GameObject { get; }
            public AudioSource Source { get; }
            public string Location { get; set; } = string.Empty;

            public void BeginOneShot(float now, float duration)
            {
                _stopped = false;
                _endTime = now + Mathf.Max(duration, 0.0001f);
            }

            public bool IsFinished(Func<float> time)
            {
                if (_stopped)
                    return true;
                if (time() >= _endTime)
                    return true;
                if (Application.isPlaying && Source != null && !Source.isPlaying)
                    return true;
                return false;
            }

            public void Play(
                AudioClip clip,
                float volume,
                bool loop,
                bool spatial,
                Vector3 position,
                float channelVolume)
            {
                if (Source == null || GameObject == null)
                    return;

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
                if (Source == null)
                    return;
                Source.volume = _volume * channelVolume;
            }

            public void Stop()
            {
                _stopped = true;
                if (Source == null)
                    return;
                Source.Stop();
                Source.clip = null;
            }

            public void Reset()
            {
                Stop();
                if (Source != null)
                {
                    Source.loop = false;
                    Source.spatialBlend = 0f;
                }
                Location = string.Empty;
                _endTime = 0f;
            }

        }
    }
}
