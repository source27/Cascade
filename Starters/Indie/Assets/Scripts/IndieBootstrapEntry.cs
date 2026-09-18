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
        [SerializeField] private BootstrapEnvironment environment = BootstrapEnvironment.Dev;

        public BootstrapEnvironment InspectorEnvironment => environment;

        protected override ILogService CreateLogService() => new UnityLogService
        {
            Enabled = true,
            MinimumLevel = ResolveMinimumLogLevel()
        };

        protected override IResourceService CreateResourceService() => new AddressablesResourceService();

        protected override ResourceInitOptions CreateResourceInitOptions() =>
            new AddressablesResourceInitOptions(autoReleaseInitHandle: true);

        protected override async UniTask RunGameAsync(IGameHost host, CancellationToken cancellationToken)
        {
            await GameEntry.Start(host, cancellationToken);
        }

        private BootstrapEnvironment ResolveEnvironment()
        {
#if UNITY_EDITOR
            return environment;
#else
            return BootstrapEnvironment.Gold;
#endif
        }

        private LogLevel ResolveMinimumLogLevel()
        {
            switch (ResolveEnvironment())
            {
                case BootstrapEnvironment.Dev:
                    return LogLevel.Trace;
                case BootstrapEnvironment.Beta:
                    return LogLevel.Debug;
                default:
                    return LogLevel.Info;
            }
        }
    }
}
