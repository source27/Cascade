using System.Threading;
using Cysharp.Threading.Tasks;

namespace Cascade.Core
{
    /// <summary>
    /// One game-owned flow step (Main, Battle, …). Ids are game strings; Core does not ship a closed enum.
    /// Enter/Exit own that step's root UI and local systems.
    /// </summary>
    public interface IGameFlowState
    {
        string Id { get; }

        UniTask EnterAsync(CancellationToken cancellationToken = default);

        UniTask ExitAsync(CancellationToken cancellationToken = default);
    }
}
