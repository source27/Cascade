using System.Threading;
using Cascade.Core;
using Cascade.Generated;
using Cysharp.Threading.Tasks;

namespace CascadeExample
{
    /// <summary>
    /// Hot-update entry, invoked reflectively by the framework's CodeLoader
    /// (entry type / assembly name configured via BootstrapConfiguration defaults).
    /// </summary>
    public static class GameLogicEntry
    {
        public static async UniTask<string> Start(IGameHost host, CancellationToken cancellationToken = default)
        {
            var registry = new UIRegistry();
            UIRegistryGenerated.RegisterAll(registry);

            var ui = await host.CreateUISystemAsync(registry, "UIRoot", cancellationToken);
            ui.BindPageContext(new PageContext(host.Services, ui, host.UpdateLoop));

            await ui.OpenUI<HomePage>(new HomePage.Args(), cancellationToken);
            return "1.0.0";
        }

        public static void Stop()
        {
        }
    }
}
