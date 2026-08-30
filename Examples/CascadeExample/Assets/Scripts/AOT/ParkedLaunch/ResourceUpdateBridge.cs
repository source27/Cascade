using System;
using System.Reflection;
using System.Threading;
using Cascade.Service;
using Cysharp.Threading.Tasks;

namespace Cascade.Launcher
{
    /// <summary>
    /// Invokes provider-specific resource update APIs without a compile-time dependency
    /// on any integration package. Removed when Mobile Starter owns the update pipeline.
    /// </summary>
    internal static class ResourceUpdateBridge
    {
        public static bool IsUsingLocalVersion(IResourceService resources)
        {
            try
            {
                return (bool)((dynamic)resources).IsUsingLocalVersion;
            }
            catch
            {
                return false;
            }
        }

        public static string ActivePackageVersion(IResourceService resources)
        {
            try
            {
                return (string)((dynamic)resources).ActivePackageVersion ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static async UniTask<string> RequestVersionAsync(
            IResourceService resources,
            CancellationToken cancellationToken)
        {
            EnsureUpdater(resources);
            return await ((dynamic)resources).RequestVersionAsync(cancellationToken);
        }

        public static async UniTask UpdateManifestAsync(
            IResourceService resources,
            string version,
            CancellationToken cancellationToken)
        {
            EnsureUpdater(resources);
            await ((dynamic)resources).UpdateManifestAsync(version, cancellationToken);
        }

        public static LauncherDownloadPlan PrepareDownload(IResourceService resources)
        {
            EnsureUpdater(resources);
            dynamic plan = ((dynamic)resources).PrepareDownload();
            int totalCount = (int)plan.TotalCount;
            long totalBytes = (long)plan.TotalBytes;
            return new LauncherDownloadPlan(totalCount, totalBytes);
        }

        public static async UniTask DownloadAsync(
            IResourceService resources,
            IProgress<LauncherDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            EnsureUpdater(resources);
            object providerProgress = null;
            if (progress != null)
            {
                var download = FindDownloadMethod(resources.GetType())
                    ?? throw Missing(resources, "DownloadAsync");
                var progressParam = download.GetParameters()[0].ParameterType;
                if (progressParam.IsGenericType)
                {
                    var dtoType = progressParam.GetGenericArguments()[0];
                    var adapterType = typeof(TypedProgressAdapter<>).MakeGenericType(dtoType);
                    providerProgress = Activator.CreateInstance(adapterType, progress);
                }
            }

            await ((dynamic)resources).DownloadAsync((dynamic)providerProgress, cancellationToken);
        }

        public static async UniTask ClearUnusedCacheAsync(
            IResourceService resources,
            CancellationToken cancellationToken)
        {
            EnsureUpdater(resources);
            await ((dynamic)resources).ClearUnusedCacheAsync(cancellationToken);
        }

        private static MethodInfo FindDownloadMethod(Type type)
        {
            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "DownloadAsync")
                    continue;
                if (method.GetParameters().Length == 2)
                    return method;
            }

            return null;
        }

        private static void EnsureUpdater(IResourceService resources)
        {
            if (resources == null)
                throw new ArgumentNullException(nameof(resources));
            if (resources.GetType().GetMethod("RequestVersionAsync") == null)
                throw Missing(resources, "RequestVersionAsync");
        }

        private static Exception Missing(IResourceService resources, string member)
        {
            return new InvalidOperationException(
                $"Resource provider '{resources.GetType().FullName}' does not expose update API '{member}'. " +
                "Use YooAssetResourceService (or Mobile Starter pipeline) for resource hot-update.");
        }

        private sealed class TypedProgressAdapter<TProvider> : IProgress<TProvider>
        {
            private readonly IProgress<LauncherDownloadProgress> _inner;

            public TypedProgressAdapter(IProgress<LauncherDownloadProgress> inner)
            {
                _inner = inner;
            }

            public void Report(TProvider value)
            {
                if (_inner == null || value == null)
                    return;
                dynamic v = value;
                _inner.Report(new LauncherDownloadProgress(
                    (int)v.TotalCount,
                    (int)v.CurrentCount,
                    (long)v.TotalBytes,
                    (long)v.CurrentBytes));
            }
        }
    }
}
