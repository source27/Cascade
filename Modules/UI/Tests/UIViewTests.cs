using System;
using Cascade.Service;
using Cascade.Modules.UI;
using NUnit.Framework;
using UnityEngine;

namespace Cascade.Modules.UI.Tests
{
    public sealed class UIViewTests
    {
        [Test]
        public void AssetScope_ClearReleasesTrackedHandles_AndCanBeReused()
        {
            var scope = new UIViewAssetScope();
            var first = new TestAssetHandle();
            var second = new TestAssetHandle();

            scope.Track(first);
            scope.Clear();

            Assert.That(first.ReleaseCount, Is.EqualTo(1));

            scope.Track(second);
            scope.Dispose();
            scope.Dispose();

            Assert.That(second.ReleaseCount, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => scope.Track(new TestAssetHandle()));
        }

        [Test]
        public void AssetScope_ReplaceReleasesPreviousHandle_AndDoesNotDoubleRelease()
        {
            var scope = new UIViewAssetScope();
            IAssetHandle<Sprite> current = null;
            var first = new TestAssetHandle();
            var second = new TestAssetHandle();

            scope.Replace(ref current, first);
            scope.Replace(ref current, second);
            scope.Dispose();

            Assert.That(first.ReleaseCount, Is.EqualTo(1));
            Assert.That(second.ReleaseCount, Is.EqualTo(1));
        }

        private sealed class TestAssetHandle : IAssetHandle<Sprite>
        {
            public Sprite Asset => null;
            public bool IsValid => ReleaseCount == 0;
            public int ReleaseCount { get; private set; }

            public void Release()
            {
                if (ReleaseCount == 0)
                    ReleaseCount++;
            }

            public void Dispose() => Release();
        }
    }
}
