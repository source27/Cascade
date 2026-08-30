using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Cascade.Indie
{
    internal static class IndieSmokeUi
    {
        public static GameObject CreateCanvas(string name)
        {
            EnsureEventSystem();
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            return go;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
                return;
            var es = new GameObject(
                "EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            Object.DontDestroyOnLoad(es);
        }

        public static Text AddLabel(Transform parent, string message, float anchorY)
        {
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(parent, false);
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, anchorY - 0.1f);
            rt.anchorMax = new Vector2(0.9f, anchorY + 0.1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 32;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = message;
            return text;
        }

        public static Button AddButton(Transform parent, string label, UnityAction onClick, float anchorY)
        {
            var buttonGo = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);
            var rt = buttonGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.3f, anchorY - 0.05f);
            rt.anchorMax = new Vector2(0.7f, anchorY + 0.05f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            buttonGo.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.85f, 0.9f);
            var button = buttonGo.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(buttonGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 28;
            text.color = Color.white;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label;
            return button;
        }
    }
}
