using System;

namespace Cascade.Mobile
{
    /// <summary>
    /// Launcher-local download progress for patch UI. Provider-specific DTOs live on
    /// integration types; this type avoids core owning update vocabulary.
    /// </summary>
    public readonly struct LauncherDownloadProgress
    {
        public LauncherDownloadProgress(int totalCount, int currentCount, long totalBytes, long currentBytes)
        {
            TotalCount = totalCount;
            CurrentCount = currentCount;
            TotalBytes = totalBytes;
            CurrentBytes = currentBytes;
        }

        public int TotalCount { get; }
        public int CurrentCount { get; }
        public long TotalBytes { get; }
        public long CurrentBytes { get; }

        public float NormalizedProgress
        {
            get
            {
                if (TotalBytes <= 0)
                    return TotalCount <= 0 ? 0f : (float)CurrentCount / TotalCount;
                return (float)CurrentBytes / TotalBytes;
            }
        }
    }

    public readonly struct LauncherDownloadPlan
    {
        public LauncherDownloadPlan(int totalCount, long totalBytes)
        {
            TotalCount = totalCount;
            TotalBytes = totalBytes;
        }

        public int TotalCount { get; }
        public long TotalBytes { get; }
        public bool NeedsDownload => TotalCount > 0 && TotalBytes > 0;
    }
}
