using System;

namespace Cascade.Service
{
    public interface IServiceRegistry : IDisposable
    {
        void Register<TService>(TService instance) where TService : class;
        TService Get<TService>() where TService : class;
        bool TryGet<TService>(out TService service) where TService : class;
    }
}
