using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cascade.Core;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Editor
{
    public static class UIScriptGenerator
    {
        private const string MenuPath = "Assets/Generate UI Page";
        private const string ViewMenuPath = "Assets/Generate UI View";

        /// <summary>
        /// Optional project override at ProjectSettings/CascadeUIGeneration.json.
        /// Absent → if Assets/Scripts/GameLogic/UI exists use GameLogic layout, else Cascade defaults.
        /// </summary>
        private static UIGenerationLayout Layout => UIGenerationLayout.Current;

        private static readonly BindingRule[] Rules =
        {
            new BindingRule("_scRect", "ScrollRect", typeof(ScrollRect)),
            new BindingRule("_slid", "Slider", typeof(Slider)),
            new BindingRule("_sBar", "Scrollbar", typeof(Scrollbar)),
            new BindingRule("_rImg", "RawImage", typeof(RawImage)),
            new BindingRule("_canvas", "Canvas", typeof(Canvas)),
            new BindingRule("_anim", "Animator", typeof(Animator)),
            new BindingRule("_tmp", "TMP", typeof(TMP_Text)),
            new BindingRule("_txt", "Txt", typeof(Text)),
            new BindingRule("_img", "Img", typeof(Image)),
            new BindingRule("_btn", "Btn", typeof(Button)),
            new BindingRule("_tog", "Tog", typeof(Toggle)),
            new BindingRule("_iF", "IF", typeof(InputField)),
            new BindingRule("_dd", "DD", typeof(Dropdown)),
            new BindingRule("_rect", "Rect", typeof(RectTransform)),
            new BindingRule("_go", "Go", typeof(GameObject))
        };

        [MenuItem(MenuPath, true)]
        private static bool ValidateGenerate()
        {
            return Selection.activeObject != null &&
                   Selection.activeObject is GameObject &&
                   AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem(MenuPath)]
        private static void GenerateFromSelection()
        {
            var prefab = Selection.activeObject as GameObject;
            Generate(AssetDatabase.GetAssetPath(prefab));
        }

        [MenuItem(ViewMenuPath, true)]
        private static bool ValidateGenerateView()
        {
            return Selection.activeObject != null &&
                   Selection.activeObject is GameObject &&
                   AssetDatabase.GetAssetPath(Selection.activeObject).EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        [MenuItem(ViewMenuPath)]
        private static void GenerateViewFromSelection()
        {
            var prefab = Selection.activeObject as GameObject;
            GenerateView(AssetDatabase.GetAssetPath(prefab));
        }

        public static bool TryGetPrefabPath(UIBindingHost host, out string prefabPath, out string error)
        {
            prefabPath = null;
            error = null;
            if (host == null)
            {
                error = "UIBindingHost is null.";
                return false;
            }

            var go = host.gameObject;
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(go);
            if (stage != null && !string.IsNullOrEmpty(stage.assetPath))
            {
                prefabPath = stage.assetPath;
            }
            else
            {
                prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
                if (string.IsNullOrEmpty(prefabPath))
                    prefabPath = AssetDatabase.GetAssetPath(go);
            }

            if (string.IsNullOrEmpty(prefabPath) ||
                !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                error = "UIBindingHost must be on a prefab asset or prefab instance.";
                return false;
            }

            return true;
        }

        public static void UpdateBindingsFromHost(UIBindingHost host)
        {
            if (!TryGetPrefabPath(host, out var prefabPath, out var error))
                throw new InvalidOperationException(error);
            if (IsViewPrefabPath(prefabPath))
                GenerateView(prefabPath, generateSource: false);
            else
                GeneratePage(prefabPath, generateSource: false);
        }

        public static void Generate(string prefabPath)
        {
            GeneratePage(prefabPath, generateSource: true);
        }

        private static void GeneratePage(string prefabPath, bool generateSource)
        {
            if (string.IsNullOrEmpty(prefabPath) ||
                !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Invalid prefab path: {prefabPath}");

            var pageName = Path.GetFileNameWithoutExtension(prefabPath);
            var pageDirectory = Layout.PageDirectory;
            var bindingDirectory = Layout.BindingDirectory;
            Directory.CreateDirectory(bindingDirectory);

            var entries = new List<BindingEntry>();
            var errors = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var succeeded = false;
            try
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);

                var host = root.GetComponent<UIBindingHost>() ?? root.AddComponent<UIBindingHost>();
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform == root.transform)
                        continue;
                    if (transform.GetComponent<UIBindingHost>() != null)
                    {
                        if (!TryResolveNestedHost(transform, out var viewEntry, out var viewError))
                        {
                            if (!string.IsNullOrEmpty(viewError))
                                errors.Add(viewError);
                        }
                        else if (entries.Any(item => item.Name == viewEntry.Name))
                        {
                            errors.Add($"Duplicate UI binding name: {viewEntry.Name}");
                        }
                        else
                        {
                            entries.Add(viewEntry);
                        }

                        continue;
                    }

                    if (HasBindingHostAncestor(transform, root.transform))
                        continue;
                    if (!TryResolve(transform, out var entry, out var error))
                    {
                        if (!string.IsNullOrEmpty(error))
                            errors.Add(error);
                        continue;
                    }

                    if (entries.Any(item => item.Name == entry.Name))
                        errors.Add($"Duplicate UI binding name: {entry.Name}");
                    else
                        entries.Add(entry);
                }

                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join("\n", errors));

                var serialized = new SerializedObject(host);
                var objects = serialized.FindProperty("_objects");
                objects.arraySize = entries.Count;
                for (var i = 0; i < entries.Count; i++)
                    objects.GetArrayElementAtIndex(i).objectReferenceValue = entries[i].Value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(host);
                EditorUtility.SetDirty(root);
                succeeded = true;
            }
            finally
            {
                if (succeeded)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    AssetDatabase.SaveAssets();
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (generateSource)
            {
                var pagePath = Path.Combine(pageDirectory, pageName + ".cs").Replace('\\', '/');
                if (!File.Exists(pagePath))
                    File.WriteAllText(pagePath, CreatePageSource(pageName), new UTF8Encoding(false));
            }

            var bindingPath = Path.Combine(bindingDirectory, pageName + "Bindings.g.cs").Replace('\\', '/');
            File.WriteAllText(bindingPath, CreateBindingSource(pageName, entries), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log(generateSource
                ? $"Generated UI page script and bindings: {pageName}"
                : $"Updated UI page bindings: {pageName}");
        }

        public static void GenerateView(string prefabPath)
        {
            GenerateView(prefabPath, generateSource: true);
        }

        private static void GenerateView(string prefabPath, bool generateSource)
        {
            if (string.IsNullOrEmpty(prefabPath) ||
                !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Invalid view prefab path: {prefabPath}");

            var viewName = Path.GetFileNameWithoutExtension(prefabPath);
            var viewDirectory = Layout.ViewDirectory;
            var bindingDirectory = Layout.BindingDirectory;
            Directory.CreateDirectory(viewDirectory);
            Directory.CreateDirectory(bindingDirectory);

            var entries = new List<BindingEntry>();
            var errors = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var succeeded = false;
            try
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.RemoveMonoBehavioursWithMissingScript(transform.gameObject);

                var host = root.GetComponent<UIBindingHost>();
                if (host == null)
                    host = root.AddComponent<UIBindingHost>();
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (transform == root.transform)
                        continue;
                    if (transform.GetComponent<UIBindingHost>() != null)
                    {
                        errors.Add($"Nested UIBindingHost is not supported in view '{viewName}': {transform.name}");
                        continue;
                    }

                    if (!TryResolve(transform, out var entry, out var error))
                    {
                        if (!string.IsNullOrEmpty(error))
                            errors.Add(error);
                        continue;
                    }

                    if (entries.Any(item => item.Name == entry.Name))
                        errors.Add($"Duplicate View binding name: {entry.Name}");
                    else
                        entries.Add(entry);
                }

                if (errors.Count > 0)
                    throw new InvalidOperationException(string.Join("\n", errors));

                var serialized = new SerializedObject(host);
                var objects = serialized.FindProperty("_objects");
                objects.arraySize = entries.Count;
                for (var i = 0; i < entries.Count; i++)
                    objects.GetArrayElementAtIndex(i).objectReferenceValue = entries[i].Value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(host);
                EditorUtility.SetDirty(root);
                succeeded = true;
            }
            finally
            {
                if (succeeded)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                    AssetDatabase.SaveAssets();
                }
                PrefabUtility.UnloadPrefabContents(root);
            }

            if (generateSource)
            {
                var viewPath = Path.Combine(viewDirectory, viewName + ".cs").Replace('\\', '/');
                if (!File.Exists(viewPath))
                    File.WriteAllText(viewPath, CreateViewSource(viewName), new UTF8Encoding(false));
            }

            var bindingPath = Path.Combine(bindingDirectory, viewName + "Bindings.g.cs").Replace('\\', '/');
            File.WriteAllText(bindingPath, CreateBindingSource(viewName, entries), new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log(generateSource
                ? $"Generated UI view script and bindings: {viewName}"
                : $"Updated UI view bindings: {viewName}");
        }

        public static bool IsViewPrefabPath(string prefabPath)
        {
            var normalizedPath = (prefabPath ?? string.Empty).Replace('\\', '/');
            return normalizedPath.IndexOf("/Prefab/UI/Views/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryResolve(Transform transform, out BindingEntry entry, out string error)
        {
            entry = null;
            error = null;
            var name = transform.name;
            var rule = Rules
                .OrderByDescending(item => item.Marker.Length)
                .FirstOrDefault(item => name.StartsWith(item.Marker, StringComparison.Ordinal) ||
                                        name.EndsWith(item.Marker, StringComparison.Ordinal));
            if (rule == null)
                return false;

            var isPrefix = name.StartsWith(rule.Marker, StringComparison.Ordinal);
            var memberName = isPrefix
                ? rule.Label + name.Substring(rule.Marker.Length)
                : name.Substring(0, name.Length - rule.Marker.Length) + "_" + rule.Label;
            memberName = ToPascal(memberName);
            UnityEngine.Object value;
            if (rule.ComponentType == typeof(GameObject))
                value = transform.gameObject;
            else if (rule.ComponentType == typeof(RectTransform))
                value = transform as RectTransform;
            else
                value = transform.GetComponent(rule.ComponentType);

            if (value == null)
            {
                error = $"UI binding '{name}' requires {rule.ComponentType.FullName}.";
                return false;
            }

            entry = new BindingEntry(memberName, rule.ComponentType, value);
            return true;
        }

        private static bool TryResolveNestedHost(Transform transform, out BindingEntry entry, out string error)
        {
            entry = null;
            error = null;
            var name = transform.name;
            var suffix = name.StartsWith("_view", StringComparison.Ordinal)
                ? name.Substring("_view".Length)
                : name;
            if (string.IsNullOrEmpty(suffix))
            {
                error = "Nested UIBindingHost must have a non-empty GameObject name.";
                return false;
            }

            entry = new BindingEntry(ToPascal(suffix) + "Root", typeof(GameObject), transform.gameObject);
            return true;
        }

        private static bool HasBindingHostAncestor(Transform transform, Transform pageRoot)
        {
            var current = transform.parent;
            while (current != null && current != pageRoot)
            {
                if (current.GetComponent<UIBindingHost>() != null)
                    return true;
                current = current.parent;
            }

            return false;
        }

        private static string ToPascal(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Binding";
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if (i == 0)
                    builder.Append(char.ToUpperInvariant(character));
                else if (char.IsLetterOrDigit(character) || character == '_')
                    builder.Append(character);
                else
                    builder.Append('_');
            }
            return builder.ToString();
        }

        private static string CreatePageSource(string pageName)
        {
            var layout = Layout;
            return
                "using Cascade.Core;\n" +
                $"using {layout.BindingNamespace};\n" +
                "\n" +
                $"namespace {layout.PageNamespace}\n" +
                "{\n" +
                $"    [UI(Address = \"{pageName}\")]\n" +
                $"    public sealed class {pageName} : UIPage<{pageName}.Args, {pageName}Bindings>\n" +
                "    {\n" +
                $"        public struct Args : IUIArgs<{pageName}>\n" +
                "        {\n" +
                "        }\n" +
                "\n" +
                "        protected override void OnPageCreate()\n" +
                "        {\n" +
                "        }\n" +
                "\n" +
                "        protected override void OnPageOpen(Args args)\n" +
                "        {\n" +
                "        }\n" +
                "    }\n" +
                "}\n";
        }

        private static string CreateViewSource(string viewName)
        {
            var layout = Layout;
            return
                "using System.Threading;\n" +
                $"using {layout.BindingNamespace};\n" +
                "\n" +
                $"namespace {layout.ViewNamespace}\n" +
                "{\n" +
                $"    public sealed class {viewName} : UIView<object, {viewName}Bindings>\n" +
                "    {\n" +
                $"        public {viewName}({viewName}Bindings bindings, PageContext context)\n" +
                "            : base(bindings, context)\n" +
                "        {\n" +
                "        }\n" +
                "\n" +
                "        // Replace object with the View's concrete model type.\n" +
                "        protected override void OnBind(object model, CancellationToken cancellationToken)\n" +
                "        {\n" +
                "        }\n" +
                "    }\n" +
                "}\n";
        }

        private static string CreateBindingSource(string pageName, IReadOnlyList<BindingEntry> entries)
        {
            var builder = new StringBuilder();
            builder.AppendLine("using Cascade.Core;");
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine("using UnityEngine.UI;");
            if (entries.Any(entry => entry.ComponentType == typeof(TMP_Text)))
                builder.AppendLine("using TMPro;");
            builder.AppendLine();
            builder.AppendLine($"namespace {Layout.BindingNamespace}");
            builder.AppendLine("{");
            builder.AppendLine($"    public sealed class {pageName}Bindings");
            builder.AppendLine("    {");
            builder.AppendLine("        private readonly UIBindingHost _host;");
            builder.AppendLine();
            builder.AppendLine("        public " + pageName + "Bindings(UIBindingHost host)");
            builder.AppendLine("        {");
            builder.AppendLine("            _host = host ?? throw new global::System.ArgumentNullException(nameof(host));");
            builder.AppendLine("        }");
            for (var i = 0; i < entries.Count; i++)
                builder.AppendLine($"        public {GetTypeName(entries[i].ComponentType)} {entries[i].Name} => _host.Get<{GetTypeName(entries[i].ComponentType)}>({i});");
            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string GetTypeName(Type type)
        {
            if (type == typeof(GameObject)) return "GameObject";
            if (type == typeof(RectTransform)) return "RectTransform";
            if (type == typeof(TMP_Text)) return "TMP_Text";
            return type.Name;
        }

        private sealed class BindingRule
        {
            public BindingRule(string marker, string label, Type componentType)
            {
                Marker = marker;
                Label = label;
                ComponentType = componentType;
            }

            public string Marker { get; }
            public string Label { get; }
            public Type ComponentType { get; }
        }

        private sealed class BindingEntry
        {
            public BindingEntry(string name, Type componentType, UnityEngine.Object value)
            {
                Name = name;
                ComponentType = componentType;
                Value = value;
            }

            public string Name { get; }
            public Type ComponentType { get; }
            public UnityEngine.Object Value { get; }
        }
    }
}
