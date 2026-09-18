using System;
using Cascade.Core;
using NUnit.Framework;
using UnityEngine;

namespace Cascade.Tests
{
    /// <summary>
    /// The default loop hosts itself: creating it must produce its own DontDestroyOnLoad object, and
    /// disposing it must clear callbacks and destroy that object (batchmode/EditMode create no frames,
    /// so ticks are exercised through the core in <see cref="UpdateLoopTests"/>).
    /// </summary>
    public sealed class UnityUpdateLoopTests
    {
        [Test]
        public void CreateHostsItsOwnObjectAndRegistersCallbacks()
        {
            var loop = UnityUpdateLoop.Create();
            try
            {
                Assert.That(loop, Is.Not.Null);
                Assert.That(loop.gameObject, Is.Not.Null);
                Assert.That(loop.gameObject.name, Is.EqualTo(UnityUpdateLoop.HostObjectName));
                Assert.That(loop.gameObject.GetComponents<UnityUpdateLoop>().Length, Is.EqualTo(1));

                var token = loop.RegisterUpdate(() => { });
                Assert.That(token, Is.Not.Null);
                token.Dispose();
            }
            finally
            {
                loop.Dispose();
            }

            Assert.That(loop == null, Is.True, "Dispose must destroy the host object");
        }

        [Test]
        public void DisposeIsIdempotentAndRegistrationAfterwardsThrows()
        {
            var loop = UnityUpdateLoop.Create();
            loop.Dispose();
            Assert.That(() => loop.Dispose(), Throws.Nothing);
            Assert.That(() => loop.RegisterUpdate(() => { }), Throws.TypeOf<ObjectDisposedException>());
        }

        [Test]
        public void ManuallyAddedComponentWithoutCreateIsRejected()
        {
            var host = new GameObject("manual-loop");
            try
            {
                var loop = host.AddComponent<UnityUpdateLoop>();
                Assert.That(() => loop.RegisterUpdate(() => { }),
                    Throws.TypeOf<InvalidOperationException>(),
                    "a component that did not go through Create has no core loop");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
