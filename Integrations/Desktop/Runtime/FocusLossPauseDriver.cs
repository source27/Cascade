using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 应用失去焦点 / 暂停时将 <see cref="Time.timeScale"/> 置 0；恢复焦点时还原。
    /// PC shell 通用；Steam Overlay 暂停见 Integrations.Steam.SteamOverlayPauseDriver。
    /// </summary>
    public sealed class FocusLossPauseDriver : MonoBehaviour
    {
        [SerializeField] bool _enabled = true;
        [SerializeField] bool _pauseOnFocusLoss = true;
        [SerializeField] bool _pauseOnApplicationPause = true;

        float _savedTimeScale = 1f;
        bool _pausedByUs;

        void OnApplicationFocus(bool hasFocus)
        {
            if (!_enabled || !_pauseOnFocusLoss)
                return;

            if (!hasFocus)
                EnterPause();
            else
                ExitPause();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (!_enabled || !_pauseOnApplicationPause)
                return;

            if (pauseStatus)
                EnterPause();
            else
                ExitPause();
        }

        void EnterPause()
        {
            if (_pausedByUs)
                return;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            _pausedByUs = true;
        }

        void ExitPause()
        {
            if (!_pausedByUs)
                return;
            Time.timeScale = _savedTimeScale;
            _pausedByUs = false;
        }

        void OnDisable() => ExitPause();
    }
}
