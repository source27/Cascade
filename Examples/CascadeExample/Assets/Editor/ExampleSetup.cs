using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using YooAsset.Editor;

namespace CascadeExample.Editor
{
    /// <summary>
    /// 创建示例工程自己的资产（client 迁移来的场景/启动 UI/UIRoot 不需要生成）：
    /// Home/Detail 页面预制体（UIBindingHost 对象顺序 = Bindings 索引）、SFX 音效、
    /// 并把 Bootstrap 场景加入 Build Settings。菜单：CascadeExample → Create Example Pages。
    /// </summary>
    public static class ExampleSetup
    {
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string HomePath = "Assets/Resources/CascadeUI/Home.prefab";
        private const string DetailPath = "Assets/Resources/CascadeUI/Detail.prefab";
        private const string SfxPath = "Assets/Resources/CascadeUI/SfxClick.audioClip";

        [MenuItem("CascadeExample/Create Example Pages", priority = 100)]
        public static void Setup()
        {
            EnsureFolder("Assets/Resources/CascadeUI");

            BuildHomePrefab();
            BuildDetailPrefab();
            BuildSfxClip();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "CascadeExample",
                "页面预制体与 SFX 已创建。\n\n打开 Assets/Scenes/Bootstrap.unity 并点击 Play\n" +
                "（启动界面/进度条来自 client 迁移的 PatchWindow 预制体）。",
                "OK");
        }

        private static void BuildHomePrefab()
        {
            var root = new GameObject("Home", typeof(RectTransform), typeof(Cascade.Core.UIBindingHost));
            Stretch((RectTransform)root.transform);

            var bindings = new UnityEngine.Object[]
            {
                MakeButton(root.transform, "BtnLocale", new Vector2(0f, 260f)),
                MakeButton(root.transform, "BtnSave", new Vector2(0f, 180f)),
                MakeButton(root.transform, "BtnLoad", new Vector2(0f, 100f)),
                MakeButton(root.transform, "BtnAudio", new Vector2(0f, 20f)),
                MakeButton(root.transform, "BtnDetail", new Vector2(0f, -60f)),
                MakeButton(root.transform, "BtnBack", new Vector2(0f, -140f)),
                MakeText(root.transform, "TxtStatus", new Vector2(0f, -240f), 40),
                MakeText(root.transform, "TxtCounter", new Vector2(0f, -300f), 32),
                MakeText(root.transform, "TxtTitle", new Vector2(0f, 360f), 56),
            };
            SetBindings(root.GetComponent<Cascade.Core.UIBindingHost>(), bindings);

            SavePrefab(root, HomePath);
        }

        private static void BuildDetailPrefab()
        {
            var root = new GameObject("Detail", typeof(RectTransform), typeof(Cascade.Core.UIBindingHost));
            Stretch((RectTransform)root.transform);

            var bindings = new UnityEngine.Object[]
            {
                MakeText(root.transform, "TxtTitle", new Vector2(0f, 120f), 48),
                MakeButton(root.transform, "BtnBack", new Vector2(0f, -160f)),
            };
            SetBindings(root.GetComponent<Cascade.Core.UIBindingHost>(), bindings);

            SavePrefab(root, DetailPath);
        }

        [MenuItem("CascadeExample/Configure YooAsset + Localization", priority = 101)]
        public static void ConfigureYooAsset()
        {
            EnsureFolder("Assets/Resources/CascadeUI/Localization");
            EnsureFolder("Assets/Code");
            WriteLocalizationData();
            ConfigureCollector();
            EditorUtility.DisplayDialog(
                "CascadeExample",
                "YooAsset 收集器已配置（CascadePak）：\n" +
                "UI 组（Resources/CascadeUI，AddressByFileName + PackSeparately）\n" +
                "Localization 组（RawFile）+ Code 组（RawFile，热更 dll/元数据）\n" +
                "本地化数据（localization_catalog / en / zh）已生成。",
                "OK");
        }

