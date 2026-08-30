using System;
using System.Linq;
using System.Threading;
using Cascade.Service;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Cascade.Tests
{
    public sealed class ResourceServiceContractTests
    {
        [Test]
        public void IResourceService_PublicSurface_IsLoadOnly()
        {
            var names = typeof(IResourceService).GetMethods()
                .Select(m => m.Name)
                .ToArray();

            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "get_IsInitialized",
                    "InitializeAsync",
                    "LoadAssetAsync",
                    "LoadSceneAsync",
                    "LoadRawBytesAsync",
                    "UnloadUnused",
                },
                names);

            CollectionAssert.DoesNotContain(names, "RequestVersionAsync");
            CollectionAssert.DoesNotContain(names, "UpdateManifestAsync");
            CollectionAssert.DoesNotContain(names, "PrepareDownload");
            CollectionAssert.DoesNotContain(names, "DownloadAsync");
            CollectionAssert.DoesNotContain(names, "ClearUnusedCacheAsync");
            CollectionAssert.DoesNotContain(names, "get_ActivePackageVersion");
            CollectionAssert.DoesNotContain(names, "get_IsUsingLocalVersion");
        }

        [Test]
        public void CascadeService_DoesNotDefine_ResourceDownloadDtos()
        {
            var serviceAssembly = typeof(IResourceService).Assembly;
            Assert.That(serviceAssembly.GetType("Cascade.Service.ResourceDownloadProgress"), Is.Null);
            Assert.That(serviceAssembly.GetType("Cascade.Service.ResourceDownloadPlan"), Is.Null);
        }

        [Test]
        public void LoadOnlyFake_CanInitializeAndLoadRawBytes()
        {
            var resources = new LoadOnlyFake();
            resources.InitializeAsync(new ResourceInitOptions()).GetAwaiter().GetResult();
            Assert.That(resources.IsInitialized, Is.True);

            var bytes = resources.LoadRawBytesAsync("x").GetAwaiter().GetResult();
            Assert.That(bytes, Is.EqualTo(new byte[] { 1, 2, 3 }));
        }

        private sealed class LoadOnlyFake : IResourceService
        {
            public bool IsInitialized { get; private set; }

            public UniTask InitializeAsync(ResourceInitOptions options, CancellationToken cancellationToken = default)
            {
                IsInitialized = true;
                return UniTask.CompletedTask;
            }

            public UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
                => UniTask.FromException<IAssetHandle<T>>(new NotSupportedException());

            public UniTask<ISceneHandle> LoadSceneAsync(
                string location,
                ResourceSceneLoadMode loadMode = ResourceSceneLoadMode.Single,
                CancellationToken cancellationToken = default)
                => UniTask.FromException<ISceneHandle>(new NotSupportedException());

            public UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken cancellationToken = default)
                => UniTask.FromResult(new byte[] { 1, 2, 3 });

            public void UnloadUnused()
            {
            }
        }
    }
}
