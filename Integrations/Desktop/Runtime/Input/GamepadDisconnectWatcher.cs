using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 检测手柄断开/重连并抛事件。可选 toast 钩子（游戏 UI 实现）。
    /// 有 Input System 时用 onDeviceChange；否则轮询 GetJoystickNames。
    /// </summary>
    public sealed class GamepadDisconnectWatcher : MonoBehaviour
    {
        public interface IToastHook
        {
            void ShowToast(string message);
        }

        public event Action OnGamepadDisconnected;
        public event Action OnGamepadConnected;

        [SerializeField] bool _pollLegacy = true;
        [SerializeField] float _pollInterval = 0.5f;

        IToastHook _toast;
        string[] _lastJoysticks = Array.Empty<string>();
        float _nextPoll;

        public void BindToast(IToastHook toast) => _toast = toast;

        void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            InputSystem.onDeviceChange += OnDeviceChange;
#endif
            _lastJoysticks = SafeJoystickNames();
        }

        void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            InputSystem.onDeviceChange -= OnDeviceChange;
#endif
        }

        void Update()
        {
            if (!_pollLegacy)
                return;
#if ENABLE_INPUT_SYSTEM
            // Input System 已覆盖时可不轮询
            return;
#else
            if (Time.unscaledTime < _nextPoll)
                return;
            _nextPoll = Time.unscaledTime + Mathf.Max(0.1f, _pollInterval);
            var now = SafeJoystickNames();
            int prev = CountNonEmpty(_lastJoysticks);
            int cur = CountNonEmpty(now);
            if (cur < prev)
                RaiseDisconnected();
            else if (cur > prev)
                RaiseConnected();
            _lastJoysticks = now;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad))
                return;

            if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected)
                RaiseDisconnected();
            else if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected)
                RaiseConnected();
        }
#endif

        void RaiseDisconnected()
        {
            OnGamepadDisconnected?.Invoke();
            _toast?.ShowToast("Gamepad disconnected");
        }

        void RaiseConnected()
        {
            OnGamepadConnected?.Invoke();
            _toast?.ShowToast("Gamepad connected");
        }

        static string[] SafeJoystickNames()
        {
            try { return Input.GetJoystickNames() ?? Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }

        static int CountNonEmpty(string[] arr)
        {
            int n = 0;
            if (arr == null) return 0;
            foreach (var s in arr)
                if (!string.IsNullOrEmpty(s)) n++;
            return n;
        }
    }
}
