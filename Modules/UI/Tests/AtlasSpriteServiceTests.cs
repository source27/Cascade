using System;
using System.IO;
using System.Text;
using System.Threading;
using Cascade.Service;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Cascade.Modules.UI.Tests
{
    /// <summary>
    /// Atlas lookup contract: the mapping index is parsed and cached once, failures (missing index, missing
    /// atlas, sprite not in the index) degrade to <c>null</c> instead of throwing, and
    /// <see cref="AtlasSpriteService.Reset"/> drops that state without latching the failure.
    /// <para>
    /// Atlas handle release is not asserted here: EditMode cannot fabricate a <c>SpriteAtlas</c> instance
    /// (it is a native object, not a <c>ScriptableObject</c>), so that path needs a real packed atlas asset.
    /// </para>
    /// </summary>
    public sealed class AtlasSpriteServiceTests
    {
        [Test]
        public void ParseIndexReadsPairsAndSkipsIncompleteEntries()
        {
            var parsed = AtlasSpriteService.ParseIndex(BuildIndex(("icon", "ui_atlas"), ("empty", ""), ("", "ui_atlas")));

            Assert.That(parsed, Has.Count.EqualTo(1));
            Assert.That(parsed["icon"], Is.EqualTo("ui_atlas"));
        }

        [Test]
        public void ParseIndexRejectsNullBytes()
        {
            Assert.Throws<ArgumentNullException>(() => AtlasSpriteService.ParseIndex(null));
        }

        [Test]
        public void IndexResolvesButUnloadableAtlasDegradesToNull()
        {
            var resources = new FakeResourceService
            {
                IndexBytes = BuildIndex(("icon", "ui_atlas")),
                FailAssetLoads = true
            };
            using var service = new AtlasSpriteService(resources);

            Assert.That(service.LoadSpriteAsync("icon").GetAwaiter().GetResult(), Is.Null,
                "an unloadable atlas must degrade to null, not propagate");
            Assert.That(service.TryGetAtlasName("icon", out var atlas), Is.True, "the index itself did load");
            Assert.That(atlas, Is.EqualTo("ui_atlas"));
        }

        [Test]
        public void UnknownSpriteIsNotInTheIndex()
        {
            var resources = new FakeResourceService
            {
                IndexBytes = BuildIndex(("icon", "ui_atlas")),
                FailAssetLoads = true
            };
            using var service = new AtlasSpriteService(resources);

            Assert.That(service.LoadSpriteAsync("not_in_index").GetAwaiter().GetResult(), Is.Null);
            Assert.That(service.TryGetAtlasName("not_in_index", out _), Is.False);
        }

        [Test]
        public void MissingIndexIsSwallowedAndDoesNotLatch()
        {
            var resources = new FakeResourceService { FailIndexLoad = true };
            using var service = new AtlasSpriteService(resources);

            Assert.That(service.LoadSpriteAsync("icon").GetAwaiter().GetResult(), Is.Null);
            Assert.That(service.TryGetAtlasName("icon", out _), Is.False);

            // A failed attempt must not be cached as "loaded": the next call retries the index.
            resources.FailIndexLoad = false;
            resources.IndexBytes = BuildIndex(("icon", "ui_atlas"));
            Assert.That(service.LoadSpriteAsync("icon").GetAwaiter().GetResult(), Is.Null,
                "atlas asset is still missing, so the sprite stays null — but the index now resolves");
            Assert.That(service.TryGetAtlasName("icon", out _), Is.True);
            Assert.That(resources.IndexLoadCount, Is.EqualTo(2), "the index is fetched per attempt until it succeeds");
        }

        [Test]
        public void SuccessfulIndexIsLoadedOnlyOnce()
        {
            var resources = new FakeResourceService { IndexBytes = BuildIndex(("icon", "ui_atlas")), FailAssetLoads = true };
            using var service = new AtlasSpriteService(resources);

            service.LoadSpriteAsync("icon").GetAwaiter().GetResult();
            service.LoadSpriteAsync("icon").GetAwaiter().GetResult();

            Assert.That(resources.IndexLoadCount, Is.EqualTo(1), "the index is cached after a successful load");
        }

        [Test]
        public void ResetForgetsTheIndexSoTheNextLoadRefetches()
        {
            var resources = new FakeResourceService { IndexBytes = BuildIndex(("icon", "ui_atlas")), FailAssetLoads = true };
            using var service = new AtlasSpriteService(resources);

            service.LoadSpriteAsync("icon").GetAwaiter().GetResult();
            Assert.That(service.TryGetAtlasName("icon", out _), Is.True);

            service.Reset();

            Assert.That(service.TryGetAtlasName("icon", out _), Is.False, "Reset forgets the index");
            Assert.That(() => service.Reset(), Throws.Nothing, "Reset is idempotent");

            service.LoadSpriteAsync("icon").GetAwaiter().GetResult();
            Assert.That(resources.IndexLoadCount, Is.EqualTo(2), "Reset must force a refetch");
        }

        [Test]
        public void LoadAfterDisposeReturnsNullAndDisposeIsIdempotent()
        {
            var resources = new FakeResourceService { IndexBytes = BuildIndex(("icon", "ui_atlas")) };
            var service = new AtlasSpriteService(resources);

            service.Dispose();
            Assert.That(() => service.Dispose(), Throws.Nothing);
            Assert.That(service.LoadSpriteAsync("icon").GetAwaiter().GetResult(), Is.Null);
        }

        /// <summary>AtlasMapping.bytes format: int32 count, then count UTF-8 string pairs.</summary>
        private static byte[] BuildIndex(params (string Sprite, string Atlas)[] pairs)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(pairs.Length);
                foreach (var pair in pairs)
                {
                    writer.Write(pair.Sprite ?? string.Empty);
                    writer.Write(pair.Atlas ?? string.Empty);
                }
            }

            return stream.ToArray();
        }

        private sealed class FakeResourceService : IResourceService
        {
            public byte[] IndexBytes;
            public bool FailIndexLoad;
            public bool FailAssetLoads;
            public int IndexLoadCount;

            public bool IsInitialized => true;

            public UniTask InitializeAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;

            public UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default)
                where T : UnityEngine.Object
            {
                // No SpriteAtlas can be fabricated in EditMode; this path always fails and the service must
                // swallow it (see the class comment).
                throw new InvalidOperationException(FailAssetLoads
                    ? $"no such asset: {location}"
                    : $"unexpected asset type {typeof(T)} for {location}");
            }

            public UniTask<ISceneHandle> LoadSceneAsync(
                string location,
                ResourceSceneLoadMode loadMode = ResourceSceneLoadMode.Single,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken cancellationToken = default)
            {
                IndexLoadCount += 1;
                if (FailIndexLoad)
                    throw new InvalidOperationException($"no such mapping: {location}");
                return UniTask.FromResult(IndexBytes);
            }

            public void UnloadUnused()
            {
            }
        }
    }
}
