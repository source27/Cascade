using System;
using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 本机显示 / 画质设置（LOCAL ONLY）。
    /// 永不上传 Steam Remote Storage / 云端；跨 PC 游玩时每台机器各自一份。
    /// </summary>
    [Serializable]
    public class LocalDisplaySettings
    {
        public int version = 1;

        public int resolutionWidth = 1920;
        public int resolutionHeight = 1080;

        /// <summary>0=ExclusiveFullScreen, 1=FullScreenWindow, 2=MaximizedWindow, 3=Windowed</summary>
        public int fullscreenMode = 1;

        public bool vSync = true;

        /// <summary>目标帧率；&lt;=0 表示不限制（Application.targetFrameRate = -1）。</summary>
        public int targetFrameRate = 60;

        public int qualityLevel = 2;

        public static LocalDisplaySettings CreateDefault()
        {
            var m = new LocalDisplaySettings();
            try
            {
                if (Screen.currentResolution.width > 0)
                {
                    m.resolutionWidth = Screen.currentResolution.width;
                    m.resolutionHeight = Screen.currentResolution.height;
                }
            }
            catch
            {
                // Editor / headless：保留字面默认
            }

            return m;
        }

        public LocalDisplaySettings Clone()
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

        public void MergeFrom(LocalDisplaySettings other)
        {
            if (other == null)
                return;

            if (other.version > 0)
                version = other.version;
            if (other.resolutionWidth > 0)
                resolutionWidth = other.resolutionWidth;
            if (other.resolutionHeight > 0)
                resolutionHeight = other.resolutionHeight;
            fullscreenMode = other.fullscreenMode;
            vSync = other.vSync;
            targetFrameRate = other.targetFrameRate;
            qualityLevel = other.qualityLevel;
        }
    }
}
