using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Service
{
    public enum AudioChannel
    {
        Music,
        Sfx
    }

    public interface IAudioService
    {
        UniTask PreloadAsync(string location, CancellationToken cancellationToken = default);
        /// <summary>
        /// Load an AudioMixer by resource location and route BGM/SFX/Voice to groups named
        /// "BGM", "SFX", and "Voice" (exact match). Missing groups are left unrouted.
        /// </summary>
        UniTask BindMixerAsync(string location, CancellationToken cancellationToken = default);

        /// <summary>
        /// Apply one-shot budget / pool settings after construction.
        /// Non-null mixer groups replace routing; null groups leave current routing unchanged.
        /// </summary>
        void Configure(AudioServiceOptions options);


        UniTask PlayBgmAsync(string location, float volume = 1f, CancellationToken cancellationToken = default);
        void PauseBgm();
        void ResumeBgm();
        void StopBgm();

        /// <summary>
        /// Play a non-spatial one-shot. Returns clip length when accepted;
        /// returns 0 when dropped by voice budget, per-key cap/cooldown, or pool exhaustion.
        /// </summary>
        UniTask<float> PlayOneShotAsync(
            string location,
            float volume = 1f,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Play a spatial one-shot. Returns clip length when accepted;
        /// returns 0 when dropped by voice budget, per-key cap/cooldown, or pool exhaustion.
        /// </summary>
        UniTask<float> PlayOneShotAtAsync(
            string location,
            Vector3 position,
            float volume = 1f,
            CancellationToken cancellationToken = default);

        UniTask PlayVoiceAsync(string location, float volume = 1f, CancellationToken cancellationToken = default);
        void StopVoice();

        void SetVolume(float volume);
        void SetVolume(AudioChannel channel, float volume);

        /// <summary>当前主音量（0-1），供设置界面回显。</summary>
        float MasterVolume { get; }

        /// <summary>当前音乐/音效通道音量（0-1），供设置界面回显。</summary>
        float GetVolume(AudioChannel channel);
        void Release(string location);
        void ReleaseAll();
    }
}
