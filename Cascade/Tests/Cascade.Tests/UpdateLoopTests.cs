using System;
using Cascade.Core;
using NUnit.Framework;

namespace Cascade.Tests
{
    /// <summary>
    /// Contract shape: the returned token is the unregister handle (works for lambda captures too),
    /// loop disposal clears every phase, and one throwing callback does not stop the others.
    /// </summary>
    public sealed class UpdateLoopTests
    {
        [Test]
        public void CallbacksFirePerPhase()
        {
            var loop = new UpdateLoop();
            var fired = 0;
            loop.RegisterUpdate(() => fired += 1);
            loop.RegisterLateUpdate(() => fired += 10);
            loop.RegisterFixedUpdate(() => fired += 100);

            loop.TickUpdate();
            Assert.That(fired, Is.EqualTo(1));
            loop.TickLateUpdate();
            Assert.That(fired, Is.EqualTo(11));
            loop.TickFixedUpdate();
            Assert.That(fired, Is.EqualTo(111));
        }

        [Test]
        public void DisposingTokenUnregistersThatCallbackOnly()
        {
            var loop = new UpdateLoop();
            var kept = 0;
            var dropped = 0;
            var keptToken = loop.RegisterUpdate(() => kept += 1);
            var droppedToken = loop.RegisterUpdate(() => dropped += 1);

            loop.TickUpdate();
            droppedToken.Dispose();
            loop.TickUpdate();

            Assert.That(kept, Is.EqualTo(2), "the other callback must keep firing");
            Assert.That(dropped, Is.EqualTo(1), "disposing the token must unregister exactly that closure");
            keptToken.Dispose();
        }

        [Test]
        public void LoopDisposeClearsEveryPhase()
        {
            var loop = new UpdateLoop();
            var fired = 0;
            loop.RegisterUpdate(() => fired += 1);
            loop.RegisterLateUpdate(() => fired += 1);
            loop.RegisterFixedUpdate(() => fired += 1);

            loop.Dispose();
            loop.TickUpdate();
            loop.TickLateUpdate();
            loop.TickFixedUpdate();

            Assert.That(fired, Is.Zero);
        }

        [Test]
        public void ThrowingCallbackDoesNotStopTheRest()
        {
            var loop = new UpdateLoop();
            var reached = false;
            loop.RegisterUpdate(() => throw new InvalidOperationException("boom"));
            loop.RegisterUpdate(() => reached = true);

            Assert.That(() => loop.TickUpdate(), Throws.Nothing);
            Assert.That(reached, Is.True);
        }

        [Test]
        public void NullCallbackIsRejected()
        {
            var loop = new UpdateLoop();
            Assert.Throws<ArgumentNullException>(() => loop.RegisterUpdate(null));
        }
    }
}
