using System;
using System.IO;
using System.Linq;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CascadeExample.Editor
{
    /// <summary>
    /// 打包 + 打热更编辑器窗口（自 client 的 DBFrameworkBuildWindow 迁移，去掉 YooAsset/CDN/APK 专有部分）：
    /// - 打包：HybridCLR Generate/All → 构建热更 dll 与 AOT 元数据到 StreamingAssets → 构建 Player（当前 Build Target）
    /// - 热更：Generate AOT Metadata / Build Hot DLL / Copy AOT Metadata 分步执行
    /// 版本号经 PlayerSettings.bundleVersion 与 PackageVersionTracker 跟踪。
    /// </summary>
    public sealed class ExampleBuildWindow : EditorWindow
    {
        private const string StreamingRoot = "Assets/StreamingAssets";
        private const string HotDllName = "GameLogic.HotUpdate.dll";
        private const string BuildArtifactsRoot = "BuildArtifacts";
        private const string LastBuiltTargetKey = "CascadeExample.LastBuiltTarget";

        private enum BuildTab
        {
            Package,
            HotUpdate
        }

        private BuildTab _tab;
        private string _applicationVersion = "0.1";
        private bool _developmentBuild;
        private string _status = "就绪";
        private Vector2 _scroll;

        [MenuItem("CascadeExample/构建窗口", priority = 110)]
        public static void Open()
        {
            var window = GetWindow<ExampleBuildWindow>("Cascade 构建");
            window.minSize = new Vector2(560f, 420f);
            window.Show();
        }

        private void OnEnable()
        {
            _applicationVersion = PlayerSettings.bundleVersion;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            _tab = (BuildTab)GUILayout.Toolbar((int)_tab, new[] { "打包", "热更" });
            EditorGUILayout.Space(8f);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_tab == BuildTab.Package)
                DrawPackageTab();
            else
                DrawHotUpdateTab();

            EditorGUILayout.EndScrollView();
            DrawStatusBar();
        }

        private void DrawPackageTab()
        {
            DrawApplicationVersionField();
            EditorGUILayout.Space(8f);

            if (GUILayout.Button("一键打包（Generate/All → 热更文件 → Player）", GUILayout.Height(32f)))
                RunAfterGui(BuildPackage);

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "流程：HybridCLR Generate/All → 热更 dll 与 AOT 元数据拷入 StreamingAssets → 以当前 Build Target 构建 Player 到 BuildArtifacts/。\n" +
                "真机需先在 HybridCLR 安装器中完成安装（ThirdParty/HybridCLR/Installer）。",
                MessageType.None);
        }

        private void DrawHotUpdateTab()
        {
            DrawApplicationVersionField();
            EditorGUILayout.Space(8f);

            if (GUILayout.Button("1. Generate AOT Metadata (All)", GUILayout.Height(28f)))
                RunAfterGui(() => PrebuildCommand.GenerateAll());

            if (GUILayout.Button("2. Build Hot DLL + Copy to StreamingAssets", GUILayout.Height(28f)))
                RunAfterGui(BuildHotUpdateDll);

            if (GUILayout.Button("3. Copy AOT Metadata to StreamingAssets（真机用）", GUILayout.Height(28f)))
                RunAfterGui(CopyAotMetadata);

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Editor 内播放走 EditorSimulate（热更程序集随编辑器编译加载）；打包后的真机/Player 走 RawFile：从 StreamingAssets 读 dll 与 AOT 元数据。",
                MessageType.None);
        }

        private void DrawApplicationVersionField()
        {
            EditorGUILayout.LabelField("应用版本");
            _applicationVersion = EditorGUILayout.TextField(_applicationVersion).Trim();
            if (GUILayout.Button("写入 PlayerSettings.bundleVersion", GUILayout.Width(220f)))
            {
                PlayerSettings.bundleVersion = _applicationVersion;
                AssetDatabase.SaveAssets();
                SetStatus($"bundleVersion = {_applicationVersion}");
            }
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(_status, EditorStyles.boldLabel);
            Repaint();
        }

        private void RunAfterGui(Action action)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                SetStatus("失败：" + exception.Message);
                Debug.LogException(exception);
                throw;
            }
        }

        private void BuildPackage()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            EnsureVersion();
            PlayerSettings.bundleVersion = _applicationVersion;
            AssetDatabase.SaveAssets();

            SetStatus("HybridCLR Generate/All…");
            PrebuildCommand.GenerateAll();

            BuildHotUpdateDll();
            CopyAotMetadata();

            SetStatus("构建 Player…");
            var buildPath = Path.Combine(
                BuildArtifactsRoot,
                GetPlatformFolder(target),
                $"CascadeExample_{Sanitize(_applicationVersion)}");
            var report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings.scenes
                        .Where(scene => scene.enabled)
                        .Select(scene => scene.path)
                        .ToArray(),
                    locationPathName = buildPath,
                    target = target,
                    options = _developmentBuild
                        ? BuildOptions.Development
                        : BuildOptions.None
                });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"Player 构建失败：{report.summary.result}（详见 Console）");

            SetStatus($"打包完成：{buildPath}，应用版本 {_applicationVersion}");
        }

        private void BuildHotUpdateDll()
        {
            SetStatus("编译热更 dll…");
            var target = EditorUserBuildSettings.activeBuildTarget;
            CompileDllCommand.CompileDll(target);

            var outputDir = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            var dll = Path.Combine(outputDir, HotDllName);
            if (!File.Exists(dll))
                throw new FileNotFoundException($"HybridCLR 热更 DLL 不存在：{dll}");

            EnsureFolder(StreamingRoot);
            var dest = Path.Combine(StreamingRoot, HotDllName);
            File.Copy(dll, dest, overwrite: true);
            AssetDatabase.ImportAsset(dest);
            SetStatus($"热更 dll 已拷入 {dest}");
        }

        private void CopyAotMetadata()
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var stripDir = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            if (!Directory.Exists(stripDir))
                throw new InvalidOperationException(
                    $"剥离后的 AOT 程序集不存在：{stripDir}。需先用 IL2CPP 构建过 Player（Generate/All 之后）。");

            EnsureFolder(StreamingRoot);
            foreach (var name in Cascade.Launcher.AotMetadataCatalog.ResolveLocations())
            {
                var source = Path.Combine(stripDir, name);
                if (!File.Exists(source))
                    throw new FileNotFoundException($"缺少 AOT 元数据：{name}。", source);
                var dest = Path.Combine(StreamingRoot, name);
                File.Copy(source, dest, overwrite: true);
                AssetDatabase.ImportAsset(dest);
            }
            SetStatus("AOT 元数据已拷入 StreamingAssets。");
        }

        private void EnsureVersion()
        {
            if (string.IsNullOrWhiteSpace(_applicationVersion))
                throw new InvalidOperationException("应用版本不能为空。");
        }

        private static string GetPlatformFolder(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.Android: return "Android";
                case BuildTarget.iOS: return "IPhone";
                case BuildTarget.WebGL: return "WebGL";
                default: return "PC";
            }
        }

        private static string Sanitize(string value) =>
            string.IsNullOrWhiteSpace(value) ? "0.0" : string.Concat(value.Where(char.IsLetterOrDigit));

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private void SetStatus(string message)
        {
            _status = message;
            Repaint();
        }
    }
}