        private static void WriteLocalizationData()
        {
            WriteJson("Assets/Resources/CascadeUI/Localization/localization_catalog.json",
                "{\"defaultLocale\":\"en\",\"locales\":[{\"code\":\"en\",\"location\":\"en\"},{\"code\":\"zh\",\"location\":\"zh\"}]}");
            WriteJson("Assets/Resources/CascadeUI/Localization/en.json",
                "{\"entries\":[" +
                "{\"id\":\"home.title\",\"value\":\"Cascade Demo\"}," +
                "{\"id\":\"home.locale\",\"value\":\"Switch locale\"}," +
                "{\"id\":\"home.save\",\"value\":\"Save value\"}," +
                "{\"id\":\"home.load\",\"value\":\"Load value\"}," +
                "{\"id\":\"home.audio\",\"value\":\"Play SFX\"}," +
                "{\"id\":\"home.counter\",\"value\":\"Update counter\"}," +
                "{\"id\":\"home.detail\",\"value\":\"Open Detail\"}," +
                "{\"id\":\"home.back\",\"value\":\"Back\"}," +
                "{\"id\":\"home.status\",\"value\":\"Ready\"}]}");
            WriteJson("Assets/Resources/CascadeUI/Localization/zh.json",
                "{\"entries\":[" +
                "{\"id\":\"home.title\",\"value\":\"Cascade 示例\"}," +
                "{\"id\":\"home.locale\",\"value\":\"切换语言\"}," +
                "{\"id\":\"home.save\",\"value\":\"保存\"}," +
                "{\"id\":\"home.load\",\"value\":\"读取\"}," +
                "{\"id\":\"home.audio\",\"value\":\"播放音效\"}," +
                "{\"id\":\"home.counter\",\"value\":\"更新计数\"}," +
                "{\"id\":\"home.detail\",\"value\":\"打开详情\"}," +
                "{\"id\":\"home.back\",\"value\":\"返回\"}," +
                "{\"id\":\"home.status\",\"value\":\"就绪\"}]}");
            AssetDatabase.Refresh();
        }

        private static void WriteJson(string path, string content)
        {
            File.WriteAllText(path, content);
            AssetDatabase.ImportAsset(path);
        }

        private static void ConfigureCollector()
        {
            var setting = BundleCollectorSettingData.Setting;
            var package = setting.Packages.Find(item => item.PackageName == "CascadePak");
            if (package == null)
                package = BundleCollectorSettingData.CreatePackage("CascadePak");

            EnsureCollector(package, "UI", "Assets/Resources/CascadeUI", nameof(AddressByFileName), nameof(PackSeparately), nameof(CollectAll));
            EnsureCollector(package, "Localization", "Assets/Resources/CascadeUI/Localization", nameof(AddressByFileName), nameof(PackRawFile), nameof(CollectAll));
            EnsureCollector(package, "Code", "Assets/Code", nameof(AddressByFileName), nameof(PackRawFile), nameof(CollectAll));
            BundleCollectorSettingData.SaveFile();
        }

        private static void EnsureCollector(
            BundleCollectorPackage package,
            string groupName,
            string collectPath,
            string addressRule,
            string packRule,
            string filterRule)
        {
            var group = package.Groups.Find(item => item.GroupName == groupName)
                        ?? BundleCollectorSettingData.CreateGroup(package, groupName);
            var collector = group.Collectors.Find(item => item.CollectPath == collectPath);
            if (collector == null)
            {
                collector = new BundleCollector();
                BundleCollectorSettingData.CreateCollector(group, collector);
            }

            collector.CollectPath = collectPath;
            collector.CollectorGUID = AssetDatabase.AssetPathToGUID(collectPath);
            collector.CollectorType = ECollectorType.MainAssetCollector;
            collector.AddressRuleName = addressRule;
            collector.PackRuleName = packRule;
            collector.FilterRuleName = filterRule;
            BundleCollectorSettingData.ModifyCollector(group, collector);
        }

        private static Button MakeButton(Transform parent, string name, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(480f, 60f);
            rect.anchoredPosition = anchoredPosition;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.2f, 0.4f, 0.8f, 1f);

            var text = MakeText(go.transform, "Label", new Vector2(0f, 0f), 28);
            var textRect = (RectTransform)text.transform;
            textRect.sizeDelta = new Vector2(460f, 40f);

            return go.GetComponent<Button>();
        }

        private static Text MakeText(Transform parent, string name, Vector2 anchoredPosition, int fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(800f, 50f);
            rect.anchoredPosition = anchoredPosition;

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static void SetBindings(Cascade.Core.UIBindingHost host, UnityEngine.Object[] objects)
        {
            var so = new SerializedObject(host);
            var prop = so.FindProperty("_objects");
            prop.arraySize = objects.Length;
            for (var i = 0; i < objects.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SavePrefab(GameObject root, string path)
        {
            if (File.Exists(path))
                AssetDatabase.DeleteAsset(path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
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
