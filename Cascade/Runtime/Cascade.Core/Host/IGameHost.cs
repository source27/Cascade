using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;

namespace Cascade.Core
{
    public interface IGameHost
    {
        IServiceRegistry Services { get; }
        IUpdateLoop UpdateLoop { get; }
        ILogService Log { get; }
        IEventBus Events { get; }
        IResourceService Resources { get; }
        /// <summary>Cascade localization when registered; null if the game uses another stack.</summary>
        ILocalizationService Localization { get; }
        IUISystem UI { get; }
        UniTask<IUISystem> CreateUISystemAsync(
            UIRegistry registry,
            string rootAddress,
            CancellationToken cancellationToken = default);
        void DestroyUISystem();
    }
}
