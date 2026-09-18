using Cascade.Service;

namespace Cascade.Core
{
    /// <summary>
    /// Bootstrap handle: a service registry and nothing else.
    /// Resolve facilities through <see cref="Services"/>; the game stores what it needs
    /// in its own context. Do not grow this into a service directory (ADR 0011/0022).
    /// </summary>
    public interface IGameHost
    {
        IServiceRegistry Services { get; }
    }
}
