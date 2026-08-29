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
                throw new InvalidOperationException($"Service already registered: {type.FullName}");

            _services.Add(type, instance);
            _registrationOrder.Add(instance);
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

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceRegistry));
        }
    }
}
