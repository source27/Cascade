using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 手柄断开/重连 Toast：实现 <see cref="GamepadDisconnectWatcher.IToastHook"/>，
    /// 也可直接订阅 Watcher 事件。
    /// </summary>
    public sealed class GamepadDisconnectToast : MonoBehaviour, GamepadDisconnectWatcher.IToastHook
    {
        [SerializeField] float _showSeconds = 2.5f;
        [SerializeField] GamepadDisconnectWatcher _watcher;

        GameObject _root;
        Text _text;
        Coroutine _hideCo;

        public static GamepadDisconnectToast Create(GamepadDisconnectWatcher watcher = null)
        {
            var canvas = DesktopModalCanvas.Ensure(5100);
            var go = new GameObject("GamepadDisconnectToast");
            go.transform.SetParent(canvas.transform, false);
            var toast = go.AddComponent<GamepadDisconnectToast>();
            toast._watcher = watcher;
            toast.BuildUi();
            if (watcher != null)
                watcher.BindToast(toast);
            return toast;
        }

        void Awake()
        {
            if (_root == null)
                BuildUi();
        }

        void OnEnable()
        {
            if (_watcher == null)
                _watcher = FindObjectOfType<GamepadDisconnectWatcher>();
            if (_watcher != null)
            {
                _watcher.BindToast(this);
                _watcher.OnGamepadDisconnected += OnDisconnected;
                _watcher.OnGamepadConnected += OnConnected;
            }
        }

        void OnDisable()
        {
            if (_watcher != null)
            {
                _watcher.OnGamepadDisconnected -= OnDisconnected;
                _watcher.OnGamepadConnected -= OnConnected;
            }
        }

        void BuildUi()
        {
            _root = new GameObject("ToastRoot");
            _root.transform.SetParent(transform, false);
            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.85f);
            rt.anchorMax = new Vector2(0.5f, 0.85f);
            rt.sizeDelta = new Vector2(420f, 48f);
            rt.anchoredPosition = Vector2.zero;

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(_root.transform, false);
            _text = tGo.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _text.fontSize = 18;
            _text.alignment = TextAnchor.MiddleCenter;
            _text.color = Color.white;
            var trt = _text.rectTransform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            _root.SetActive(false);
        }

        void OnDisconnected() => ShowToast("Gamepad disconnected");
        void OnConnected() => ShowToast("Gamepad connected");

        public void ShowToast(string message)
        {
            if (_text != null)
                _text.text = message ?? string.Empty;
            if (_root != null)
                _root.SetActive(true);
            if (_hideCo != null)
                StopCoroutine(_hideCo);
            _hideCo = StartCoroutine(HideAfter());
        }

        IEnumerator HideAfter()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, _showSeconds));
            if (_root != null)
                _root.SetActive(false);
            _hideCo = null;
        }
    }
}
