using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Cascade.Editor;
using Cascade.Mobile;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace Cascade.Mobile.Editor
{
    /// <summary>
    /// 打包 + 打热更窗口（与 client 的 DBFrameworkBuildWindow 一致）：
    /// - 打包页：环境(DEV/BETA/GOLD) → 应用/资源版本 → 资源包模式(Full 内嵌/Lite 只留 Bundles)
    ///   → HybridCLR Generate/All → 热更 dll/元数据 → YooAsset SBP → Android APK；
    ///   上传 DevCDN（PUT + X-CSRF-Token，.version 最后传）+ DevCDN 版本查询。
    /// - 热更页：一键出热更资源包（当前 Build Target，不构建 Player）+ 上传 DevCDN。
    /// DevCDN 服务端：tools/devcdn（node devcdn-server.mjs）。
    /// </summary>
    public sealed class MobileBuildWindow : EditorWindow
    {
        private enum BuildTab
        {
            Package,
            HotUpdate
        }

        private enum BuildEnvironment
        {
            DEV,
            BETA,
            GOLD
        }

        private enum ResourcePackageMode
        {
            Full = 0,
            Lite = 1
        }

        private const string PackageName = "CascadePak";
        private const string CodeRoot = "Assets/Code";
        private const string StreamingAssetPackRoot = "Assets/StreamingAssets/assetpack";
        private const string DevCdnBaseUrl = "http://127.0.0.1:2727";
        private const string DevCdnRootName = "Cascade";
        private const string DevCdnCsrfToken = "227e24ff-63a2-4499-b1f4-7fd5f0c7330f";
        private const string LastBuiltPackageVersionKey = "Cascade.Mobile.LastBuiltPackageVersion";
        private const string ApkOutputDirectory = "BuildArtifacts/Android";

        private BuildTab _tab;
        private BuildEnvironment _environment = BuildEnvironment.DEV;
        private string _applicationVersion = "0.1";
        private string _packageVersion;
        private ResourcePackageMode _resourcePackageMode = ResourcePackageMode.Full;
        private bool _developmentBuild;
        private bool _isUploading;
        private bool _isRefreshingDevCdnVersion;
        private string _devCdnVersion = "未查询";
        private string _requestedDevCdnVersionUrl;
        private string _lastBuiltPackageVersion;
        private string _status = "就绪";
        private Vector2 _scroll;
        private GUIStyle _primaryActionButtonStyle;
        private GUIStyle _secondaryActionButtonStyle;

        private bool EmbedPackage => _resourcePackageMode == ResourcePackageMode.Full;

        [MenuItem("Mobile/构建窗口", priority = 110)]
        public static void Open()
        {
            var window = GetWindow<MobileBuildWindow>("Cascade 构建");
            window.minSize = new Vector2(640f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            _applicationVersion = string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)
                ? "0.1"
                : PlayerSettings.bundleVersion;
            if (string.IsNullOrWhiteSpace(_packageVersion))
                _packageVersion = CreateDefaultPackageVersion();
            _lastBuiltPackageVersion = EditorPrefs.GetString(LastBuiltPackageVersionKey, string.Empty);
        }

        private void OnGUI()
        {
            EnsureStyles();
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
            EditorGUILayout.LabelField("APK 打包", EditorStyles.boldLabel);
            _environment = (BuildEnvironment)EditorGUILayout.EnumPopup("环境", _environment);
            DrawApplicationAndPackageVersionFields();
            DrawBuildDirectoryField(GetApkBuildDirectory(), OpenApkBuildDirectory);
            _resourcePackageMode = (ResourcePackageMode)EditorGUILayout.EnumPopup("资源包模式", _resourcePackageMode);
            _developmentBuild = EditorGUILayout.ToggleLeft("Development Build", _developmentBuild);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("APK 输出路径", GetApkOutputPath());

            DrawDevCdnVersion();

            EditorGUILayout.Space(8f);
            if (EmbedPackage)
            {
                EditorGUILayout.HelpBox(
                    "Full（默认）：YooAsset 构建结果写入 StreamingAssets/assetpack 并打入 APK。正式发布请使用此模式。\n" +
                    $"资源版本：应用版本_序号（如 0.0.1_1），记录于 {PackageVersionTracker.RelativePath}。",
                    MessageType.None);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Lite：不内嵌资源，产物只保留在 Bundles。仅供内部测试，正式发布请改回 Full。\n" +
                    $"资源版本：应用版本_序号（如 0.0.1_1），记录于 {PackageVersionTracker.RelativePath}。",
                    MessageType.Warning);
            }

            DrawActionButtons("构建 APK", BuildPackageAndApk);
        }

        private void DrawHotUpdateTab()
        {
            EditorGUILayout.LabelField("热更包", EditorStyles.boldLabel);
            _environment = (BuildEnvironment)EditorGUILayout.EnumPopup("环境", _environment);
            DrawApplicationAndPackageVersionFields();
            DrawBuildDirectoryField(GetHotUpdateBuildDirectory(), OpenHotUpdateBuildDirectory);
            DrawDevCdnVersion();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "流程：设置环境 → HybridCLR Generate/All → 复制 GameLogic/AOT 为 *.dll.bytes → YooAsset ScriptableBuildPipeline。\n" +
                "DLL 必须落盘为 .dll.bytes（TextAsset），裸 .dll 会被 NormalIgnoreRule 丢掉，导致 Location is invalid。\n" +
                $"资源版本格式：应用版本_序号（如 0.0.1_1）。序号记录在 {PackageVersionTracker.RelativePath}。",
                MessageType.None);

            DrawActionButtons("构建热更包", BuildHotUpdatePackage);
        }

        private void DrawApplicationAndPackageVersionFields()
        {
            EditorGUI.BeginChangeCheck();
            var applicationVersion = EditorGUILayout.TextField("应用版本", _applicationVersion);
            if (EditorGUI.EndChangeCheck())
            {
                _applicationVersion = applicationVersion;
                _packageVersion = CreateDefaultPackageVersion();
            }

            EditorGUILayout.BeginHorizontal();
            _packageVersion = EditorGUILayout.TextField("资源包版本", _packageVersion);
            if (GUILayout.Button("刷新序号", GUILayout.Width(72f)))
                _packageVersion = CreateDefaultPackageVersion();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawBuildDirectoryField(string directory, Action open)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField("输出目录", directory);
            if (GUILayout.Button("打开", GUILayout.Width(60f)))
                open();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDevCdnVersion()
        {
            var versionUrl = GetDevCdnDirectoryUrl(EditorUserBuildSettings.activeBuildTarget, _applicationVersion);
            if (!_isRefreshingDevCdnVersion && _requestedDevCdnVersionUrl != versionUrl)
                RefreshDevCdnVersion(versionUrl);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("DevCDN 最新资源版本", _devCdnVersion);
            using (new EditorGUI.DisabledScope(_isRefreshingDevCdnVersion || _isUploading))
            {
                if (GUILayout.Button("刷新", GUILayout.Width(60f)))
                {
                    _requestedDevCdnVersionUrl = null;
                    RefreshDevCdnVersion(versionUrl);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawActionButtons(string buildLabel, Action buildAction)
        {
            EditorGUILayout.Space(10f);
            var buildRequested = false;
            var uploadRequested = false;
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || _isUploading))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                var previousBackgroundColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.58f, 0.76f, 1f);
                if (GUILayout.Button(buildLabel, _primaryActionButtonStyle, GUILayout.Width(132f), GUILayout.Height(32f)))
                    buildRequested = true;

                GUI.backgroundColor = new Color(0.68f, 0.84f, 0.7f);
                if (GUILayout.Button(_isUploading ? "上传中…" : "上传资源到 DevCDN", _secondaryActionButtonStyle, GUILayout.Width(164f), GUILayout.Height(32f)))
                    uploadRequested = true;
                GUI.backgroundColor = previousBackgroundColor;

                EditorGUILayout.EndHorizontal();
            }

            if (buildRequested)
                RunAfterGui(buildAction);
            else if (uploadRequested)
                RunAfterGui(UploadResourcesToDevCdn);
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(_status, EditorStyles.boldLabel);
        }

        private void EnsureStyles()
        {
            if (_primaryActionButtonStyle != null)
                return;

            _primaryActionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _secondaryActionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void RunAfterGui(Action action)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                    return;

                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    SetStatus("失败：" + exception.Message);
                    Debug.LogException(exception);
                }
            };
        }

        private void BuildPackageAndApk()
        {
            try
            {
                var packageVersion = GetPackageVersionInput();
                var embedPackage = EmbedPackage;
                PrepareBuild(BuildTarget.Android, embedPackage);
                BuildHotUpdateCode(BuildTarget.Android);
                BuildYooAssetPackage(BuildTarget.Android, embedPackage, packageVersion);
                var apkPath = BuildApk();
                SetStatus($"APK 构建完成（{_resourcePackageMode}）：{apkPath}，资源版本：{packageVersion}");
            }
            catch (Exception exception)
            {
                SetStatus("APK 构建失败：" + exception.Message);
                Debug.LogException(exception);
                throw;
            }
        }

        private void BuildHotUpdatePackage()
        {
            try
            {
                var packageVersion = GetPackageVersionInput();
                var target = EditorUserBuildSettings.activeBuildTarget;
                PrepareBuild(target, false);
                BuildHotUpdateCode(target);
                BuildYooAssetPackage(target, false, packageVersion);
                SetStatus($"热更包构建完成，资源版本：{packageVersion}");
            }
            catch (Exception exception)
            {
                SetStatus("热更包构建失败：" + exception.Message);
                Debug.LogException(exception);
                throw;
            }
        }

        private void PrepareBuild(BuildTarget target, bool embedPackage)
        {
            if (target != EditorUserBuildSettings.activeBuildTarget)
                throw new InvalidOperationException($"请先将 Unity 当前 Build Target 切换为 {target}。");

            if (string.IsNullOrWhiteSpace(_applicationVersion))
                throw new InvalidOperationException("应用版本不能为空。");
            PlayerSettings.bundleVersion = _applicationVersion.Trim();
            SetEnvironmentDefine(target, _environment.ToString(), embedPackage);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void BuildHotUpdateCode(BuildTarget target)
        {
            SetStatus("HybridCLR Generate/All…");
            PrebuildCommand.GenerateAll();
            CopyGeneratedCode(target);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private void CopyGeneratedCode(BuildTarget target)
        {
            EnsureFolder(CodeRoot);
            DeleteGeneratedCodeFiles(CodeRoot);

            var hotUpdateDirectory = SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target);
            var hotUpdateDll = Path.Combine(hotUpdateDirectory, "GameLogic.HotUpdate.dll");
            if (!File.Exists(hotUpdateDll))
                throw new FileNotFoundException("HybridCLR 热更 DLL 不存在。", hotUpdateDll);

            // YooAsset SBP + NormalIgnoreRule 不会收集 PluginImporter/.dll（常为 DefaultAsset）。
            // 落盘为 *.dll.bytes → TextAsset；AddressByFileName 去掉末尾扩展名后仍是 *.dll。
            CopyFileAsAsset(hotUpdateDll, ToCodeAssetPath(BootstrapConfiguration.DefaultHotUpdateDllLocation));

            var metadataDirectory = SettingsUtil.GetAssembliesPostIl2CppStripDir(target);
            foreach (var location in Cascade.Launcher.AotMetadataCatalog.ResolveLocations())
            {
                var source = Path.Combine(metadataDirectory, location);
                if (!File.Exists(source))
                    throw new FileNotFoundException($"缺少 AOT 元数据：{location}。请先 HybridCLR Generate/All 或完整 Player 构建。", source);
                CopyFileAsAsset(source, ToCodeAssetPath(location));
            }
        }

        private static string ToCodeAssetPath(string dllFileName) =>
            Path.Combine(CodeRoot, dllFileName.EndsWith(".bytes", StringComparison.OrdinalIgnoreCase)
                ? dllFileName
                : dllFileName + ".bytes");

        private void BuildYooAssetPackage(BuildTarget target, bool embedPackage, string packageVersion)
        {
            SetStatus("YooAsset ScriptableBuildPipeline…");
            var outputRoot = BundleBuilderHelper.GetDefaultBuildOutputRoot();
            Directory.CreateDirectory(outputRoot);

            if (!embedPackage)
                ClearDirectory(ProjectPath(StreamingAssetPackRoot));

            var bundledRoot = embedPackage
                ? BundleBuilderHelper.GetStreamingAssetsRoot()
                : Path.Combine(outputRoot, "StreamingAssets");
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
                PackageNote = $"Cascade.Mobile {_environment} {_applicationVersion}",
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

            VerifyCodeAssetsPackaged(result.OutputPackageDirectory);

            _lastBuiltPackageVersion = packageVersion;
            EditorPrefs.SetString(LastBuiltPackageVersionKey, packageVersion);
            PackageVersionTracker.CommitBuiltVersion(packageVersion);
            _packageVersion = PackageVersionTracker.PeekNext(_applicationVersion);
            Debug.Log($"YooAsset package built: {result.OutputPackageDirectory}");
        }

        private string BuildApk()
        {
            SetStatus("Unity 构建 APK…");
            var outputPath = ProjectPath(GetApkOutputPath());
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("Build Settings 没有启用场景。");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = _developmentBuild ? BuildOptions.Development : BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException($"Unity APK 构建失败：{report.summary.result}");
            return outputPath;
        }

        private async void UploadResourcesToDevCdn()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_applicationVersion))
                    throw new InvalidOperationException("应用版本不能为空。");
                var target = EditorUserBuildSettings.activeBuildTarget;
                var packageVersion = ResolveLastBuiltPackageVersion(target);
                if (string.IsNullOrWhiteSpace(packageVersion))
                    throw new InvalidOperationException("未找到最近构建的资源包，请先执行构建。");

                var source = Path.Combine(GetYooAssetPackageRoot(target), packageVersion);
                var destinationUrl = GetDevCdnDirectoryUrl(target, _applicationVersion);

                _isUploading = true;
                await UploadDirectory(source, destinationUrl);
                _requestedDevCdnVersionUrl = null;
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
                _requestedDevCdnVersionUrl = directoryUrl;
                _devCdnVersion = "应用版本为空";
                return;
            }

            _requestedDevCdnVersionUrl = directoryUrl;
            _isRefreshingDevCdnVersion = true;
            _devCdnVersion = "查询中...";
            Repaint();

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                using (var request = new HttpRequestMessage(
                           HttpMethod.Get,
                           $"{directoryUrl}/{PackageName}.version?t={DateTime.UtcNow.Ticks}"))
                {
                    request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
                    using (var response = await client.SendAsync(request))
                    {
                        if (GetDevCdnDirectoryUrl(EditorUserBuildSettings.activeBuildTarget, _applicationVersion) != directoryUrl)
                            return;

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

        private static void SetEnvironmentDefine(BuildTarget target, string environment, bool embedPackage)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            var values = PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(value => value != "DEV" && value != "BETA" && value != "GOLD" && value != "DB_EMBED_PACKAGE")
                .ToList();
            values.Add(environment);
            if (embedPackage)
                values.Add("DB_EMBED_PACKAGE");
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", values));
        }

        private string CreateDefaultPackageVersion() => PackageVersionTracker.PeekNext(_applicationVersion);

        private string GetPackageVersionInput()
        {
            if (string.IsNullOrWhiteSpace(_packageVersion))
                throw new InvalidOperationException("资源包版本不能为空。");
            return _packageVersion.Trim();
        }

        private string ResolveLastBuiltPackageVersion(BuildTarget target)
        {
            var packageRoot = GetYooAssetPackageRoot(target);

            if (!string.IsNullOrWhiteSpace(_lastBuiltPackageVersion))
            {
                var remembered = Path.Combine(packageRoot, _lastBuiltPackageVersion);
                if (Directory.Exists(remembered))
                    return _lastBuiltPackageVersion;
            }

            var prefsVersion = EditorPrefs.GetString(LastBuiltPackageVersionKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(prefsVersion))
            {
                var remembered = Path.Combine(packageRoot, prefsVersion);
                if (Directory.Exists(remembered))
                {
                    _lastBuiltPackageVersion = prefsVersion;
                    return prefsVersion;
                }
            }

            if (!Directory.Exists(packageRoot))
                return null;

            return Directory.GetDirectories(packageRoot)
                .Where(path =>
                {
                    var name = Path.GetFileName(path);
                    return !string.Equals(name, "Simulate", StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(name, "OutputCache", StringComparison.OrdinalIgnoreCase);
                })
                .OrderByDescending(Directory.GetLastWriteTimeUtc)
                .Select(Path.GetFileName)
                .FirstOrDefault();
        }

        private string GetApkBuildDirectory() => NormalizePath(ProjectPath(ApkOutputDirectory));

        private string GetHotUpdateBuildDirectory()
        {
            // 与 BuildYooAssetPackage / UploadResourcesToDevCdn 一致：Bundles/{BuildTarget}/CascadePak
            return NormalizePath(GetYooAssetPackageRoot(EditorUserBuildSettings.activeBuildTarget));
        }

        private static string GetYooAssetPackageRoot(BuildTarget target) =>
            Path.Combine(
                BundleBuilderHelper.GetDefaultBuildOutputRoot(),
                target.ToString(),
                PackageName);

        private string GetApkOutputPath()
        {
            var productName = SanitizeFileNamePart(PlayerSettings.productName, "Application");
            var version = SanitizeFileNamePart(_applicationVersion, "0.0.0");
            var environment = _environment.ToString().ToLowerInvariant();
            environment = char.ToUpperInvariant(environment[0]) + environment.Substring(1);
            var resourceModeSuffix = EmbedPackage ? "_Full" : "_Lite";
            return NormalizePath(Path.Combine(
                ApkOutputDirectory,
                $"{productName}_{version}_{environment}{resourceModeSuffix}.apk"));
        }

        private void OpenApkBuildDirectory() => OpenDirectory(ProjectPath(ApkOutputDirectory));

        private void OpenHotUpdateBuildDirectory() => OpenDirectory(GetYooAssetPackageRoot(EditorUserBuildSettings.activeBuildTarget));

        private static void OpenDirectory(string path)
        {
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        private static string GetDevCdnDirectoryUrl(BuildTarget target, string applicationVersion)
        {
            if (string.IsNullOrWhiteSpace(applicationVersion))
                throw new InvalidOperationException("应用版本不能为空。");
            return $"{DevCdnBaseUrl}/{DevCdnRootName}/{GetPlatformFolder(target)}/{Uri.EscapeDataString(applicationVersion.Trim())}";
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

        private static string SanitizeFileNamePart(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            var sanitized = string.Concat(value.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'));
            return string.IsNullOrEmpty(sanitized) ? fallback : sanitized;
        }

        private static string NormalizePath(string path) => path.Replace('\\', '/');

        private static string ProjectPath(string path) => Path.Combine(Directory.GetCurrentDirectory(), path);

        private static void CopyFileAsAsset(string source, string assetPath)
        {
            var destination = ProjectPath(assetPath);
            var directory = Path.GetDirectoryName(destination);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.Copy(source, destination, true);
        }

        private static void DeleteGeneratedCodeFiles(string directory)
        {
            var fullPath = ProjectPath(directory);
            if (!Directory.Exists(fullPath))
                return;

            // 连同 .meta 一起清，避免残留 PluginImporter 的 *.dll.meta 干扰 *.dll.bytes 导入。
            foreach (var file in Directory.GetFiles(fullPath))
                File.Delete(file);
        }

        private static void VerifyCodeAssetsPackaged(string outputPackageDirectory)
        {
            var reportPath = Directory.GetFiles(outputPackageDirectory, "*.report", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(reportPath))
                throw new InvalidOperationException($"YooAsset 报告不存在，无法校验 Code 资源：{outputPackageDirectory}");

            var reportJson = File.ReadAllText(reportPath);
            var required = new List<string> { BootstrapConfiguration.DefaultHotUpdateDllLocation };
            required.AddRange(Cascade.Launcher.AotMetadataCatalog.ResolveLocations());

            var missing = required
                .Where(location => !string.IsNullOrEmpty(location))
                .Distinct(StringComparer.Ordinal)
                .Where(location => reportJson.IndexOf("\"" + location + "\"", StringComparison.Ordinal) < 0)
                .ToArray();

            if (missing.Length == 0)
                return;

            throw new InvalidOperationException(
                "YooAsset 包未包含代码资源地址：" + string.Join(", ", missing) +
                "。请确认 Assets/Code 下为 *.dll.bytes（TextAsset），且 Code 分组使用 AddressByFileName + PackRawFile。");
        }

        private static void ClearDirectory(string directory)
        {
            if (!Directory.Exists(directory))
                return;
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    File.Delete(file);
            }
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

        private void SetStatus(string message)
        {
            _status = message;
            Repaint();
        }
    }
}
