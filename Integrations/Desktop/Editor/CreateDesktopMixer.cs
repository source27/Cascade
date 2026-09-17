#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Cascade.Integrations.Desktop.Editor
{
    /// <summary>
    /// 菜单创建带 Master/BGM/SFX 组与 Exposed 参数 MasterVol / BgmVol / SfxVol 的 AudioMixer。
    /// 与 Runtime <c>AudioMixerVolumes</c> 默认参数名对齐。
    /// </summary>
    public static class CreateDesktopMixer
    {
        const string DefaultFolder = "Assets/CascadeDesktop";
        const string DefaultAsset = "DesktopMasterMixer.mixer";

        [MenuItem("Cascade/Desktop/Create Desktop Master Mixer")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/CascadeDesktop"))
            {
                if (!AssetDatabase.IsValidFolder("Assets"))
                {
                    Debug.LogError("[CreateDesktopMixer] No Assets folder.");
                    return;
                }
                AssetDatabase.CreateFolder("Assets", "CascadeDesktop");
            }

            var path = Path.Combine(DefaultFolder, DefaultAsset).Replace('\\', '/');
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            if (existing != null)
            {
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log($"[CreateDesktopMixer] Already exists: {path}. Expose MasterVol/BgmVol/SfxVol on group volumes if missing.");
                return;
            }

            // Unity 无公开 API 完整构建 mixer 图；创建空资源后提示用户在 Inspector 暴露参数。
            // 优先：若 Samples 包内 mixer 可复制则复制。
            var sampleGuids = AssetDatabase.FindAssets("DesktopMasterMixer t:AudioMixer");
            if (sampleGuids != null && sampleGuids.Length > 0)
            {
                var samplePath = AssetDatabase.GUIDToAssetPath(sampleGuids[0]);
                if (AssetDatabase.CopyAsset(samplePath, path))
                {
                    AssetDatabase.SaveAssets();
                    var copied = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
                    Selection.activeObject = copied;
                    Debug.Log($"[CreateDesktopMixer] Copied sample mixer → {path}");
                    return;
                }
            }

            // Fallback：用 AudioMixerController 内部类型（Editor-only）尽力创建
            var controllerType = System.Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor.CoreModule")
                ?? System.Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
            if (controllerType != null)
            {
                var mixer = ScriptableObject.CreateInstance(controllerType);
                AssetDatabase.CreateAsset(mixer, path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = mixer;
                Debug.LogWarning(
                    $"[CreateDesktopMixer] Created empty mixer at {path}. " +
                    "Please add child groups BGM / SFX and expose volume parameters named MasterVol, BgmVol, SfxVol (dB).");
                return;
            }

            Debug.LogError("[CreateDesktopMixer] Could not create AudioMixer. Create one manually with exposed MasterVol / BgmVol / SfxVol.");
        }
    }
}
#endif
