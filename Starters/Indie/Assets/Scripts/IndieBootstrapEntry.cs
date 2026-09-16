using System.Threading;
using Cascade.Bootstrap;
using Cascade.Core;
using Cascade.Service;
using Cascade.Service.Addressables;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Cascade.Indie
{
    public sealed class IndieBootstrapEntry : BootstrapBase
    {
        protected override IResourceService CreateResourceService() => new AddressablesResourceService();

        protected override ResourceInitOptions CreateResourceInitOptions() =>
            new AddressablesResourceInitOptions(autoReleaseInitHandle: true);

        protected override async UniTask RunGameAsync(IGameHost host, CancellationToken cancellationToken)
        {
            await GameEntry.Start(host, cancellationToken);
        }
    }
}
