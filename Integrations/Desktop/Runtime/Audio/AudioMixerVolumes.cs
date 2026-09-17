using UnityEngine;
using UnityEngine.Audio;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// AudioMixer 音量辅助。Exposed 参数名可配置（默认 MasterVol / BgmVol / SfxVol，dB）。
    /// Mixer 缺失时静默；主音量亦可由 <see cref="GameSettingsApplier"/> 兜底 AudioListener。
    /// </summary>
    public sealed class AudioMixerVolumes : MonoBehaviour
    {
        [SerializeField] AudioMixer _mixer;
        [SerializeField] string _masterParam = "MasterVol";
        [SerializeField] string _bgmParam = "BgmVol";
        [SerializeField] string _sfxParam = "SfxVol";

        public void SetMaster(float linear01) => SetLinear(_masterParam, linear01);
        public void SetBgm(float linear01) => SetLinear(_bgmParam, linear01);
        public void SetSfx(float linear01) => SetLinear(_sfxParam, linear01);

        public void SetMixer(AudioMixer mixer) => _mixer = mixer;

        public void SetParamNames(string master, string bgm, string sfx)
        {
            if (!string.IsNullOrEmpty(master)) _masterParam = master;
            if (!string.IsNullOrEmpty(bgm)) _bgmParam = bgm;
            if (!string.IsNullOrEmpty(sfx)) _sfxParam = sfx;
        }

        void SetLinear(string param, float linear01)
        {
            if (_mixer == null || string.IsNullOrEmpty(param))
                return;

            float v = Mathf.Clamp01(linear01);
            float db = v <= 0.0001f ? -80f : Mathf.Log10(v) * 20f;
            _mixer.SetFloat(param, db);
        }
    }
}
