using System;
using System.Threading;
using Cascade.Service;
using Cascade.Service.YooAsset;
using Cysharp.Threading.Tasks;

namespace Cascade.Mobile
{
    /// <summary>
    /// Adapts YooAsset update APIs to launcher DTOs. Starter owns the update pipeline;
    /// core IResourceService stays load-only.
    /// </summary>
    internal static class ResourceUpdateBridge
    {
        public static bool IsUsingLocalVersion(IResourceService resources)
        {
            return AsYoo(resources).IsUsingLocalVersion;
        }

        public static string ActivePackageVersion(IResourceService resources)
        {
            return AsYoo(resources).ActivePackageVersion ?? string.Empty;
        }

        public static UniTask<string> RequestVersionAsync(
            IResourceService resources,
            CancellationToken cancellationToken)
        {
            return AsYoo(resources).RequestVersionAsync(cancellationToken);
        }

        public static UniTask UpdateManifestAsync(
            IResourceService resources,
            string version,
            CancellationToken cancellationToken)
        {
            return AsYoo(resources).UpdateManifestAsync(version, cancellationToken);
        }

        public static LauncherDownloadPlan PrepareDownload(IResourceService resources)
        {
            var plan = AsYoo(resources).PrepareDownload();
            return new LauncherDownloadPlan(plan.TotalCount, plan.TotalBytes);
        }

        public static UniTask DownloadAsync(
            IResourceService resources,
            IProgress<LauncherDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            IProgress<YooAssetDownloadProgress> adapted = progress == null
                ? null
                : new ProgressAdapter(progress);
            return AsYoo(resources).DownloadAsync(adapted, cancellationToken);
        }

        public static UniTask ClearUnusedCacheAsync(
            IResourceService resources,
            CancellationToken cancellationToken)
        {
            return AsYoo(resources).ClearUnusedCacheAsync(cancellationToken);
        }

        private static YooAssetResourceService AsYoo(IResourceService resources)
        {
            if (resources == null)
                throw new ArgumentNullException(nameof(resources));
            if (resources is YooAssetResourceService yoo)
                return yoo;
            throw new InvalidOperationException(
                $"Resource provider '{resources.GetType().FullName}' is not YooAssetResourceService. " +
                "Mobile launch update pipeline requires Cascade.Service.YooAsset.");
        }

        private sealed class ProgressAdapter : IProgress<YooAssetDownloadProgress>
        {
            private readonly IProgress<LauncherDownloadProgress> _inner;

            public ProgressAdapter(IProgress<LauncherDownloadProgress> inner)
            {
                _inner = inner;
            }

            public void Report(YooAssetDownloadProgress value)
            {
                _inner?.Report(new LauncherDownloadProgress(
                    value.TotalCount,
                    value.CurrentCount,
                    value.TotalBytes,
                    value.CurrentBytes));
            }
        }
    }
}
