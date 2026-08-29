using System.IO;
using Cascade.Launcher;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CascadeExample.Editor
{
    /// <summary>
    /// One-shot editor setup for the demo: builds the Bootstrap scene, the UIRoot
    /// canvas prefab, the Home/Detail page prefabs (with UIBindingHost objects in
    /// the exact order the Bindings classes expect), a silent SFX clip, and adds
    /// the scene to Build Settings. Run via menu: CascadeExample → Setup Demo Scene.
    /// </summary>
    public static class ExampleSetup
    {
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string UiRootPath = "Assets/Resources/CascadeUI/UIRoot.prefab";
        private const string HomePath = "Assets/Resources/CascadeUI/Home.prefab";
        private const string DetailPath = "Assets/Resources/CascadeUI/Detail.prefab";
        private const string SfxPath = "Assets/Resources/CascadeUI/SfxClick.audioClip";

        [MenuItem("CascadeExample/Setup Demo Scene")]
        public static void Setup()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder("Assets/Resources/CascadeUI");

            BuildUiRootPrefab();
            BuildHomePrefab();
            BuildDetailPrefab();
            BuildSfxClip();
            BuildBootstrapScene();

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "CascadeExample",
                "Demo scene and prefabs created.\n\nOpen Assets/Scenes/Bootstrap.unity and press Play.\n" +
                "The launcher runs in EditorSimulate: the hot-update assembly is loaded from the editor compile.",
                "OK");
        }

        private static void BuildUiRootPrefab()
        {
            var canvas = new GameObject(
                "UIRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(Cascade.Core.UIRoot));
            Stretch((RectTransform)canvas.transform);

            var c = canvas.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            SavePrefab(canvas, UiRootPath);
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

        private static void BuildSfxClip()
        {
            const int sampleRate = 44100;
            const float duration = 0.08f;
            var clip = AudioClip.Create("SfxClick", (int)(sampleRate * duration), 1, sampleRate, false);
            var data = new float[(int)(sampleRate * duration)];
            for (var i = 0; i < data.Length; i++)
            {
                var t = (float)i / sampleRate;
                data[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * (1f - t / duration);
            }
            clip.SetData(data, 0);
            AssetDatabase.CreateAsset(clip, SfxPath);
        }

        private static void BuildBootstrapScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrap = new GameObject("Bootstrap");
            var entry = bootstrap.AddComponent<ExampleBootstrapEntry>();
            // EditorSimulate: CodeLoader finds the already-compiled GameLogic.HotUpdate assembly.
            var so = new SerializedObject(entry);
            so.FindProperty("playMode").enumValueIndex = (int)BootstrapPlayMode.EditorSimulate;
            so.ApplyModifiedPropertiesWithoutUndo();

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.SaveScene(scene, ScenePath);
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
