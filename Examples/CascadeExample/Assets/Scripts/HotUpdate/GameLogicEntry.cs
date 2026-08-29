using System.Threading;
using Cascade.Core;
using Cascade.Generated;
using CascadeExample;
using Cysharp.Threading.Tasks;

namespace GameLogic
{
    /// <summary>
    /// Hot-update entry, invoked reflectively by the framework's CodeLoader.
    /// Namespace/class intentionally match the framework default entry type
    /// (GameLogic.GameLogicEntry) so no configuration is needed.
    /// </summary>
    public static class GameLogicEntry
    {
        public static async UniTask<string> Start(IGameHost host, CancellationToken cancellationToken = default)
        {
            // 1) Gameplay scene (Single mode unloads the Bootstrap scene — launcher UI goes with it).
            await host.Resources.LoadSceneAsync("Gameplay", ResourceSceneLoadMode.Single, cancellationToken);

            // 2) UI system (DontDestroyOnLoad — survives the scene swap).
            var registry = new UIRegistry();
            UIRegistryGenerated.RegisterAll(registry);
            var ui = await host.CreateUISystemAsync(registry, "UIRoot", cancellationToken);
            ui.BindPageContext(new PageContext(host.Services, ui, host.UpdateLoop));

            // 3) Pages open under an active context (client's GameFlow does the same).
            ui.SetActiveContext(UIContextId.Main);
            await ui.OpenUI<HomePage>(new HomePage.Args(), cancellationToken);
            return "1.0.0";
        }

        public static void Stop()
        {
        }
    }
}
