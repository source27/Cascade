#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Cascade.Service;


using Cascade.Modules.Audio;

namespace Cascade.Modules.Audio.Tests
{
    public sealed class AudioServiceTests
    {
        private float _time;
        private FakeResourceService _resources;
        private AudioService _audio;

        [SetUp]
        public void SetUp()
        {
            _time = 0f;
            _resources = new FakeResourceService();
            _resources.AddClip("hit", lengthSeconds: 1f);
            _resources.AddClip("hit_b", lengthSeconds: 1f);
            _resources.AddClip("bgm", lengthSeconds: 2f);
            _resources.AddClip("voice", lengthSeconds: 1f);
        }

        [TearDown]
        public void TearDown()
        {
            _audio?.Dispose();
            _audio = null;
            _resources?.Dispose();
            _resources = null;
        }

        [Test]
        public void PlayOneShot_UnderGlobalMax_Accepts_ThenDrops()
        {
            _audio = CreateAudio(maxConcurrent: 2, maxPerKey: 8, cooldown: 0f, poolMax: 8);

            Assert.Greater(Play("hit"), 0f);
            Assert.Greater(Play("hit_b"), 0f);
            Assert.AreEqual(0f, Play("hit"));
        }

        [Test]
        public void PlayOneShot_PerKeyMax_DropsEvenIfGlobalHasRoom()
        {
            _audio = CreateAudio(maxConcurrent: 8, maxPerKey: 1, cooldown: 0f, poolMax: 8);

            Assert.Greater(Play("hit"), 0f);
            Assert.AreEqual(0f, Play("hit"));
            Assert.Greater(Play("hit_b"), 0f);
        }

        [Test]
        public void PlayOneShot_Cooldown_DropsThenAcceptsAfterTime()
        {
            _audio = CreateAudio(maxConcurrent: 8, maxPerKey: 8, cooldown: 0.1f, poolMax: 8);

            Assert.Greater(Play("hit"), 0f);
            _audio.Release("hit");
            _time = 0.05f;
            Assert.AreEqual(0f, Play("hit"));
            _time = 0.11f;
            Assert.Greater(Play("hit"), 0f);
        }

        [Test]
        public void Configure_UpdatesCooldownBudget()
        {
            _audio = CreateAudio(maxConcurrent: 8, maxPerKey: 8, cooldown: 1f, poolMax: 8);
            Assert.Greater(Play("hit"), 0f);
            _audio.Release("hit");
            Assert.AreEqual(0f, Play("hit")); // still in 1s cooldown

            _audio.Configure(new AudioServiceOptions
            {
                MaxConcurrentOneShots = 8,
                MaxInstancesPerKey = 8,
                PerKeyCooldownSeconds = 0f,
                MaxPoolSize = 8,
                UnscaledTime = () => _time
            });

            Assert.Greater(Play("hit"), 0f);
        }


        [Test]
        public void PlayOneShot_DifferentKeys_DoNotSharePerKeyCount()
        {
            _audio = CreateAudio(maxConcurrent: 8, maxPerKey: 1, cooldown: 0f, poolMax: 8);

            Assert.Greater(Play("hit"), 0f);
            Assert.Greater(Play("hit_b"), 0f);
        }

        [Test]
        public void Release_FreesGlobalAndPerKeySlots()
        {
            _audio = CreateAudio(maxConcurrent: 1, maxPerKey: 1, cooldown: 0f, poolMax: 4);

            Assert.Greater(Play("hit"), 0f);
            Assert.AreEqual(0f, Play("hit"));
            _audio.Release("hit");
            Assert.Greater(Play("hit"), 0f);
        }

        [Test]
        public void PlayOneShot_PoolExhausted_Drops()
        {
            _audio = CreateAudio(maxConcurrent: 8, maxPerKey: 8, cooldown: 0f, poolMax: 1);

            Assert.Greater(Play("hit"), 0f);
            Assert.AreEqual(0f, Play("hit_b"));
        }

        [Test]
        public void PlayOneShot_PoolExhausted_DoesNotArmCooldown()
        {
            _audio = CreateAudio(maxConcurrent: 8, maxPerKey: 8, cooldown: 1f, poolMax: 1);

            Assert.Greater(Play("hit"), 0f);
            Assert.AreEqual(0f, Play("hit_b"));
            _audio.Release("hit");
            // Same key as first accepted play is still in cooldown; different key must not be
            // blocked by a failed acquire that never committed.
            Assert.Greater(Play("hit_b"), 0f);
        }

        [Test]
        public void Prewarm_DoesNotBreakPlay()
        {
            _audio = CreateAudio(maxConcurrent: 4, maxPerKey: 4, cooldown: 0f, poolMax: 4, prewarm: 2);
            _audio.PrewarmSfx(4);
            Assert.Greater(Play("hit"), 0f);
        }

        [Test]
        public void PlayBgm_WorksWhileSfxBudgetSaturated()
        {
            _audio = CreateAudio(maxConcurrent: 1, maxPerKey: 1, cooldown: 0f, poolMax: 4);

            Assert.Greater(Play("hit"), 0f);
            Assert.AreEqual(0f, Play("hit_b"));
            Assert.DoesNotThrow(() => PlayBgm("bgm").GetAwaiter().GetResult());
            _audio.StopBgm();
        }

