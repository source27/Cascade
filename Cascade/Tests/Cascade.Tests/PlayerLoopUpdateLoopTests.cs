using System;
using Cascade.Core;
using NUnit.Framework;
using UnityEngine.LowLevel;

namespace Cascade.Tests
{
    /// <summary>
    /// PlayerLoop-backed implementation: injecting adds exactly its three ticks to Unity's loop, removal
    /// takes them back out, and several instances stay independent (ownership is matched by delegate
    /// target, not by marker type). TearDown always disposes so the global loop is left untouched.
    /// </summary>
    public sealed class PlayerLoopUpdateLoopTests
    {
        private PlayerLoopUpdateLoop _loop;

        [TearDown]
        public void TearDown()
        {
            _loop?.Dispose();
            _loop = null;
        }

        [Test]
        public void InjectsThreeTicksAndRemovesThemOnDispose()
        {
            _loop = new PlayerLoopUpdateLoop();

            Assert.That(_loop.IsInjected, Is.True);
            Assert.That(CountOwnedBy(_loop), Is.EqualTo(3), "update/late/fixed ticks should be in the PlayerLoop");

            _loop.Dispose();
            Assert.That(_loop.IsInjected, Is.False);
            Assert.That(CountOwnedBy(_loop), Is.Zero, "nothing of ours may stay in the PlayerLoop");
            _loop = null;
        }

        [Test]
        public void InstancesDoNotRemoveEachOther()
        {
            _loop = new PlayerLoopUpdateLoop();
            var second = new PlayerLoopUpdateLoop();
            try
            {
                Assert.That(CountOwnedBy(_loop), Is.EqualTo(3));
                Assert.That(CountOwnedBy(second), Is.EqualTo(3));

                second.Dispose();

                Assert.That(CountOwnedBy(second), Is.Zero);
                Assert.That(CountOwnedBy(_loop), Is.EqualTo(3), "disposing one loop must not unhook another");
            }
            finally
            {
                second.Dispose();
            }
        }

        [Test]
        public void RegistrationAfterDisposeThrows()
        {
            _loop = new PlayerLoopUpdateLoop();
            _loop.Dispose();
            Assert.That(() => _loop.RegisterUpdate(() => { }), Throws.TypeOf<ObjectDisposedException>());
            _loop = null;
        }

        private static int CountOwnedBy(object owner)
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            var count = 0;
            Walk(ref loop, owner, ref count);
            return count;
        }

        private static void Walk(ref PlayerLoopSystem root, object owner, ref int count)
        {
            if (root.subSystemList == null)
                return;

            foreach (var child in root.subSystemList)
            {
                if (child.updateDelegate != null && ReferenceEquals(child.updateDelegate.Target, owner))
                    count += 1;

                var copy = child;
                Walk(ref copy, owner, ref count);
            }
        }

    }
}
