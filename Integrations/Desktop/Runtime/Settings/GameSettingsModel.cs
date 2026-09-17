using System;
using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 运行时合并视图：Local（显示/画质）+ Roaming（音量/键鼠/语言）。
    /// Apply 时拆回 Local / Roaming；持久化由 <see cref="GameSettingsStore"/> 写两份文件。
    /// </summary>
    [Serializable]
    public class GameSettingsModel
    {
        public int version = 1;

        // --- LocalDisplaySettings ---
        public int resolutionWidth = 1920;
        public int resolutionHeight = 1080;
        /// <summary>0=ExclusiveFullScreen, 1=FullScreenWindow, 2=MaximizedWindow, 3=Windowed</summary>
        public int fullscreenMode = 1;
        public bool vSync = true;
        public int targetFrameRate = 60;
        public int qualityLevel = 2;

        // --- RoamingGameSettings ---
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 1f;
        public float mouseSensitivity = 1f;
        public float gamepadDeadzone = 0.15f;
        public string language = "zh-CN";

        public static GameSettingsModel CreateDefault()
        {
            var m = new GameSettingsModel();
            m.ApplyLocal(LocalDisplaySettings.CreateDefault());
            m.ApplyRoaming(RoamingGameSettings.CreateDefault());
            return m;
        }

        public GameSettingsModel Clone()
        {
            return new GameSettingsModel
            {
                version = version,
                resolutionWidth = resolutionWidth,
                resolutionHeight = resolutionHeight,
                fullscreenMode = fullscreenMode,
                vSync = vSync,
                targetFrameRate = targetFrameRate,
                qualityLevel = qualityLevel,
                masterVolume = masterVolume,
                bgmVolume = bgmVolume,
                sfxVolume = sfxVolume,
                mouseSensitivity = mouseSensitivity,
                gamepadDeadzone = gamepadDeadzone,
                language = language
            };
        }

        public LocalDisplaySettings ToLocal()
        {
            return new LocalDisplaySettings
            {
                version = version,
                resolutionWidth = resolutionWidth,
                resolutionHeight = resolutionHeight,
                fullscreenMode = fullscreenMode,
                vSync = vSync,
                targetFrameRate = targetFrameRate,
                qualityLevel = qualityLevel
            };
        }

        public RoamingGameSettings ToRoaming()
        {
            return new RoamingGameSettings
            {
                version = version,
                masterVolume = masterVolume,
                bgmVolume = bgmVolume,
                sfxVolume = sfxVolume,
                mouseSensitivity = mouseSensitivity,
                gamepadDeadzone = gamepadDeadzone,
                language = language
            };
        }

        public void ApplyLocal(LocalDisplaySettings local)
        {
            if (local == null)
                return;
            if (local.version > 0)
                version = Math.Max(version, local.version);
            resolutionWidth = local.resolutionWidth;
            resolutionHeight = local.resolutionHeight;
            fullscreenMode = local.fullscreenMode;
            vSync = local.vSync;
            targetFrameRate = local.targetFrameRate;
            qualityLevel = local.qualityLevel;
        }

        public void ApplyRoaming(RoamingGameSettings roaming)
        {
            if (roaming == null)
                return;
            if (roaming.version > 0)
                version = Math.Max(version, roaming.version);
            masterVolume = roaming.masterVolume;
            bgmVolume = roaming.bgmVolume;
            sfxVolume = roaming.sfxVolume;
            mouseSensitivity = roaming.mouseSensitivity;
            gamepadDeadzone = roaming.gamepadDeadzone;
            if (!string.IsNullOrEmpty(roaming.language))
                language = roaming.language;
        }

        public void MergeFrom(GameSettingsModel other)
        {
            if (other == null)
                return;
            ApplyLocal(other.ToLocal());
            ApplyRoaming(other.ToRoaming());
        }
    }
}
