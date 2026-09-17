using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    public sealed class NullAchievementService : IAchievementService
    {
        public bool Unlock(string achievementId)
        {
            Debug.Log($"[NullAchievement] Unlock: {achievementId}");
            return true;
        }

        public bool IsUnlocked(string achievementId) => false;

        public void ResetAll()
        {
        }
    }
}
