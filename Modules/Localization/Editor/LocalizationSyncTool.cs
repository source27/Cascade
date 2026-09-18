using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Cascade.Modules.Localization.Editor
{
    /// <summary>
    /// Editor orchestration for localization authoring: settings menus + Sheet download + disk write.
    /// Pure import → JSON lives in <see cref="LocalizationImportPipeline"/>.
    /// </summary>
    public static class LocalizationSyncTool
    {
        public static string DefaultOutputRoot => LocalizationImportPipeline.DefaultOutputRoot;

        public static void FocusSettings(LocalizationSyncSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            LocalizationEditorPaths.SetPreferredAssetRoot(settings.ResolvedOutputRoot);
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        /// <summary>
        /// Ask the user before creating the settings asset. Returns null if declined.
        /// </summary>
        public static LocalizationSyncSettings PromptCreateSettings()
        {
            var existing = LocalizationSyncSettings.TryLoad();
            if (existing != null)
                return existing;

            var create = EditorUtility.DisplayDialog(
                "Cascade Localization",
                $"尚未创建本地化同步设置。\n\n是否创建？\n{LocalizationSyncSettings.AssetPath}",
                "创建",
                "取消");
            if (!create)
                return null;

            var settings = LocalizationSyncSettings.CreateDefault();
            FocusSettings(settings);
            return settings;
        }

        [MenuItem("Cascade/更新多语言", priority = 110)]
        public static async void SyncFromMenu()
        {
            try
            {
                var settings = LocalizationSyncSettings.TryLoad();
                var createdNow = false;
                if (settings == null)
                {
                    settings = PromptCreateSettings();
                    if (settings == null)
                        return;
                    createdNow = true;
                }

                if (createdNow || !HasConfiguredSourceUrl(settings))
                {
                    FocusSettings(settings);
                    EditorUtility.DisplayDialog(
                        "Cascade Localization",
                        "请在 Inspector 中填写 Google Sheet URL（sources → url），然后再次执行 Cascade/更新多语言。",
                        "OK");
                    return;
                }

                var count = await SyncAsync(settings);
                EditorUtility.DisplayDialog("Cascade Localization", $"同步完成，共生成 {count} 个语言表。", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Cascade Localization", exception.Message, "OK");
            }
        }

        public static async Task<int> SyncAsync(LocalizationSyncSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var enabledSources = EnsureSourcesConfigured(settings);
            var contributions = new List<LocalizationCsvContribution>(enabledSources.Count);
            foreach (var source in enabledSources)
            {
                var csv = await LocalizationGoogleSheetAdapter.DownloadCsvAsync(source.url);
                contributions.Add(new LocalizationCsvContribution(source, csv));
            }

            var outputRoot = settings.ResolvedOutputRoot;
            var artifacts = LocalizationImportPipeline.BuildArtifacts(
                settings.defaultLocale,
                contributions,
                outputRoot);
            WriteArtifacts(artifacts, outputRoot);
            LocalizationEditorPaths.SetPreferredAssetRoot(outputRoot);
            Debug.Log(
                $"Cascade Localization: generated {CountLocaleTables(artifacts)} locale table(s) from {enabledSources.Count} source(s) → {outputRoot}");
            return CountLocaleTables(artifacts);
        }

        private static bool HasConfiguredSourceUrl(LocalizationSyncSettings settings)
        {
            return settings.sources != null &&
                   settings.sources.Any(source =>
                       source != null &&
                       source.enabled &&
                       !string.IsNullOrWhiteSpace(source.url));
        }

        private static List<LocalizationCsvSource> EnsureSourcesConfigured(LocalizationSyncSettings settings)
        {
            var enabledSources = settings.sources?.Where(source => source != null && source.enabled).ToList();
            if (enabledSources == null || enabledSources.Count == 0)
            {
                FocusSettings(settings);
                throw new InvalidOperationException(
                    "未配置启用的本地化数据源。请在 Inspector 中为 LocalizationSyncSettings 添加 sources，然后重试 Cascade/更新多语言。");
            }

            var missingUrl = enabledSources.FirstOrDefault(source => string.IsNullOrWhiteSpace(source.url));
            if (missingUrl != null)
            {
                FocusSettings(settings);
                throw new InvalidOperationException(
                    $"数据源 “{missingUrl.name}” 未填写 Google Sheet URL。请在 Inspector 填写 url 后重试 Cascade/更新多语言。");
            }

            return enabledSources;
        }

        private static void WriteArtifacts(IReadOnlyDictionary<string, string> artifacts, string outputRoot)
        {
            EnsureFolder(outputRoot);
            foreach (var output in artifacts)
                File.WriteAllText(Path.GetFullPath(output.Key), output.Value, new UTF8Encoding(false));

            var expected = new HashSet<string>(artifacts.Keys.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(Path.GetFullPath(outputRoot), "localization_*.json"))
            {
                var assetPath = NormalizePath(Path.GetRelativePath(Directory.GetCurrentDirectory(), file));
                if (!expected.Contains(assetPath))
                    AssetDatabase.DeleteAsset(assetPath);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static int CountLocaleTables(IReadOnlyDictionary<string, string> artifacts)
        {
            var count = 0;
            foreach (var path in artifacts.Keys)
            {
                var name = Path.GetFileName(path);
                if (name != null &&
                    name.StartsWith("localization_", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("localization_catalog.json", StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = NormalizePath(Path.GetDirectoryName(path));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string NormalizePath(string path) => path?.Replace('\\', '/');
    }
}
