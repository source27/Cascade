using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Cascade.Integrations.InputGlyphs
{
    /// <summary>
    /// 监听 <see cref="InputSystem.onActionChange"/> / 输入事件，发出当前 scheme 名（Gamepad vs Keyboard&amp;Mouse）。
    /// </summary>
    public sealed class ControlSchemeWatcher : IDisposable
    {
        public string GamepadSchemeName { get; set; } = "Gamepad";
        public string KeyboardMouseSchemeName { get; set; } = "Keyboard&Mouse";

        public string CurrentScheme { get; private set; }

        public event Action<string> SchemeChanged;

        bool _disposed;

        public ControlSchemeWatcher()
        {
            CurrentScheme = KeyboardMouseSchemeName;
            InputSystem.onActionChange += OnActionChange;
            InputSystem.onEvent += OnEvent;
        }

        void OnActionChange(object obj, InputActionChange change)
        {
            if (_disposed)
                return;
            if (change != InputActionChange.ActionPerformed && change != InputActionChange.ActionStarted)
                return;

            if (obj is InputAction action)
                ConsiderControls(action.activeControl);
        }

        void OnEvent(InputEventPtr eventPtr, InputDevice device)
        {
            if (_disposed || device == null)
                return;
            if (!eventPtr.IsA<StateEvent>() && !eventPtr.IsA<DeltaStateEvent>())
                return;

            if (device is Gamepad)
                SetScheme(GamepadSchemeName);
            else if (device is Keyboard || device is Mouse)
                SetScheme(KeyboardMouseSchemeName);
        }

        void ConsiderControls(InputControl control)
        {
            if (control == null)
                return;
            var device = control.device;
            if (device is Gamepad)
                SetScheme(GamepadSchemeName);
            else if (device is Keyboard || device is Mouse)
                SetScheme(KeyboardMouseSchemeName);
        }

        void SetScheme(string scheme)
        {
            if (string.Equals(CurrentScheme, scheme, StringComparison.Ordinal))
                return;
            CurrentScheme = scheme;
            SchemeChanged?.Invoke(scheme);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onEvent -= OnEvent;
        }
    }
}
