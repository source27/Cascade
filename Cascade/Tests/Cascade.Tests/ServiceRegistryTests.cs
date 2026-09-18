using System;
using System.Collections.Generic;
using Cascade.Service;
using NUnit.Framework;

namespace Cascade.Tests
{
    /// <summary>
    /// Registry lifecycle contract: duplicate registration still throws, and Replace/Remove hand the
    /// replaced instance over to <see cref="IDisposable"/> instead of silently leaking it.
    /// </summary>
    public sealed class ServiceRegistryTests
    {
        [Test]
        public void RegisterDuplicateThrows()
        {
            using var registry = new ServiceRegistry();
            registry.Register<IFake>(new Fake("first"));

            var error = Assert.Throws<InvalidOperationException>(() => registry.Register<IFake>(new Fake("second")));
            Assert.That(error.Message, Does.Contain(nameof(IFake)));
        }

        [Test]
        public void ReplaceSwapsAndDisposesPrevious()
        {
            var disposed = new List<string>();
            var registry = new ServiceRegistry();
            var first = new Fake("first", disposed);
            var second = new Fake("second", disposed);

            registry.Register<IFake>(first);
            registry.Replace<IFake>(second);

            Assert.That(registry.Get<IFake>(), Is.SameAs(second));
            Assert.That(disposed, Is.EqualTo(new[] { "first" }));

            registry.Dispose();
            Assert.That(disposed, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void ReplaceTakesOverTheTeardownSlot()
        {
            var disposed = new List<string>();
            var registry = new ServiceRegistry();

            registry.Register<IFake>(new Fake("first", disposed));
            registry.Register<ISaveLike>(new Fake("second", disposed));
            registry.Replace<IFake>(new Fake("replacement", disposed));

            Assert.That(disposed, Is.EqualTo(new[] { "first" }),
                "Replace must dispose the replaced instance immediately");

            registry.Dispose();

            // Teardown is reverse registration order; the replacement kept slot 0, so it goes last.
            Assert.That(disposed, Is.EqualTo(new[] { "first", "second", "replacement" }));
        }

        [Test]
        public void ReplaceWithSameInstanceIsNoOp()
        {
            var disposed = new List<string>();
            var registry = new ServiceRegistry();
            var service = new Fake("only", disposed);

            registry.Register<IFake>(service);
            registry.Replace<IFake>(service);

            Assert.That(registry.Get<IFake>(), Is.SameAs(service));
            Assert.That(disposed, Is.Empty);
        }

        [Test]
        public void ReplaceWhenAbsentRegisters()
        {
            using var registry = new ServiceRegistry();
            var service = new Fake("late");

            registry.Replace<IFake>(service);

            Assert.That(registry.Get<IFake>(), Is.SameAs(service));
        }

        [Test]
        public void RemoveDisposesAndUnregisters()
        {
            var disposed = new List<string>();
            using var registry = new ServiceRegistry();
            registry.Register<IFake>(new Fake("gone", disposed));

            Assert.That(registry.Remove<IFake>(), Is.True);
            Assert.That(disposed, Is.EqualTo(new[] { "gone" }));
            Assert.That(registry.TryGet<IFake>(out _), Is.False);
            Assert.That(registry.Remove<IFake>(), Is.False);

            registry.Dispose();
            Assert.That(disposed, Is.EqualTo(new[] { "gone" }));
        }

        [Test]
        public void NullReplaceThrows()
        {
            using var registry = new ServiceRegistry();
            Assert.Throws<ArgumentNullException>(() => registry.Replace<IFake>(null));
        }

        private interface IFake
        {
        }

        private interface ISaveLike
        {
        }

        private sealed class Fake : IFake, ISaveLike, IDisposable
        {
            private readonly List<string> _disposed;
            private readonly string _name;

            public Fake(string name, List<string> disposed = null)
            {
                _name = name;
                _disposed = disposed;
            }

            public void Dispose() => _disposed?.Add(_name);
        }

    }
}
