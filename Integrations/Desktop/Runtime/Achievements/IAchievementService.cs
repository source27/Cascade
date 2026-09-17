namespace Cascade.Integrations.Desktop
{
    /// <summary>成就服务接口。Steam 实现可放在 Integrations.Steam 或游戏侧。</summary>
    public interface IAchievementService
    {
        bool Unlock(string achievementId);
        bool IsUnlocked(string achievementId);
        void ResetAll();
    }
}
