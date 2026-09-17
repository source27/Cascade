using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Integrations.Desktop
{
    /// <summary>运行时构建的退出确认框：Yes/No，绑定 <see cref="QuitConfirmGate"/>。</summary>
    public sealed class QuitConfirmModal : MonoBehaviour
    {
        [SerializeField] QuitConfirmGate _gate;
        GameObject _panel;
        Text _message;

        public static QuitConfirmModal Create(QuitConfirmGate gate = null, string message = "Quit?")
        {
            var canvas = DesktopModalCanvas.Ensure();
            var go = new GameObject("QuitConfirmModal");
            go.transform.SetParent(canvas.transform, false);
            var modal = go.AddComponent<QuitConfirmModal>();
            modal._gate = gate != null ? gate : go.AddComponent<QuitConfirmGate>();
            modal.Build(message);
            modal.Hide();
            return modal;
        }

        void OnEnable()
        {
            if (_gate == null)
                _gate = GetComponent<QuitConfirmGate>() ?? FindObjectOfType<QuitConfirmGate>();
            if (_gate != null)
                _gate.OnQuitRequested += Show;
        }

        void OnDisable()
        {
            if (_gate != null)
                _gate.OnQuitRequested -= Show;
        }

        void Build(string message)
        {
            var blocker = new GameObject("Blocker");
            blocker.transform.SetParent(transform, false);
            var blockerImg = blocker.AddComponent<Image>();
            blockerImg.color = new Color(0f, 0f, 0f, 0.55f);
            StretchFull(blocker.GetComponent<RectTransform>());

            _panel = new GameObject("Panel");
            _panel.transform.SetParent(transform, false);
            var panelImg = _panel.AddComponent<Image>();
            panelImg.color = new Color(0.12f, 0.12f, 0.14f, 0.98f);
            var prt = _panel.GetComponent<RectTransform>();
            prt.sizeDelta = new Vector2(420f, 180f);
            prt.anchoredPosition = Vector2.zero;

            var msgGo = new GameObject("Message");
            msgGo.transform.SetParent(_panel.transform, false);
            _message = msgGo.AddComponent<Text>();
            _message.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _message.fontSize = 22;
            _message.alignment = TextAnchor.MiddleCenter;
            _message.color = Color.white;
            _message.text = message ?? "Quit?";
            var mrt = _message.rectTransform;
            mrt.anchorMin = new Vector2(0.05f, 0.45f);
            mrt.anchorMax = new Vector2(0.95f, 0.95f);
            mrt.offsetMin = Vector2.zero;
            mrt.offsetMax = Vector2.zero;

            CreateButton(_panel.transform, "Yes", new Vector2(-90f, -50f), () =>
            {
                Hide();
                _gate?.ConfirmQuit();
            });
            CreateButton(_panel.transform, "No", new Vector2(90f, -50f), () =>
            {
                Hide();
                _gate?.CancelQuit();
            });
        }

        static void CreateButton(Transform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label + "Button");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 40f);
            rt.anchoredPosition = pos;

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            var t = tGo.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.fontSize = 18;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = label;
            StretchFull(t.rectTransform);
        }

        static void StretchFull(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void BindGate(QuitConfirmGate gate)
        {
            if (_gate != null)
                _gate.OnQuitRequested -= Show;
            _gate = gate;
            if (isActiveAndEnabled && _gate != null)
                _gate.OnQuitRequested += Show;
        }
    }
}
