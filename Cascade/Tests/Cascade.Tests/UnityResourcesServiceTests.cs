using System;
using Cascade.Service;
using NUnit.Framework;
using UnityEngine;

namespace Cascade.Tests
{
    /// <summary>
    /// Default provider contract: init gating plus the error text callers rely on
    /// (a missing location must name itself, not fail silently with null).
    /// </summary>
    public sealed class UnityResourcesServiceTests
    {
        [Test]
        public void LoadsBeforeInitializeThrow()
        {
            using var service = new UnityResourcesService();

            Assert.Throws<InvalidOperationException>(
                () => service.LoadAssetAsync<GameObject>("whatever").GetAwaiter().GetResult());
            Assert.Throws<InvalidOperationException>(
                () => service.LoadRawBytesAsync("whatever").GetAwaiter().GetResult());
        }

        [Test]
        public void InitializeIsOptionlessAndDisposeResetsState()
        {
            using (var service = new UnityResourcesService())
            {
                Assert.That(service.IsInitialized, Is.False);
                service.InitializeAsync(null).GetAwaiter().GetResult();
                Assert.That(service.IsInitialized, Is.True);
            }

            var disposed = new UnityResourcesService();
            disposed.Dispose();
            Assert.That(disposed.IsInitialized, Is.False);
            Assert.Throws<ObjectDisposedException>(
                () => disposed.InitializeAsync(new ResourceInitOptions()).GetAwaiter().GetResult());
        }

        [Test]
        public void EmptyLocationIsRejected()
        {
            using var service = new UnityResourcesService();
            service.InitializeAsync(new ResourceInitOptions()).GetAwaiter().GetResult();

            Assert.Throws<ArgumentException>(
                () => service.LoadAssetAsync<GameObject>("  ").GetAwaiter().GetResult());
            Assert.Throws<ArgumentException>(
                () => service.LoadRawBytesAsync(string.Empty).GetAwaiter().GetResult());
        }

        [Test]
        public void MissingAssetErrorNamesLocationAndType()
        {
            using var service = new UnityResourcesService();
            service.InitializeAsync(new ResourceInitOptions()).GetAwaiter().GetResult();

            var assetError = Assert.Throws<InvalidOperationException>(
                () => service.LoadAssetAsync<GameObject>("no_such_asset").GetAwaiter().GetResult());
            Assert.That(assetError.Message, Does.Contain("no_such_asset"));
            Assert.That(assetError.Message, Does.Contain(nameof(GameObject)));

            var bytesError = Assert.Throws<InvalidOperationException>(
                () => service.LoadRawBytesAsync("no_such_bytes").GetAwaiter().GetResult());
            Assert.That(bytesError.Message, Does.Contain("no_such_bytes"));
        }
    }
}
