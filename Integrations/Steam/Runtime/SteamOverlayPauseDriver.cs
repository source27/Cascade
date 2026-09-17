using UnityEngine;

namespace Cascade.Integrations.Steam
{
    /// <summary>
    /// 当 Steam Overlay 激活时将 <see cref="Time.timeScale"/> 置 0；关闭后恢复。
    /// 需在组合根 <c>Bind(ISteamClient)</c>，并持续调用客户端 <c>RunCallbacks</c>。
    /// </summary>
    public sealed class SteamOverlayPauseDriver : MonoBehaviour
    {
        [SerializeField] bool _enabled = true;

        ISteamClient _client;
        float _savedTimeScale = 1f;
        bool _pausedByOverlay;

        public void Bind(ISteamClient client) => _client = client;

        void Update()
        {
            if (!_enabled || _client == null)
                return;

            var overlay = _client.OverlayActive;
            if (overlay && !_pausedByOverlay)
            {
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _pausedByOverlay = true;
            }
            else if (!overlay && _pausedByOverlay)
            {
                Time.timeScale = _savedTimeScale;
                _pausedByOverlay = false;
            }
        }

        void OnDisable()
        {
            if (_pausedByOverlay)
            {
                Time.timeScale = _savedTimeScale;
                _pausedByOverlay = false;
            }
        }
    }
}
