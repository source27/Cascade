using System;

namespace Cascade.Integrations.Desktop
{
    /// <summary>本地 vs 远程存档冲突决议。</summary>
    public enum CloudSaveConflictResolution
    {
        None = 0,
        UseLocal = 1,
        UseRemote = 2,
        Manual = 3
    }

    /// <summary>一次槽位比较的结果（含两侧信封与时间戳）。</summary>
    public struct CloudSaveConflict
    {
        public int slot;
        public CloudSaveConflictResolution suggested;
        public JsonSaveEnvelope local;
        public JsonSaveEnvelope remote;
        public DateTime? localUpdatedUtc;
        public DateTime? remoteUpdatedUtc;
        public bool hasLocal;
        public bool hasRemote;

        public static CloudSaveConflict None(int slot) => new CloudSaveConflict
        {
            slot = slot,
            suggested = CloudSaveConflictResolution.None,
            hasLocal = false,
            hasRemote = false
        };
    }
}
