using UnityEngine;

namespace Cascade.Integrations.Steam
{
    /// <summary>无 Steamworks / Init 失败时的空实现。</summary>
    public sealed class NullSteamClient : ISteamClient
    {
        public string PersonaName => "Player";
        public bool OverlayActive => false;

        public bool Init()
        {
            Debug.Log("[NullSteamClient] Init (no-op). Install Steamworks.NET and define STEAMWORKS_NET for real Steam.");
            return true;
        }

        public void Shutdown()
        {
        }

        public void RunCallbacks()
        {
        }

        public void Dispose() => Shutdown();
    }
}
