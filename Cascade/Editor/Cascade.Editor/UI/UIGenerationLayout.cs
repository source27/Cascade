using System;
using System.IO;
using UnityEngine;

namespace Cascade.Editor
{
    /// <summary>
    /// Where UIScriptGenerator writes page/view/binding sources.
    /// Override with ProjectSettings/CascadeUIGeneration.json; otherwise auto-detect
    /// GameLogic layout when that folder exists, else Cascade starter defaults.
    /// </summary>
    internal sealed class UIGenerationLayout
    {
        public const string SettingsPath = "ProjectSettings/CascadeUIGeneration.json";

        public string PageDirectory { get; private set; }
        public string ViewDirectory { get; private set; }
        public string BindingDirectory { get; private set; }
        public string PageNamespace { get; private set; }
        public string ViewNamespace { get; private set; }
        public string BindingNamespace { get; private set; }

        private static UIGenerationLayout _cached;
        private static string _cachedStamp;

        public static UIGenerationLayout Current
        {
            get
            {
                var stamp = File.Exists(SettingsPath)
                    ? File.GetLastWriteTimeUtc(SettingsPath).Ticks.ToString()
                    : Directory.Exists("Assets/Scripts/GameLogic/UI") ? "gamelogic" : "default";
                if (_cached != null && _cachedStamp == stamp)
                    return _cached;
                _cached = Load();
                _cachedStamp = stamp;
                return _cached;
            }
        }

        public static UIGenerationLayout CascadeDefaults() => new UIGenerationLayout
        {
            PageDirectory = "Assets/Scripts/UI",
            ViewDirectory = "Assets/Scripts/UI/Views",
            BindingDirectory = "Assets/Scripts/UI/Generated",
            PageNamespace = "Cascade.UI",
            ViewNamespace = "Cascade.UI.Views",
            BindingNamespace = "Cascade.Generated",
        };

        public static UIGenerationLayout GameLogicDefaults() => new UIGenerationLayout
        {
            PageDirectory = "Assets/Scripts/GameLogic/UI",
            ViewDirectory = "Assets/Scripts/GameLogic/UI/Views",
            BindingDirectory = "Assets/Scripts/GameLogic/UI/Generated",
            PageNamespace = "GameLogic.UI",
            ViewNamespace = "GameLogic.UI.Views",
            BindingNamespace = "GameLogic.UI.Generated",
        };

        private static UIGenerationLayout Load()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsPath);
                    var data = JsonUtility.FromJson<SettingsFile>(json);
                    if (data != null && !string.IsNullOrWhiteSpace(data.pageDirectory))
                    {
                        return new UIGenerationLayout
                        {
                            PageDirectory = Normalize(data.pageDirectory),
                            ViewDirectory = Normalize(
                                string.IsNullOrWhiteSpace(data.viewDirectory)
                                    ? Path.Combine(data.pageDirectory, "Views")
                                    : data.viewDirectory),
                            BindingDirectory = Normalize(
                                string.IsNullOrWhiteSpace(data.bindingDirectory)
                                    ? Path.Combine(data.pageDirectory, "Generated")
                                    : data.bindingDirectory),
                            PageNamespace = string.IsNullOrWhiteSpace(data.pageNamespace)
                                ? "Cascade.UI"
                                : data.pageNamespace.Trim(),
                            ViewNamespace = string.IsNullOrWhiteSpace(data.viewNamespace)
                                ? "Cascade.UI.Views"
                                : data.viewNamespace.Trim(),
                            BindingNamespace = string.IsNullOrWhiteSpace(data.bindingNamespace)
                                ? "Cascade.Generated"
                                : data.bindingNamespace.Trim(),
                        };
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[Cascade] Failed to read {SettingsPath}; using defaults. {exception.Message}");
                }
            }

            return Directory.Exists("Assets/Scripts/GameLogic/UI")
                ? GameLogicDefaults()
                : CascadeDefaults();
        }

        private static string Normalize(string path) =>
            (path ?? string.Empty).Replace('\\', '/').Trim().TrimEnd('/');

        [Serializable]
        private sealed class SettingsFile
        {
            public string pageDirectory;
            public string viewDirectory;
            public string bindingDirectory;
            public string pageNamespace;
            public string viewNamespace;
            public string bindingNamespace;
        }
    }
}