        [Test]
        public void SetVolume_AppliesToActiveOneShotSource()
        {
            _audio = CreateAudio(maxConcurrent: 2, maxPerKey: 2, cooldown: 0f, poolMax: 4);
            Assert.Greater(Play("hit"), 0f);
            _audio.SetVolume(0.5f);
            _audio.SetVolume(AudioChannel.Sfx, 0.5f);

            var source = UnityEngine.Object.FindObjectsOfType<AudioSource>()
                .FirstOrDefault(s => s != null && s.clip != null && s.clip.name == "hit");
            Assert.IsNotNull(source);
            Assert.AreEqual(0.25f, source.volume, 0.0001f);
        }

        [Test]
        public void ReleaseAll_AndDispose_AreSafe()
        {
            _audio = CreateAudio(maxConcurrent: 2, maxPerKey: 2, cooldown: 0f, poolMax: 4);
            Assert.Greater(Play("hit"), 0f);
            Assert.DoesNotThrow(() => _audio.ReleaseAll());
            Assert.Greater(Play("hit"), 0f);
            Assert.DoesNotThrow(() => _audio.Dispose());
            _audio = null;
        }

        [Test]
        public void PlayOneShotAt_UsesSameBudgetAsNonSpatial()
        {
            _audio = CreateAudio(maxConcurrent: 1, maxPerKey: 1, cooldown: 0f, poolMax: 4);
            var duration = _audio.PlayOneShotAtAsync("hit", Vector3.one).GetAwaiter().GetResult();
            Assert.Greater(duration, 0f);
            Assert.AreEqual(0f, Play("hit_b"));
        }

        [Test]
        public void NaturalFinish_AfterClockAdvances_FreesSlot()
        {
            _audio = CreateAudio(maxConcurrent: 1, maxPerKey: 1, cooldown: 0f, poolMax: 4);
            Assert.AreEqual(1f, Play("hit"));
            _time = 1.01f;
            Assert.Greater(Play("hit_b"), 0f);
        }


        [Test]
        public void Dispose_AfterUnityDestroyedRoot_DoesNotThrow()
        {
            _audio = CreateAudio(maxConcurrent: 2, maxPerKey: 2, cooldown: 0f, poolMax: 4);
            var rootField = typeof(AudioService).GetField("_root", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null);
            var root = (UnityEngine.GameObject)rootField.GetValue(_audio);
            UnityEngine.Object.DestroyImmediate(root);
            Assert.DoesNotThrow(() => _audio.Dispose());
            _audio = null;
        }

        private AudioService CreateAudio(
            int maxConcurrent,
            int maxPerKey,
            float cooldown,
            int poolMax,
            int prewarm = 0)
        {
            return new AudioService(
                _resources,
                log: null,
                new AudioServiceOptions
                {
                    MaxConcurrentOneShots = maxConcurrent,
                    MaxInstancesPerKey = maxPerKey,
                    PerKeyCooldownSeconds = cooldown,
                    MaxPoolSize = poolMax,
                    PrewarmCount = prewarm,
                    UnscaledTime = () => _time
                });
        }

        private float Play(string location) =>
            _audio.PlayOneShotAsync(location).GetAwaiter().GetResult();

        private UniTask PlayBgm(string location) => _audio.PlayBgmAsync(location);

        private sealed class FakeResourceService : IResourceService, IDisposable
        {
            private readonly Dictionary<string, AudioClip> _clips =
                new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            private readonly List<AudioClip> _owned = new List<AudioClip>();

            public bool IsInitialized => true;

            public void AddClip(string location, float lengthSeconds)
            {
                var samples = Mathf.Max(1, Mathf.RoundToInt(lengthSeconds * 44100f));
                var clip = AudioClip.Create(location, samples, 1, 44100, false);
                _owned.Add(clip);
                _clips[location] = clip;
            }

            public UniTask InitializeAsync(CancellationToken cancellationToken = default) =>
                UniTask.CompletedTask;

            public UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                if (typeof(T) != typeof(AudioClip))
                    throw new InvalidOperationException($"Unexpected type {typeof(T)}");
                if (!_clips.TryGetValue(location, out var clip))
                    throw new InvalidOperationException($"Missing clip {location}");
                IAssetHandle<T> handle = (IAssetHandle<T>)(object)new FakeHandle<AudioClip>(clip);
                return UniTask.FromResult(handle);
            }

            public UniTask<ISceneHandle> LoadSceneAsync(
                string location,
                ResourceSceneLoadMode loadMode = ResourceSceneLoadMode.Single,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public void UnloadUnused()
            {
            }

            public void Dispose()
            {
                foreach (var clip in _owned)
                {
                    if (clip != null)
                        UnityEngine.Object.DestroyImmediate(clip);
                }
                _owned.Clear();
                _clips.Clear();
            }
        }

        private sealed class FakeHandle<T> : IAssetHandle<T> where T : UnityEngine.Object
        {
            public FakeHandle(T asset) => Asset = asset;
            public T Asset { get; }
            public bool IsValid => Asset != null;
            public void Release()
            {
            }

            public void Dispose() => Release();
        }
    }
}
#endif
