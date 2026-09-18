using System;
using System.Threading;
using Cascade.Service;
using Cysharp.Threading.Tasks;

namespace Cascade.Modules.Localization
{
    /// <summary>
    /// Composition-root helper for the default resource-backed localization stack:
    /// build from registered services, initialize (catalog + locale tables), register the live instance.
    /// The registry disposes it on shutdown, which also unbinds <see cref="LocalizationAccess"/>.
    /// Call once per registry.
    /// </summary>
    public static class LocalizationInstaller
    {
        public static async UniTask<ILocalizationService> InstallAsync(
            IServiceRegistry services,
            CancellationToken cancellationToken = default,
            string catalogLocation = LocalizationService.DefaultCatalogLocation)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            var service = new LocalizationService(
                services.Get<IResourceService>(),
                services.Get<ISaveService>(),
                services.Get<ILogService>(),
                catalogLocation);
            await service.InitializeAsync(cancellationToken);
            services.Register<ILocalizationService>(service);
            return service;
        }
    }
}
