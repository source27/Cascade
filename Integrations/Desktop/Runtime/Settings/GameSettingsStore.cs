using System;
using System.IO;
using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 双文件持久化：
    /// <list type="bullet">
    /// <item><c>cascade-desktop/settings.local.json</c> — 本机显示/画质（永不云同步）</item>
    /// <item><c>cascade-desktop/settings.roaming.json</c> — 可漫游偏好（可云同步）</item>
    /// </list>
    /// 与 Cascade <c>ISaveService</c> 独立；prefs 镜像仅允许 roaming JSON。
    /// </summary>
    public static class GameSettingsStore
    {
        public const string RelativeDir = "cascade-desktop";
        public const string LocalFileName = "settings.local.json";
        public const string RoamingFileName = "settings.roaming.json";

        /// <summary>兼容旧单文件名（仅迁移读取，不再写入）。</summary>
        public const string LegacyCombinedFileName = "settings.json";

        public static string LocalFilePath =>
            Path.Combine(Application.persistentDataPath, RelativeDir, LocalFileName);

        public static string RoamingFilePath =>
            Path.Combine(Application.persistentDataPath, RelativeDir, RoamingFileName);

        public static string LegacyCombinedFilePath =>
            Path.Combine(Application.persistentDataPath, RelativeDir, LegacyCombinedFileName);

        public static LocalDisplaySettings LoadLocal()
        {
            try
            {
                if (File.Exists(LocalFilePath))
                {
                    var json = File.ReadAllText(LocalFilePath);
                    var loaded = JsonUtility.FromJson<LocalDisplaySettings>(json);
                    if (loaded != null)
                    {
                        var merged = LocalDisplaySettings.CreateDefault();
                        merged.MergeFrom(loaded);
                        return merged;
                    }
                }

                // 迁移：旧 cascade-desktop/settings.json 或 cascade-steam/settings.json
                if (TryLoadLegacyCombined(out var legacy))
                {
                    var local = legacy.ToLocal();
                    SaveLocal(local);
                    return local;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettingsStore] LoadLocal failed: {e.Message}");
            }

            return LocalDisplaySettings.CreateDefault();
        }

        public static RoamingGameSettings LoadRoaming()
        {
            try
            {
                if (File.Exists(RoamingFilePath))
                {
                    var json = File.ReadAllText(RoamingFilePath);
                    var loaded = JsonUtility.FromJson<RoamingGameSettings>(json);
                    if (loaded != null)
                    {
                        var merged = RoamingGameSettings.CreateDefault();
                        merged.MergeFrom(loaded);
                        return merged;
                    }
                }

                if (TryLoadLegacyCombined(out var legacy))
                {
                    var roaming = legacy.ToRoaming();
                    SaveRoaming(roaming);
                    return roaming;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettingsStore] LoadRoaming failed: {e.Message}");
            }

            return RoamingGameSettings.CreateDefault();
        }

        /// <summary>加载 local + roaming 并合并为运行时模型。</summary>
        public static GameSettingsModel Load()
        {
            var model = GameSettingsModel.CreateDefault();
            model.ApplyLocal(LoadLocal());
            model.ApplyRoaming(LoadRoaming());
            return model;
        }

        public static void SaveLocal(LocalDisplaySettings model)
        {
            if (model == null)
                return;
            WriteJson(LocalFilePath, JsonUtility.ToJson(model, true));
        }

        public static void SaveRoaming(RoamingGameSettings model)
        {
            if (model == null)
                return;
            WriteJson(RoamingFilePath, JsonUtility.ToJson(model, true));
        }

        /// <summary>分别写入 local + roaming 两份文件（不写合并单文件）。</summary>
        public static void Save(GameSettingsModel model)
        {
            if (model == null)
                return;
            SaveLocal(model.ToLocal());
            SaveRoaming(model.ToRoaming());
        }

        /// <summary>
        /// 仅把 <b>roaming</b> JSON 写入 ISaveService 键（云镜像安全）。
        /// 切勿把 local 显示设置塞进此镜像。
        /// </summary>
        public static void SaveRoamingToPrefs(Cascade.Service.ISaveService prefs, string key, RoamingGameSettings model)
        {
            if (prefs == null || model == null || string.IsNullOrEmpty(key))
                return;
            prefs.SetString(key, JsonUtility.ToJson(model));
            prefs.Flush();
        }

        public static RoamingGameSettings LoadRoamingFromPrefs(Cascade.Service.ISaveService prefs, string key)
        {
            if (prefs == null || string.IsNullOrEmpty(key))
                return RoamingGameSettings.CreateDefault();

            var json = prefs.GetString(key, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
                return RoamingGameSettings.CreateDefault();

            try
            {
                var loaded = JsonUtility.FromJson<RoamingGameSettings>(json);
                if (loaded == null)
                    return RoamingGameSettings.CreateDefault();
                var merged = RoamingGameSettings.CreateDefault();
                merged.MergeFrom(loaded);
                return merged;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettingsStore] LoadRoamingFromPrefs: {e.Message}");
                return RoamingGameSettings.CreateDefault();
            }
        }

        static bool TryLoadLegacyCombined(out GameSettingsModel model)
        {
            model = null;
            try
            {
                string[] candidates =
                {
                    LegacyCombinedFilePath,
                    Path.Combine(Application.persistentDataPath, "cascade-steam", "settings.json")
                };
                foreach (var path in candidates)
                {
                    if (!File.Exists(path))
                        continue;
                    var json = File.ReadAllText(path);
                    if (string.IsNullOrWhiteSpace(json))
                        continue;
                    var loaded = JsonUtility.FromJson<GameSettingsModel>(json);
                    if (loaded == null)
                        continue;
                    model = GameSettingsModel.CreateDefault();
                    model.MergeFrom(loaded);
                    return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettingsStore] legacy migrate: {e.Message}");
            }

            return false;
        }

        static void WriteJson(string path, string json)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[GameSettingsStore] Write failed ({path}): {e.Message}");
            }
        }
    }
}
