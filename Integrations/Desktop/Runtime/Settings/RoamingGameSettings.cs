using System;
using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 可漫游游戏偏好（CLOUD-ELIGIBLE）。
    /// 可经 Steam Remote Storage / ISaveService 镜像同步；不含分辨率等本机显示字段。
    /// </summary>
    [Serializable]
    public class RoamingGameSettings
    {
        public int version = 1;

        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        public float mouseSensitivity = 1f;
        public float gamepadDeadzone = 0.15f;
        public string language = "zh-CN";

        public static RoamingGameSettings CreateDefault() => new RoamingGameSettings();

        public RoamingGameSettings Clone()
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

        public void MergeFrom(RoamingGameSettings other)
        {
            if (other == null)
                return;

            if (other.version > 0)
                version = other.version;
            masterVolume = other.masterVolume;
            bgmVolume = other.bgmVolume;
            sfxVolume = other.sfxVolume;
            mouseSensitivity = other.mouseSensitivity;
            gamepadDeadzone = other.gamepadDeadzone;
            if (!string.IsNullOrEmpty(other.language))
                language = other.language;
        }
    }
}
