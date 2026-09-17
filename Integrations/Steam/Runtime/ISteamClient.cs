using System;

namespace Cascade.Integrations.Steam
{
    /// <summary>
    /// Steam 客户端能力抽象。无 Steamworks 时使用 <see cref="NullSteamClient"/>。
    /// </summary>
    public interface ISteamClient : IDisposable
    {
        bool Init();
        void Shutdown();
        void RunCallbacks();

        /// <summary>当前玩家 Persona 名；未初始化时可为占位。</summary>
        string PersonaName { get; }

        /// <summary>Steam Overlay 是否激活（完整实现需 GameOverlayActivated 回调；桩可恒为 false）。</summary>
        bool OverlayActive { get; }
    }
}
