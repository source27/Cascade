using System.Threading;
using Cascade.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Indie
{
    public sealed class MainFlowState : IGameFlowState
    {
        private readonly IndieGameContext _context;
        private GameObject _root;

        public MainFlowState(IndieGameContext context)
        {
            _context = context;
        }

        public string Id => IndieFlowIds.Main;

        public UniTask EnterAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _root = IndieSmokeUi.CreateCanvas("IndieFlow_Main");
            IndieSmokeUi.AddLabel(_root.transform, "Main flow\n(Cascade.Core.GameFlow)", anchorY: 0.55f);
            IndieSmokeUi.AddButton(_root.transform, "Enter Battle", () =>
            {
                _context.Flow.ChangeStateAsync(IndieFlowIds.Battle).Forget();
            }, anchorY: 0.35f);
            _context.Log.Info("Indie", "Main flow entered.");
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(CancellationToken cancellationToken = default)
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            _context.Log.Info("Indie", "Main flow exited.");
            return UniTask.CompletedTask;
        }
    }
}
