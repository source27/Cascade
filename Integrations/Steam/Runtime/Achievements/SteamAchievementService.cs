using Cascade.Integrations.Desktop;
using UnityEngine;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace Cascade.Integrations.Steam
{
    /// <summary>
    /// Steam 成就实现 Desktop <see cref="IAchievementService"/>。
    /// 无 <c>STEAMWORKS_NET</c> 时 <see cref="Create"/> 返回 <see cref="NullAchievementService"/>。
    /// </summary>
    public sealed class SteamAchievementService : IAchievementService
    {
        SteamAchievementService() { }

        /// <summary>有 Steamworks 时返回真实实现，否则返回 <see cref="NullAchievementService"/>。</summary>
        public static IAchievementService Create()
        {
#if STEAMWORKS_NET
            return new SteamAchievementService();
#else
            return new NullAchievementService();
#endif
        }

        public bool Unlock(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId))
                return false;
#if STEAMWORKS_NET
            try
            {
                if (!SteamUserStats.SetAchievement(achievementId))
                {
                    Debug.LogWarning($"[SteamAchievement] SetAchievement failed: {achievementId}");
                    return false;
                }
                return SteamUserStats.StoreStats();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SteamAchievement] Unlock: {e.Message}");
                return false;
            }
#else
            Debug.Log($"[SteamAchievement:no-steam] Unlock: {achievementId}");
            return true;
#endif
        }

        public bool IsUnlocked(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId))
                return false;
#if STEAMWORKS_NET
            try
            {
                bool achieved;
                if (!SteamUserStats.GetAchievement(achievementId, out achieved))
                    return false;
                return achieved;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SteamAchievement] IsUnlocked: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// 清除全部成就统计。Steamworks 提供 <c>ClearAchievement</c> 按 id；
        /// 无 id 列表时本实现仅打日志（Editor/调试请按游戏已知 id 列表循环 Clear）。
        /// </summary>
        public void ResetAll()
        {
#if STEAMWORKS_NET
            // Steamworks 无“清空全部”API；需游戏侧已知成就 id 列表。
            // 提供 ClearAchievement 单条封装供游戏循环调用，见 Clear(string)。
            Debug.LogWarning("[SteamAchievement] ResetAll: Steam has no ClearAll; call Clear(id) per achievement (editor/debug).");
#else
            // no-op / null-like
#endif
        }

        /// <summary>清除单个成就（调试用）。无 Steamworks 时 no-op。</summary>
        public bool Clear(string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId))
                return false;
#if STEAMWORKS_NET
            try
            {
                if (!SteamUserStats.ClearAchievement(achievementId))
                    return false;
                return SteamUserStats.StoreStats();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SteamAchievement] Clear: {e.Message}");
                return false;
            }
#else
            return false;
#endif
        }
    }
}
