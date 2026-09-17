using UnityEngine;
using UnityEngine.Audio;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 薄门面：持有 <see cref="AudioMixerVolumes"/>，供设置 Applier 接线。
    /// 完整 BGM 交叉淡化等留给游戏或后续迭代。
    /// </summary>
    public sealed class DesktopAudioService : MonoBehaviour
    {
        [SerializeField] AudioMixer _mixer;
        [SerializeField] AudioMixerVolumes _volumes;

        public AudioMixerVolumes Volumes => _volumes;

        public void Initialize(AudioMixerVolumes volumes = null, AudioMixer mixer = null)
        {
            if (mixer != null)
                _mixer = mixer;

            _volumes = volumes != null ? volumes : _volumes;
            if (_volumes == null)
                _volumes = GetComponent<AudioMixerVolumes>();
            if (_volumes == null)
                _volumes = gameObject.AddComponent<AudioMixerVolumes>();

            if (_mixer != null)
                _volumes.SetMixer(_mixer);
        }

        public void ApplyRoamingVolumes(RoamingGameSettings roaming)
        {
            if (roaming == null)
                return;
            if (_volumes == null)
                Initialize();
            GameSettingsApplier.ApplyAudio(roaming, _volumes);
        }
    }
}
