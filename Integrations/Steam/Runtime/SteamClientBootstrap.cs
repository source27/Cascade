using System;
using UnityEngine;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace Cascade.Integrations.Steam
{
    /// <summary>
    /// 创建 <see cref="ISteamClient"/>。
    /// <c>#if STEAMWORKS_NET</c> → 真实 Steamworks 实现；否则 <see cref="NullSteamClient"/>。
    /// 不捆绑任何 Steamworks DLL。
    /// </summary>
    public static class SteamClientBootstrap
    {
        /// <summary>创建客户端实例（尚未 Init）。</summary>
        public static ISteamClient Create()
        {
#if STEAMWORKS_NET
            return new SteamworksSteamClient();
#else
            return new NullSteamClient();
#endif
        }
    }

#if STEAMWORKS_NET
    /// <summary>编译期接入 Steamworks.NET 时的实现。</summary>
    public sealed class SteamworksSteamClient : ISteamClient
    {
        bool _initialized;
        bool _overlayActive;
        Callback<GameOverlayActivated_t> _overlayCallback;

        public string PersonaName
        {
            get
            {
                try
                {
                    return _initialized ? SteamFriends.GetPersonaName() : "Player";
                }
                catch
                {
                    return "Player";
                }
            }
        }

        /// <summary>Overlay 激活状态；若回调未挂上则为 stub（恒 false）。</summary>
        public bool OverlayActive => _overlayActive;

        public bool Init()
        {
            try
            {
                if (!SteamAPI.Init())
                {
                    Debug.LogWarning("[SteamworksSteamClient] SteamAPI.Init returned false.");
                    return false;
                }

                _initialized = true;
                try
                {
                    _overlayCallback = Callback<GameOverlayActivated_t>.Create(OnOverlay);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[SteamworksSteamClient] Overlay callback stub failed: {e.Message}");
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamworksSteamClient] Init exception: {e.Message}");
                _initialized = false;
                return false;
            }
        }

        void OnOverlay(GameOverlayActivated_t ev)
        {
            _overlayActive = ev.m_bActive != 0;
        }

        public void Shutdown()
        {
            if (!_initialized)
                return;
            try
            {
                _overlayCallback?.Dispose();
                _overlayCallback = null;
                SteamAPI.Shutdown();
            }
            catch
            {
                // ignore
            }
            finally
            {
                _initialized = false;
                _overlayActive = false;
            }
        }

        public void RunCallbacks()
        {
            if (!_initialized)
                return;
            try
            {
                SteamAPI.RunCallbacks();
            }
            catch
            {
                // ignore
            }
        }

        public void Dispose() => Shutdown();
    }
#endif
}
