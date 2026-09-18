using System;
using Cascade.Core;
using Cascade.Service;

namespace Cascade.Bootstrap
{
    internal sealed class GameHost : IGameHost
    {
        public GameHost(IServiceRegistry services)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }

        public IServiceRegistry Services { get; }
    }
}
