using System;
using System.Collections.Generic;
using Cascade.Service;

namespace Cascade.Core
{
    public interface IEventBus
    {
        IDisposable Subscribe<TEvent>(Action<TEvent> handler);
        void Publish<TEvent>(TEvent value);
        int GetSubscriptionCount<TEvent>();
    }

    public sealed class EventBus : IEventBus, IDisposable
    {
        private readonly Dictionary<Type, List<IEventSubscription>> _subscriptions =
            new Dictionary<Type, List<IEventSubscription>>();
        private readonly ILogService _log;
        private bool _disposed;

        public EventBus(ILogService log = null)
        {
            _log = log;
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            ThrowIfDisposed();
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var type = typeof(TEvent);
            if (!_subscriptions.TryGetValue(type, out var list))
            {
                list = new List<IEventSubscription>();
                _subscriptions.Add(type, list);
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Handler.Equals(handler))
                    throw new InvalidOperationException($"Event handler already subscribed: {type.FullName}");
            }

            var subscription = new EventSubscription<TEvent>(this, type, handler);
            list.Add(subscription);
            return subscription;
        }

        public void Publish<TEvent>(TEvent value)
        {
            ThrowIfDisposed();
            if (!_subscriptions.TryGetValue(typeof(TEvent), out var list))
                return;

            var snapshot = list.ToArray();
            for (var i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i].Invoke(value);
                }
                catch (Exception exception)
                {
                    _log?.Exception("EventBus", exception, typeof(TEvent).FullName);
                }
            }
        }

        public int GetSubscriptionCount<TEvent>()
        {
            ThrowIfDisposed();
            return _subscriptions.TryGetValue(typeof(TEvent), out var list) ? list.Count : 0;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _subscriptions.Clear();
        }

        private void Remove(IEventSubscription subscription)
        {
            if (_disposed)
                return;
            if (_subscriptions.TryGetValue(subscription.EventType, out var list))
            {
                list.Remove(subscription);
                if (list.Count == 0)
                    _subscriptions.Remove(subscription.EventType);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EventBus));
        }

        private interface IEventSubscription
        {
            Type EventType { get; }
            Delegate Handler { get; }
            void Invoke(object value);
        }

        private sealed class EventSubscription<TEvent> : IEventSubscription, IDisposable
        {
            private readonly EventBus _owner;
            private readonly Action<TEvent> _handler;
            private bool _disposed;

            public EventSubscription(EventBus owner, Type eventType, Action<TEvent> handler)
            {
                _owner = owner;
                EventType = eventType;
                _handler = handler;
            }

            public Type EventType { get; }
            public Delegate Handler => _handler;

            public void Invoke(object value)
            {
                if (!_disposed)
                    _handler((TEvent)value);
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                _owner.Remove(this);
            }
        }
    }
}
