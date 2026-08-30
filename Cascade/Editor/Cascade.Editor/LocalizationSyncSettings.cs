using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Cascade.Editor
{
    [Serializable]
    public sealed class LocalizationCsvSource
    {
        public string name = "Main";
        public bool enabled = true;
        [Tooltip("Google Sheet edit or CSV export URL.")]
        public string url = string.Empty;
        [Tooltip("1-based row index of the header (Key / locale codes).")]
        public int headerRow = 3;
        [Tooltip("1-based row index where key rows start.")]
        public int dataStartRow = 7;
    }

    public sealed class LocalizationSyncSettings : ScriptableObject
    {
        public const string AssetPath = "Assets/Settings/LocalizationSyncSettings.asset";
        public const string DefaultSheetUrl = "";

        [Tooltip("Normalized default locale code written into localization_catalog.json.")]
        public string defaultLocale = "en";
        public List<LocalizationCsvSource> sources = new List<LocalizationCsvSource>();

        public static LocalizationSyncSettings GetOrCreate()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LocalizationSyncSettings>(AssetPath);
            if (settings != null)
                return settings;

            EnsureFolder("Assets/Settings");
            settings = CreateInstance<LocalizationSyncSettings>();
            settings.sources.Add(new LocalizationCsvSource { name = "Main", url = DefaultSheetUrl });
            AssetDatabase.CreateAsset(settings, AssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
