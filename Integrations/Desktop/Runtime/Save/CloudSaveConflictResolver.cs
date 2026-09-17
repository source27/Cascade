using System;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 比较本地/远程信封：较新的 <c>updatedUtc</c> 胜出；相等 → Manual；缺一侧 → 用另一侧。
    /// </summary>
    public static class CloudSaveConflictResolver
    {
        public static CloudSaveConflict Compare(int slot, JsonSaveEnvelope local, JsonSaveEnvelope remote)
        {
            var c = new CloudSaveConflict
            {
                slot = slot,
                local = local,
                remote = remote,
                hasLocal = local != null,
                hasRemote = remote != null,
                localUpdatedUtc = ParseUtc(local?.updatedUtc),
                remoteUpdatedUtc = ParseUtc(remote?.updatedUtc)
            };

            if (!c.hasLocal && !c.hasRemote)
            {
                c.suggested = CloudSaveConflictResolution.None;
                return c;
            }

            if (c.hasLocal && !c.hasRemote)
            {
                c.suggested = CloudSaveConflictResolution.UseLocal;
                return c;
            }

            if (!c.hasLocal && c.hasRemote)
            {
                c.suggested = CloudSaveConflictResolution.UseRemote;
                return c;
            }

            // both present
            if (!c.localUpdatedUtc.HasValue && !c.remoteUpdatedUtc.HasValue)
            {
                c.suggested = CloudSaveConflictResolution.Manual;
                return c;
            }

            if (c.localUpdatedUtc.HasValue && !c.remoteUpdatedUtc.HasValue)
            {
                c.suggested = CloudSaveConflictResolution.UseLocal;
                return c;
            }

            if (!c.localUpdatedUtc.HasValue && c.remoteUpdatedUtc.HasValue)
            {
                c.suggested = CloudSaveConflictResolution.UseRemote;
                return c;
            }

            var cmp = DateTime.Compare(c.localUpdatedUtc.Value, c.remoteUpdatedUtc.Value);
            if (cmp > 0)
                c.suggested = CloudSaveConflictResolution.UseLocal;
            else if (cmp < 0)
                c.suggested = CloudSaveConflictResolution.UseRemote;
            else
                c.suggested = CloudSaveConflictResolution.Manual;

            return c;
        }

        public static DateTime? ParseUtc(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso))
                return null;
            if (DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
            {
                if (dt.Kind == DateTimeKind.Unspecified)
                    dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
                return dt.ToUniversalTime();
            }
            return null;
        }
    }
}
