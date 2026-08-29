using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;
using NUnit.Framework;
using UnityEngine;

namespace Cascade.Tests
{
    public sealed class AudioServiceTests
    {
        [Test]
        public void Dispose_AfterUnityDestroyedRoot_DoesNotThrow()
        {
            var service = new AudioService(new NullResourceService());
            var rootField = typeof(AudioService).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(rootField, Is.Not.Null);

            var root = (GameObject)rootField.GetValue(service);
            Object.DestroyImmediate(root);

            Assert.DoesNotThrow(service.Dispose);
        }

        /// <summary>
        /// Provider-neutral stand-in so the test does not depend on any resource
        /// integration package (decoupled resource layer).
        /// </summary>
        private sealed class NullResourceService : IResourceService
        {
            public bool IsInitialized => false;
            public string ActivePackageVersion => string.Empty;
            public bool IsUsingLocalVersion => true;

            public UniTask InitializeAsync(ResourceInitOptions options, CancellationToken cancellationToken = default)
                => UniTask.CompletedTask;
            public UniTask<string> RequestVersionAsync(CancellationToken cancellationToken = default)
                => UniTask.FromResult(string.Empty);
            public UniTask UpdateManifestAsync(string version, CancellationToken cancellationToken = default)
                => UniTask.CompletedTask;
            public ResourceDownloadPlan PrepareDownload() => new ResourceDownloadPlan(0, 0);
            public UniTask DownloadAsync(IProgress<ResourceDownloadProgress> progress = null, CancellationToken cancellationToken = default)
                => UniTask.CompletedTask;
            public UniTask ClearUnusedCacheAsync(CancellationToken cancellationToken = default)
                => UniTask.CompletedTask;
            public UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
                => throw new NotSupportedException();
            public UniTask<ISceneHandle> LoadSceneAsync(
                string location,
                ResourceSceneLoadMode loadMode = ResourceSceneLoadMode.Single,
                CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
            public UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
            public void UnloadUnused()
            {
            }
        }
    }
}
