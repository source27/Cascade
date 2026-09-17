namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 设置门面：Load / Save / Current / ResetToDefaults / Apply。
    /// Load 合并 local + roaming；Save 写两份文件；prefs/云镜像<strong>仅 roaming</strong>。
    /// </summary>
    public sealed class GameSettingsService
    {
        /// <summary>云镜像键：仅存 roaming JSON。</summary>
        public const string DefaultRoamingPrefsKey = "cascade.desktop.gameSettings.roaming";

        public GameSettingsModel Current { get; private set; }

        AudioMixerVolumes _mixerVolumes;
        Cascade.Service.ISaveService _prefsMirror;
        string _prefsKey = DefaultRoamingPrefsKey;
        bool _mirrorToPrefs;

        public GameSettingsService()
        {
            Current = GameSettingsModel.CreateDefault();
        }

        public void BindAudioMixerVolumes(AudioMixerVolumes volumes) => _mixerVolumes = volumes;

        /// <summary>可选：Save 时仅把 roaming JSON 写入 ISaveService（可被 Steam Remote Storage 装饰）。</summary>
        public void EnablePrefsMirror(Cascade.Service.ISaveService prefs, string key = DefaultRoamingPrefsKey)
        {
            _prefsMirror = prefs;
            _prefsKey = string.IsNullOrEmpty(key) ? DefaultRoamingPrefsKey : key;
            _mirrorToPrefs = prefs != null;
        }

        public void DisablePrefsMirror()
        {
            _mirrorToPrefs = false;
            _prefsMirror = null;
        }

        public GameSettingsModel Load()
        {
            Current = GameSettingsStore.Load() ?? GameSettingsModel.CreateDefault();

            // 若启用镜像且本地 roaming 文件缺失，可从 prefs 补齐（不覆盖已有本地 roaming）
            if (_mirrorToPrefs && _prefsMirror != null && !System.IO.File.Exists(GameSettingsStore.RoamingFilePath))
            {
                var fromCloud = GameSettingsStore.LoadRoamingFromPrefs(_prefsMirror, _prefsKey);
                Current.ApplyRoaming(fromCloud);
            }

            return Current;
        }

        public void Save()
        {
            if (Current == null)
                Current = GameSettingsModel.CreateDefault();

            GameSettingsStore.Save(Current);

            // 云镜像：只上传 roaming
            if (_mirrorToPrefs && _prefsMirror != null)
                GameSettingsStore.SaveRoamingToPrefs(_prefsMirror, _prefsKey, Current.ToRoaming());
        }

        public void ResetToDefaults()
        {
            Current = GameSettingsModel.CreateDefault();
        }

        public void ResetLocalToDefaults()
        {
            if (Current == null)
                Current = GameSettingsModel.CreateDefault();
            Current.ApplyLocal(LocalDisplaySettings.CreateDefault());
        }

        public void ResetRoamingToDefaults()
        {
            if (Current == null)
                Current = GameSettingsModel.CreateDefault();
            Current.ApplyRoaming(RoamingGameSettings.CreateDefault());
        }

        /// <summary>应用显示 + 画质 + 音频。改完 UI 后调用；需要落盘再 <see cref="Save"/>。</summary>
        public void Apply()
        {
            GameSettingsApplier.ApplyAll(Current, _mixerVolumes);
        }

        public void ApplyDisplay()
        {
            GameSettingsApplier.ApplyDisplay(Current);
            GameSettingsApplier.ApplyQuality(Current);
        }

        public void ApplyAudio()
        {
            GameSettingsApplier.ApplyAudio(Current, _mixerVolumes);
        }

        /// <summary>Load → Apply，启动时一键调用。</summary>
        public void LoadAndApply()
        {
            Load();
            Apply();
        }
    }
}
