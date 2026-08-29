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
        UniTask PlayBgmAsync(string location, float volume = 1f, CancellationToken cancellationToken = default);
        void PauseBgm();
        void ResumeBgm();
        void StopBgm();

        UniTask<float> PlayOneShotAsync(
            string location,
            float volume = 1f,
            CancellationToken cancellationToken = default);

        UniTask<float> PlayOneShotAtAsync(
            string location,
            Vector3 position,
            float volume = 1f,
            CancellationToken cancellationToken = default);

        UniTask PlayVoiceAsync(string location, float volume = 1f, CancellationToken cancellationToken = default);
        void StopVoice();

        void SetVolume(float volume);
        void SetVolume(AudioChannel channel, float volume);
        void Release(string location);
        void ReleaseAll();
    }
}
