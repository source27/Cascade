using System;
using System.IO;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;

namespace CascadeExample.Editor
{
    /// <summary>
    /// HybridCLR hot-update workflow for the example:
    /// 1. Configure settings (GameLogic.HotUpdate = hot assembly)
    /// 2. Generate AOT metadata (HybridCLR Generate/All, needed for player builds)
    /// 3. Build the hot DLL and copy it to StreamingAssets; flips the bootstrap
    ///    playMode to Offline so the launcher Assembly.Loads the dll (real hot-update)
    /// 4. Copy stripped AOT metadata to StreamingAssets (player builds only:
    ///    requires an IL2CPP player build first; editor runs skip metadata)
    /// </summary>
    public static class ExampleHotUpdateBuild
    {
        private const string HotUpdateAsmdefPath = "Assets/Scripts/HotUpdate/GameLogic.HotUpdate.asmdef";
        private const string StreamingRoot = "Assets/StreamingAssets";
        private const string HotDllName = "GameLogic.HotUpdate.dll";
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";

        [MenuItem("CascadeExample/HotUpdate/1. Configure HybridCLR Settings")]
        public static void ConfigureSettings()
        {
            var settings = HybridCLRSettings.Instance;
            var asmdef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(HotUpdateAsmdefPath);
            if (asmdef == null)
                throw new InvalidOperationException($"Hot-update asmdef not found: {HotUpdateAsmdefPath}");

            settings.enable = true;
            settings.hotUpdateAssemblyDefinitions = new[] { asmdef };
            settings.hotUpdateAssemblies = new[] { "GameLogic.HotUpdate" };
            HybridCLRSettings.Save();
            Debug.Log("[CascadeExample] HybridCLR settings configured: GameLogic.HotUpdate is a hot-update assembly.");
        }

        [MenuItem("CascadeExample/HotUpdate/2. Generate AOT Metadata (All)")]
        public static void GenerateAll()
        {
            PrebuildCommand.GenerateAll();
        }

        [MenuItem("CascadeExample/HotUpdate/3. Build Hot DLL + Copy to StreamingAssets")]
        public static void BuildAndCopy()
        {
            ConfigureSettings();
            var target = EditorUserBuildSettings.activeBuildTarget;
            CompileDllCommand.CompileDll(target);

            var outputDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            var dll = Path.Combine(outputDir, HotDllName);
            if (!File.Exists(dll))
                throw new FileNotFoundException($"Hot-update dll not found: {dll}");

            EnsureFolder(StreamingRoot);
            var dest = Path.Combine(StreamingRoot, HotDllName);
            File.Copy(dll, dest, overwrite: true);
            AssetDatabase.ImportAsset(dest);

            // Offline (RawFile): the launcher Assembly.Loads the dll from StreamingAssets
            // via DemoResourceService — the real hot-update path, runnable in the editor.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var entry = UnityEngine.Object.FindObjectOfType<ExampleBootstrapEntry>();
            if (entry != null)
            {
                var so = new SerializedObject(entry);
                so.FindProperty("playMode").enumValueIndex = (int)Cascade.Launcher.BootstrapPlayMode.Offline;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log($"[CascadeExample] Hot-update dll built and copied to {dest}. Press Play (Offline mode loads the dll).");
        }

        [MenuItem("CascadeExample/HotUpdate/4. Copy AOT Metadata to StreamingAssets (player builds)")]
        public static void CopyAotMetadata()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var stripDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            if (!Directory.Exists(stripDir))
                throw new InvalidOperationException(
                    $"Stripped AOT assemblies not found at {stripDir}. Build the player with IL2CPP first " +
                    "(menu 2 Generate AOT Metadata, then build the player).");

            EnsureFolder(StreamingRoot);
            foreach (var dll in Directory.GetFiles(stripDir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(dll);
                File.Copy(dll, Path.Combine(StreamingRoot, name), overwrite: true);
                AssetDatabase.ImportAsset(Path.Combine(StreamingRoot, name));
            }

            Debug.Log("[CascadeExample] AOT metadata copied to StreamingAssets (player build reads it via AotMetadataCatalog).");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
