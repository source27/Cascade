using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cascade.Modules.Localization.Editor
{
    /// <summary>
    /// Editor-only disk roots for localization JSON. Runtime uses resource locations, not these paths.
    /// Projects override via EditorPrefs (tools package writes this on sync) or by placing catalog JSON under Assets.
    /// </summary>
    public static class LocalizationEditorPaths
    {
        public const string DefaultAssetRoot = "Assets/Localization";
        public const string AssetRootPrefsKey = "Cascade.Localization.EditorAssetRoot";
        public const string CatalogFileName = "localization_catalog.json";

        public static string NormalizeRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root))
                return DefaultAssetRoot;
            var normalized = root.Replace('\\', '/').Trim().TrimEnd('/');
            if (normalized.StartsWith("./", StringComparison.Ordinal))
                normalized = normalized.Substring(2);
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"Localization asset root must be under Assets/ (got '{root}').",
                    nameof(root));
            return normalized;
        }

        public static void SetPreferredAssetRoot(string root)
        {
            EditorPrefs.SetString(AssetRootPrefsKey, NormalizeRoot(root));
        }

        public static string ResolveAssetRoot()
        {
            var preferred = EditorPrefs.GetString(AssetRootPrefsKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                try
                {
                    var normalized = NormalizeRoot(preferred);
                    if (File.Exists(CatalogPath(normalized)))
                        return normalized;
                }
                catch (ArgumentException)
                {
                    // fall through to discovery
                }
            }

            if (File.Exists(CatalogPath(DefaultAssetRoot)))
                return DefaultAssetRoot;

            var guids = AssetDatabase.FindAssets("localization_catalog");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(path) ||
                    !path.EndsWith("/" + CatalogFileName, StringComparison.OrdinalIgnoreCase))
                    continue;
                var root = path.Substring(0, path.Length - CatalogFileName.Length).TrimEnd('/');
                if (root.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    return root;
            }

            return DefaultAssetRoot;
        }

        public static string CatalogPath(string assetRoot = null) =>
            $"{NormalizeRoot(assetRoot ?? ResolveAssetRoot())}/{CatalogFileName}";

        public static string TablePath(string location, string assetRoot = null)
        {
            var root = NormalizeRoot(assetRoot ?? ResolveAssetRoot());
            var fileName = location ?? string.Empty;
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                fileName += ".json";
            return $"{root}/{fileName}";
        }
    }
}
