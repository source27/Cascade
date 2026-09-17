using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 将设置应用到 Screen / QualitySettings / Audio。
    /// Display/Quality 来自 Local；Audio 来自 Roaming（可接 <see cref="AudioMixerVolumes"/>）。
    /// </summary>
    public static class GameSettingsApplier
    {
        public static void ApplyAll(GameSettingsModel model, AudioMixerVolumes mixerVolumes = null)
        {
            if (model == null)
                return;

            ApplyDisplay(model);
            ApplyQuality(model);
            ApplyAudio(model, mixerVolumes);
        }

        public static void ApplyDisplay(GameSettingsModel model)
        {
            if (model == null)
                return;
            ApplyDisplay(model.ToLocal());
        }

        public static void ApplyDisplay(LocalDisplaySettings local)
        {
            if (local == null)
                return;

            var mode = (FullScreenMode)Mathf.Clamp(local.fullscreenMode, 0, 3);
            int w = Mathf.Max(640, local.resolutionWidth);
            int h = Mathf.Max(480, local.resolutionHeight);

            try
            {
                Screen.SetResolution(w, h, mode);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameSettingsApplier] SetResolution: {e.Message}");
            }

            try
            {
                QualitySettings.vSyncCount = local.vSync ? 1 : 0;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameSettingsApplier] vSync: {e.Message}");
            }

            try
            {
                Application.targetFrameRate = local.targetFrameRate > 0 ? local.targetFrameRate : -1;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameSettingsApplier] targetFrameRate: {e.Message}");
            }
        }

        public static void ApplyQuality(GameSettingsModel model)
        {
            if (model == null)
                return;
            ApplyQuality(model.ToLocal());
        }

        public static void ApplyQuality(LocalDisplaySettings local)
        {
            if (local == null)
                return;

            try
            {
                int qMax = QualitySettings.names != null ? QualitySettings.names.Length - 1 : 0;
                if (qMax >= 0)
                    QualitySettings.SetQualityLevel(Mathf.Clamp(local.qualityLevel, 0, qMax), true);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameSettingsApplier] qualityLevel: {e.Message}");
            }
        }

        /// <summary>
        /// 应用漫游音量。若提供 <paramref name="mixerVolumes"/> 则推 Mixer；
        /// 否则用 <see cref="AudioListener.volume"/> 兜底主音量。
        /// </summary>
        public static void ApplyAudio(GameSettingsModel model, AudioMixerVolumes mixerVolumes = null)
        {
            if (model == null)
                return;
            ApplyAudio(model.ToRoaming(), mixerVolumes);
        }

        public static void ApplyAudio(RoamingGameSettings roaming, AudioMixerVolumes mixerVolumes = null)
        {
            if (roaming == null)
                return;

            if (mixerVolumes != null)
            {
                mixerVolumes.SetMaster(roaming.masterVolume);
                mixerVolumes.SetBgm(roaming.bgmVolume);
                mixerVolumes.SetSfx(roaming.sfxVolume);
                return;
            }

            try
            {
                AudioListener.volume = Mathf.Clamp01(roaming.masterVolume);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameSettingsApplier] AudioListener: {e.Message}");
            }
        }

        [System.Obsolete("Use ApplyAudio instead.")]
        public static void ApplyAudioFallback(GameSettingsModel model) => ApplyAudio(model, null);
    }
}
