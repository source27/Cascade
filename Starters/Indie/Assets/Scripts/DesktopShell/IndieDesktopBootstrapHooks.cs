using UnityEngine;
using Cascade.Integrations.Desktop;
#if CASCADE_STEAM || STEAMWORKS_NET
using Cascade.Integrations.Steam;
#endif

namespace Cascade.Starters.Indie.DesktopShell
{
    /// <summary>
    /// Indie Starter 附加引导：LoadAndApply 设置、FocusLoss；可选 Steam 钩子（软引用/宏）。
    /// 复制到 <c>Starters/Indie/Assets/Scripts/DesktopShell/</c>，挂到启动场景空物体即可。
    /// </summary>
    public sealed class IndieDesktopBootstrapHooks : MonoBehaviour
    {
        [SerializeField] bool _addFocusLossPause = true;
        [SerializeField] bool _trySteam = true;
        [SerializeField] bool _showSettingsHotkey = true;
        [SerializeField] KeyCode _settingsKey = KeyCode.F10;

        GameSettingsService _settings;
#if CASCADE_STEAM || STEAMWORKS_NET
        ISteamClient _steam;
#endif

        void Awake()
        {
            _settings = new GameSettingsService();
            _settings.LoadAndApply();

            if (_addFocusLossPause && GetComponent<FocusLossPauseDriver>() == null)
                gameObject.AddComponent<FocusLossPauseDriver>();

            if (_trySteam)
                TryWireSteam();
        }

        void Update()
        {
#if CASCADE_STEAM || STEAMWORKS_NET
            _steam?.RunCallbacks();
#endif
            if (_showSettingsHotkey && Input.GetKeyDown(_settingsKey))
                SimpleSettingsPanel.Show(_settings);
        }

        void OnDestroy()
        {
#if CASCADE_STEAM || STEAMWORKS_NET
            _steam?.Shutdown();
#endif
        }

        void TryWireSteam()
        {
#if CASCADE_STEAM || STEAMWORKS_NET
            try
            {
                _steam = SteamClientBootstrap.Create();
                if (_steam == null || !_steam.Init())
                {
                    Debug.LogWarning("[IndieDesktop] Steam Init failed; continuing without Steam.");
                    _steam = null;
                    return;
                }

                var overlay = gameObject.GetComponent<SteamOverlayPauseDriver>();
                if (overlay == null)
                    overlay = gameObject.AddComponent<SteamOverlayPauseDriver>();
                overlay.Bind(_steam);

                // Achievements factory (optional register via game locator)
                var achievements = SteamAchievementService.Create();
                Debug.Log($"[IndieDesktop] Achievements ready: {achievements.GetType().Name}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[IndieDesktop] Steam wire skipped: {e.Message}");
            }
#else
            // Soft: no Steam asm → skip silently
#endif
        }

        public GameSettingsService Settings => _settings;
    }
}
