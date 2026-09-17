using System;
using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 请求退出：抛 <see cref="OnQuitRequested"/>；实际模态框由游戏 UI 实现。
    /// ConfirmQuit 调用 Application.Quit（Editor 停 Play）。
    /// </summary>
    public sealed class QuitConfirmGate : MonoBehaviour
    {
        public event Action OnQuitRequested;
        public event Action OnQuitCancelled;

        public void RequestQuit()
        {
            OnQuitRequested?.Invoke();
        }

        public void CancelQuit()
        {
            OnQuitCancelled?.Invoke();
        }

        public void ConfirmQuit()
        {
#if UNITY_EDITOR
            var t = Type.GetType("UnityEditor.EditorApplication,UnityEditor");
            t?.GetProperty("isPlaying")?.SetValue(null, false);
#else
            Application.Quit();
#endif
        }
    }
}
