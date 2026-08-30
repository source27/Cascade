using System.Threading;
using Cascade.Core;
using Cysharp.Threading.Tasks;

namespace Cascade.Indie
{
    /// <summary>
    /// Game logic entry after Bootstrap: smoke resource check → context/managers → <see cref="GameFlow.RunAsync"/>.
    /// </summary>
    public static class GameEntry
    {
        public static async UniTask Start(IGameHost host, CancellationToken cancellationToken = default)
        {
            var bytes = await host.Resources.LoadRawBytesAsync("localization_catalog", cancellationToken);
            host.Log.Info("Indie", $"Loaded localization_catalog ({bytes.Length} bytes).");

            var context = new IndieGameContext(host);
            // settings / tables / managers: init here as the project grows

            var flow = new GameFlow(
                new IGameFlowState[]
                {
                    new MainFlowState(context),
                    new BattleFlowState(context),
                },
                host.Log);
            context.Flow = flow;
            host.Services.Register<IGameFlowQuery>(flow);

            await flow.RunAsync(IndieFlowIds.Main, cancellationToken);
            host.Log.Info("Indie", $"GameEntry ready. flow={flow.CurrentStateId}");
        }
    }
}
