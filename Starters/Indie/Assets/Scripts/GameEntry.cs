using System.Threading;
using Cascade.Core;
using Cascade.Modules.Localization;
using Cascade.Service;
using Cysharp.Threading.Tasks;

namespace Cascade.Indie
{
    /// <summary>
    /// Game logic entry after Bootstrap: localization install → context/managers → <see cref="GameFlow.RunAsync"/>.
    /// </summary>
    public static class GameEntry
    {
        public static async UniTask Start(IGameHost host, CancellationToken cancellationToken = default)
        {
            var services = host.Services;
            var log = services.Get<ILogService>();

            var localization = await LocalizationInstaller.InstallAsync(services, cancellationToken);
            log.Info("Indie", $"Localization ready: {localization.CurrentLocale}");

            var context = new IndieGameContext(services);
            // settings / tables / managers: init here as the project grows

            var flow = new GameFlow(
                new IGameFlowState[]
                {
                    new MainFlowState(context),
                    new BattleFlowState(context),
                },
                log);
            context.Flow = flow;
            services.Register<IGameFlowQuery>(flow);

            await flow.RunAsync(IndieFlowIds.Main, cancellationToken);
            log.Info("Indie", $"GameEntry ready. flow={flow.CurrentStateId}");
        }
    }
}
