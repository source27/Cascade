using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;
using Cascade.Core;

namespace Cascade.Bootstrap
{
    internal sealed class GameHost : IGameHost
    {
        private IUISystem _ui;

        public GameHost(IServiceRegistry services, IUpdateLoop updateLoop)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            UpdateLoop = updateLoop ?? throw new ArgumentNullException(nameof(updateLoop));
            Log = Services.Get<ILogService>();
            Events = Services.Get<IEventBus>();
            Resources = Services.Get<IResourceService>();
            Services.TryGet<ILocalizationService>(out var localization);
            Localization = localization;
        }

        public IServiceRegistry Services { get; }
        public IUpdateLoop UpdateLoop { get; }
        public ILogService Log { get; }
        public IEventBus Events { get; }
        public IResourceService Resources { get; }
        public ILocalizationService Localization { get; }
        public IUISystem UI => _ui;

        public async UniTask<IUISystem> CreateUISystemAsync(
            UIRegistry registry,
            string rootAddress,
            CancellationToken cancellationToken = default)
        {
            if (_ui != null)
                throw new InvalidOperationException("UISystem already exists. Call DestroyUISystem before creating another.");

            var ui = await UISystem.CreateAsync(
                Resources,
                registry,
                rootAddress,
                UpdateLoop,
                Log,
                cancellationToken);
            _ui = ui;
            return ui;
        }

        public void DestroyUISystem()
        {
            if (_ui == null)
                return;
            _ui.Dispose();
            _ui = null;
        }
    }
}
