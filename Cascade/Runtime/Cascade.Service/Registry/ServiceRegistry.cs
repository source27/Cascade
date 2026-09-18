using System;
using System.Collections.Generic;

namespace Cascade.Service
{
    public sealed class ServiceRegistry : IServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly List<object> _registrationOrder = new List<object>();
        private bool _disposed;

        public void Register<TService>(TService instance) where TService : class
        {
            ThrowIfDisposed();
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var type = typeof(TService);
            if (_services.ContainsKey(type))
                throw new InvalidOperationException(
                    $"Service already registered: {type.FullName}. Use Replace to swap the implementation.");

            _services.Add(type, instance);
            _registrationOrder.Add(instance);
        }

        public void Replace<TService>(TService instance) where TService : class
        {
            ThrowIfDisposed();
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            var type = typeof(TService);
            if (!_services.TryGetValue(type, out var previous))
            {
                _services.Add(type, instance);
                _registrationOrder.Add(instance);
                return;
            }

            if (ReferenceEquals(previous, instance))
                return;

            // Take over the old slot: registration order drives reverse-order teardown,
            // so a swap must not move this contract in that order.
            // Reference-based search on purpose — UnityEngine.Object.Equals compares object identity.
            var slot = IndexOf(previous);
            if (slot >= 0)
                _registrationOrder[slot] = instance;
            else
                _registrationOrder.Add(instance);

            _services[type] = instance;
            (previous as IDisposable)?.Dispose();
        }

        public bool Remove<TService>() where TService : class
        {
            ThrowIfDisposed();

            var type = typeof(TService);
            if (!_services.TryGetValue(type, out var previous))
                return false;

            _services.Remove(type);
            var slot = IndexOf(previous);
            if (slot >= 0)
                _registrationOrder.RemoveAt(slot);

            (previous as IDisposable)?.Dispose();
            return true;
        }

        public TService Get<TService>() where TService : class
        {
            ThrowIfDisposed();
            if (!TryGet<TService>(out var service))
                throw new InvalidOperationException($"Service not registered: {typeof(TService).FullName}");
            return service;
        }

        public bool TryGet<TService>(out TService service) where TService : class
        {
            ThrowIfDisposed();
            if (_services.TryGetValue(typeof(TService), out var boxed) && boxed is TService typed)
            {
                service = typed;
                return true;
            }

            service = null;
            return false;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            for (var i = _registrationOrder.Count - 1; i >= 0; i--)
            {
                if (_registrationOrder[i] is IDisposable disposable)
                    disposable.Dispose();
            }

            _registrationOrder.Clear();
            _services.Clear();
        }

        private int IndexOf(object instance)
        {
            for (var i = 0; i < _registrationOrder.Count; i++)
            {
                if (ReferenceEquals(_registrationOrder[i], instance))
                    return i;
            }

            return -1;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceRegistry));
        }
    }
}
