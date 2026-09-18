using System;

namespace Cascade.Service
{
    public interface IServiceRegistry : IDisposable
    {
        /// <summary>Registers a service instance. Registering the same contract twice throws — use <see cref="Replace{TService}"/> to swap an implementation.</summary>
        void Register<TService>(TService instance) where TService : class;

        /// <summary>
        /// Makes <paramref name="instance"/> the current implementation of
        /// <typeparamref name="TService"/>: registers it when the contract is absent, otherwise
        /// disposes the replaced instance (when <see cref="IDisposable"/>) and takes over its slot
        /// so registry teardown order stays stable. Same instance → no-op.
        /// Intended for the composition root, before other code resolved the service.
        /// </summary>
        void Replace<TService>(TService instance) where TService : class;

        /// <summary>
        /// Unregisters a service and disposes it (when <see cref="IDisposable"/>).
        /// Returns false when nothing was registered for the contract.
        /// </summary>
        bool Remove<TService>() where TService : class;

        TService Get<TService>() where TService : class;

        bool TryGet<TService>(out TService service) where TService : class;
    }
}
