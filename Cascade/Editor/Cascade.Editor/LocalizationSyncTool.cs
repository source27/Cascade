using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Cascade.Editor
{
    /// <summary>
    /// Editor orchestration for localization authoring: settings menus + Sheet download + disk write.
    /// Pure import → JSON lives in <see cref="LocalizationImportPipeline"/>.
    /// </summary>
    public static class LocalizationSyncTool
    {
        public const string OutputRoot = LocalizationImportPipeline.OutputRoot;

        /// <summary>
        /// Ensure settings asset exists, select it in the Project window for Inspector editing.
        /// </summary>
        public static LocalizationSyncSettings ConfigureProject()
        {
            var settings = LocalizationSyncSettings.GetOrCreate();
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
            return settings;
        }

        [MenuItem("Cascade/多语言设置", priority = 109)]
        public static void OpenSettingsFromMenu()
        {
            ConfigureProject();
        }

        [MenuItem("Cascade/更新多语言", priority = 110)]
        public static async void SyncFromMenu()
        {
            try
            {
                var count = await SyncAsync(LocalizationSyncSettings.GetOrCreate());
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

            var artifacts = LocalizationImportPipeline.BuildArtifacts(settings.defaultLocale, contributions);
            WriteArtifacts(artifacts);
            Debug.Log($"Cascade Localization: generated {CountLocaleTables(artifacts)} locale table(s) from {enabledSources.Count} source(s).");
            return CountLocaleTables(artifacts);
        }

        private static List<LocalizationCsvSource> EnsureSourcesConfigured(LocalizationSyncSettings settings)
        {
            var enabledSources = settings.sources?.Where(source => source != null && source.enabled).ToList();
            if (enabledSources == null || enabledSources.Count == 0)
            {
                ConfigureProject();
                throw new InvalidOperationException(
                    "未配置启用的本地化数据源。请在 Inspector 中为 LocalizationSyncSettings 添加 sources，然后重试 Cascade/更新多语言。");
            }

            var missingUrl = enabledSources.FirstOrDefault(source => string.IsNullOrWhiteSpace(source.url));
            if (missingUrl != null)
            {
                ConfigureProject();
                throw new InvalidOperationException(
                    $"数据源 “{missingUrl.name}” 未填写 Google Sheet URL。请在 Inspector 填写 url 后重试 Cascade/更新多语言。");
            }

            return enabledSources;
        }

        private static void WriteArtifacts(IReadOnlyDictionary<string, string> artifacts)
        {
            EnsureFolder(OutputRoot);
            foreach (var output in artifacts)
                File.WriteAllText(Path.GetFullPath(output.Key), output.Value, new UTF8Encoding(false));

            var expected = new HashSet<string>(artifacts.Keys.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(Path.GetFullPath(OutputRoot), "localization_*.json"))
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
