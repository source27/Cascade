using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Cascade.Editor;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace CascadeExample.Editor
{
    /// <summary>
    /// 打包 + 打热更窗口（自 client 的 DBFrameworkBuildWindow 迁移，功能对齐）：
    /// - 打包页：HybridCLR Generate/All → 热更 dll/元数据 → YooAsset SBP → Player；
    ///   构建产物上传 DevCDN（PUT + X-CSRF-Token），可查询 DevCDN 最新资源版本。
    /// - 热更页：Generate AOT Metadata / Build Hot DLL / Copy AOT Metadata 分步。
    /// DevCDN 服务端见 tools/devcdn（node devcdn-server.mjs）。
    /// </summary>
    public sealed class ExampleBuildWindow : EditorWindow
    {
        private const string PackageName = "CascadePak";
        private const string CodeRoot = "Assets/Code";
        private const string DevCdnBaseUrl = "http://10.1.51.151:2727";
        private const string DevCdnRootName = "Cascade";
        private const string DevCdnCsrfToken = "227e24ff-63a2-4499-b1f4-7fd5f0c7330f";
        private const string LastBuiltPackageVersionKey = "CascadeExample.LastBuiltPackageVersion";
        private const string HotDllName = "GameLogic.HotUpdate.dll";
        private const string BuildArtifactsRoot = "BuildArtifacts";

        private enum BuildTab
        {
            Package,
            HotUpdate
        }

        private BuildTab _tab;
        private string _applicationVersion = "0.1";
        private string _packageVersion = string.Empty;
        private bool _developmentBuild;
        private bool _isUploading;
        private bool _isRefreshingDevCdnVersion;
        private string _devCdnVersion = "未查询";
        private string _status = "就绪";
        private Vector2 _scroll;

        [MenuItem("CascadeExample/构建窗口", priority = 110)]
        public static void Open()
        {
            var window = GetWindow<ExampleBuildWindow>("Cascade 构建");
            window.minSize = new Vector2(600f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            _applicationVersion = PlayerSettings.bundleVersion;
            _packageVersion = PackageVersionTracker.PeekNext(_applicationVersion);
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
            DrawVersionFields();

            if (GUILayout.Button("一键打包（Generate/All → 热更文件 → YooAsset SBP → Player）", GUILayout.Height(32f)))
                RunAfterGui(BuildPackage);

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(_isUploading))
            {
                if (GUILayout.Button(_isUploading ? "上传中…" : "上传构建产物到 DevCDN", GUILayout.Height(28f)))
                    UploadResourcesToDevCdn();
            }

            DrawDevCdnVersion();

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "流程：HybridCLR Generate/All → 热更 dll 与 AOT 元数据拷入 Assets/Code（YooAsset Code 组，RawFile）→ YooAsset ScriptableBuildPipeline → 构建 Player（当前 Build Target）。\n" +
                "真机联调：tools/devcdn 启动 DevCDN → 上传产物 → Bootstrap 场景 playMode 切 Host。",
                MessageType.None);
        }

        private void DrawHotUpdateTab()
        {
            DrawVersionFields();

            if (GUILayout.Button("1. Generate AOT Metadata (All)", GUILayout.Height(28f)))
                RunAfterGui(() => PrebuildCommand.GenerateAll());

            if (GUILayout.Button("2. Build Hot DLL + Copy to Assets/Code", GUILayout.Height(28f)))
                RunAfterGui(BuildHotUpdateDll);

            if (GUILayout.Button("3. Copy AOT Metadata to Assets/Code（真机用）", GUILayout.Height(28f)))
                RunAfterGui(CopyAotMetadata);

            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Editor 内播放走 EditorSimulate（YooAsset 虚拟资源 + 热更程序集随编辑器编译加载）；打包后的真机走 YooAsset 包（Offline 内置 / Host 远端）。",
                MessageType.None);
        }

        private void DrawVersionFields()
        {
            EditorGUILayout.LabelField("应用版本");
            _applicationVersion = EditorGUILayout.TextField(_applicationVersion).Trim();
            EditorGUILayout.LabelField("资源包版本", _packageVersion);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("下一个包版本", PackageVersionTracker.PeekNext(_applicationVersion));
            }
            EditorGUILayout.Space(4f);
        }

        private void DrawDevCdnVersion()
        {
            var versionUrl = GetDevCdnDirectoryUrl(EditorUserBuildSettings.activeBuildTarget);
            if (!_isRefreshingDevCdnVersion && !_isUploading)
                RefreshDevCdnVersion(versionUrl);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("DevCDN 最新资源版本", _devCdnVersion);
            using (new EditorGUI.DisabledScope(_isRefreshingDevCdnVersion || _isUploading))
            {
                if (GUILayout.Button("刷新", GUILayout.Width(60f)))
                    RefreshDevCdnVersion(versionUrl);
            }
            EditorGUILayout.EndHorizontal();
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
            CopyGeneratedCode(target);

            var packageVersion = GetPackageVersionInput();
            BuildYooAssetPackage(target, packageVersion);

            SetStatus("构建 Player…");
            var buildPath = Path.Combine(
                BuildArtifactsRoot,
                GetPlatformFolder(target),
                $"CascadeExample_{Sanitize(_applicationVersion)}");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = buildPath,
                target = target,
                options = _developmentBuild ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Player 构建失败：{report.summary.result}（详见 Console）");

            SetStatus($"打包完成：{buildPath}，资源版本 {packageVersion}");
        }

        private void CopyGeneratedCode(BuildTarget target)
        {
            BuildHotUpdateDll();
            CopyAotMetadata();
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

            EnsureFolder(CodeRoot);
            var dest = Path.Combine(CodeRoot, HotDllName);
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

            EnsureFolder(CodeRoot);
            foreach (var name in Cascade.Launcher.AotMetadataCatalog.ResolveLocations())
            {
                var source = Path.Combine(stripDir, name);
                if (!File.Exists(source))
                    throw new FileNotFoundException($"缺少 AOT 元数据：{name}。", source);
                var dest = Path.Combine(CodeRoot, name);
                File.Copy(source, dest, overwrite: true);
                AssetDatabase.ImportAsset(dest);
            }
            SetStatus("AOT 元数据已拷入 Assets/Code。");
        }

        private void BuildYooAssetPackage(BuildTarget target, string packageVersion)
        {
            SetStatus("YooAsset ScriptableBuildPipeline…");
            var outputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot();
            Directory.CreateDirectory(outputRoot);

            var bundledRoot = Path.Combine(outputRoot, "StreamingAssets");
            Directory.CreateDirectory(bundledRoot);

            var parameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = outputRoot,
                BundledFileRoot = bundledRoot,
                BuildPipeline = EBuildPipeline.ScriptableBuildPipeline.ToString(),
                BuildBundleType = (int)EBundleType.AssetBundle,
                BuildTarget = target,
                PackageName = PackageName,
                PackageVersion = packageVersion,
                PackageNote = $"CascadeExample {_applicationVersion}",
                EnableSharePackRule = true,
                VerifyBuildingResult = true,
                FileNameStyle = EFileNameStyle.HashName,
                BundledCopyOption = EBundledCopyOption.ClearAndCopyAll,
                CompressOption = ECompressOption.Uncompressed,
                ClearBuildCacheFiles = true,
                TrackSpriteAtlasDependencies = true
            };

            var result = new ScriptableBuildPipeline().Run(parameters, true);
            if (!result.Success)
                throw new InvalidOperationException("YooAsset 构建失败：" + result.ErrorInfo);

            PackageVersionTracker.CommitBuiltVersion(packageVersion);
            _packageVersion = PackageVersionTracker.PeekNext(_applicationVersion);
            EditorPrefs.SetString(LastBuiltPackageVersionKey, packageVersion);
            SetStatus($"YooAsset 包构建完成：{result.OutputPackageDirectory}");
        }

        private async void UploadResourcesToDevCdn()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_applicationVersion))
                    throw new InvalidOperationException("应用版本不能为空。");
                var target = EditorUserBuildSettings.activeBuildTarget;
                var packageVersion = EditorPrefs.GetString(LastBuiltPackageVersionKey, string.Empty);
                if (string.IsNullOrWhiteSpace(packageVersion))
                    throw new InvalidOperationException("未找到最近构建的资源包，请先执行一键打包。");

                var source = Path.Combine(
                    BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                    target.ToString(),
                    PackageName,
                    packageVersion);
                var destinationUrl = GetDevCdnDirectoryUrl(target);

                _isUploading = true;
                await UploadDirectory(source, destinationUrl);
                _devCdnVersion = "已上传 " + packageVersion;
                SetStatus($"资源已上传到 DevCDN：{destinationUrl}");
            }
            catch (Exception exception)
            {
                SetStatus("上传 DevCDN 失败：" + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                _isUploading = false;
                EditorUtility.ClearProgressBar();
                Repaint();
            }
        }

        private async void RefreshDevCdnVersion(string directoryUrl)
        {
            if (string.IsNullOrWhiteSpace(_applicationVersion))
            {
                _devCdnVersion = "应用版本为空";
                return;
            }

            _isRefreshingDevCdnVersion = true;
            _devCdnVersion = "查询中...";
            Repaint();

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                using (var request = new HttpRequestMessage(HttpMethod.Get, $"{directoryUrl}/{PackageName}.version?t={DateTime.UtcNow.Ticks}"))
                {
                    request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
                    using (var response = await client.SendAsync(request))
                    {
                        if (response.StatusCode == global::System.Net.HttpStatusCode.NotFound)
                            _devCdnVersion = "未发布";
                        else if (!response.IsSuccessStatusCode)
                            _devCdnVersion = $"查询失败 (HTTP {(int)response.StatusCode})";
                        else
                        {
                            var version = (await response.Content.ReadAsStringAsync()).Trim();
                            _devCdnVersion = string.IsNullOrEmpty(version) ? "版本文件为空" : version;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                _devCdnVersion = "查询失败：" + exception.Message;
            }
            finally
            {
                _isRefreshingDevCdnVersion = false;
                Repaint();
            }
        }

        private static async Task UploadDirectory(string source, string destinationUrl)
        {
            if (!Directory.Exists(source))
                throw new DirectoryNotFoundException($"YooAsset 构建目录不存在：{source}");

            var files = Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(file => file.EndsWith(".version", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenBy(Path.GetFileName, StringComparer.Ordinal)
                .ToArray();
            if (files.Length == 0)
                throw new InvalidOperationException($"YooAsset 构建目录为空：{source}");

            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
            {
                for (var index = 0; index < files.Length; index++)
                {
                    var file = files[index];
                    var fileName = Path.GetFileName(file);
                    EditorUtility.DisplayProgressBar(
                        "上传资源到 DevCDN",
                        $"{index + 1}/{files.Length}  {fileName}",
                        (index + 1f) / files.Length);

                    using (var stream = File.OpenRead(file))
                    using (var fileContent = new StreamContent(stream))
                    using (var request = new HttpRequestMessage(
                               HttpMethod.Put,
                               $"{destinationUrl}/{Uri.EscapeDataString(fileName)}"))
                    {
                        request.Headers.TryAddWithoutValidation("X-CSRF-Token", DevCdnCsrfToken);
                        request.Content = fileContent;
                        using (var response = await client.SendAsync(request))
                        {
                            if (!response.IsSuccessStatusCode)
                            {
                                var responseBody = await response.Content.ReadAsStringAsync();
                                throw new InvalidOperationException(
                                    $"上传 {fileName} 失败：HTTP {(int)response.StatusCode} {response.ReasonPhrase}。{responseBody}");
                            }
                        }
                    }
                }
            }
        }

        private string GetPackageVersionInput()
        {
            var next = PackageVersionTracker.PeekNext(_applicationVersion);
            if (string.IsNullOrWhiteSpace(_packageVersion))
                _packageVersion = next;
            return _packageVersion;
        }

        private void EnsureVersion()
        {
            if (string.IsNullOrWhiteSpace(_applicationVersion))
                throw new InvalidOperationException("应用版本不能为空。");
        }

        private static string GetDevCdnDirectoryUrl(BuildTarget target)
        {
            if (string.IsNullOrWhiteSpace(EditorUserBuildSettings.activeBuildTarget.ToString()))
                throw new InvalidOperationException("Build Target 无效。");
            return $"{DevCdnBaseUrl}/{DevCdnRootName}/{GetPlatformFolder(target)}";
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
