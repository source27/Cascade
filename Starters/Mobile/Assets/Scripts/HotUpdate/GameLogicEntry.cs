using System.Threading;
using Cascade.Core;
using Cascade.Generated;
using Cascade.Modules.UI;
using Cascade.Service;
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
        private static IUISystem _ui;

        public static async UniTask<string> Start(IGameHost host, CancellationToken cancellationToken = default)
        {
            var services = host.Services;

            // 1) Gameplay scene (Single mode unloads the Bootstrap scene — launcher UI goes with it).
            await services.Get<IResourceService>()
                .LoadSceneAsync("Gameplay", ResourceSceneLoadMode.Single, cancellationToken);

            // 2) UI system (DontDestroyOnLoad — survives the scene swap). The game owns the instance.
            var registry = new UIRegistry();
            UIRegistryGenerated.RegisterAll(registry);
            _ui = await UISystem.CreateAsync(services, registry, "UIRoot", cancellationToken);
            _ui.BindPageContext(new PageContext(services, _ui, services.Get<IUpdateLoop>()));

            // 3) Pages open under an active context (client's GameFlow does the same).
            _ui.SetActiveContext(UIContextId.Main);
            await _ui.OpenUI<HomePage>(new HomePage.Args(), cancellationToken);
            return "1.0.0";
        }

        /// <summary>Invoked by CodeLoader before a retry/unload; releases the game-owned UI system.</summary>
        public static void Stop()
        {
            _ui?.Dispose();
            _ui = null;
        }
    }
}
