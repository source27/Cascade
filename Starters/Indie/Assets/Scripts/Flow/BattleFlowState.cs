using System.Threading;
using Cascade.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Indie
{
    public sealed class BattleFlowState : IGameFlowState
    {
        private readonly IndieGameContext _context;
        private GameObject _root;

        public BattleFlowState(IndieGameContext context)
        {
            _context = context;
        }

        public string Id => IndieFlowIds.Battle;

        public UniTask EnterAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _root = IndieSmokeUi.CreateCanvas("IndieFlow_Battle");
            IndieSmokeUi.AddLabel(_root.transform, "Battle flow\n(own UI root)", anchorY: 0.55f);
            IndieSmokeUi.AddButton(_root.transform, "Return", () =>
            {
                _context.Flow.ReturnAsync().Forget();
            }, anchorY: 0.35f);
            _context.Log.Info("Indie", "Battle flow entered.");
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CancellationToken cancellationToken = default)
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            _context.Log.Info("Indie", "Battle flow exited.");
            return UniTask.CompletedTask;
        }
    }
}
