using Cascade.Core;
using Cascade.Service;

namespace Cascade.Indie
{
    /// <summary>
    /// Game-side composition bag after Bootstrap: services pulled from the registry + live flow.
    /// Managers/settings go here as the project grows.
    /// </summary>
    public sealed class IndieGameContext
    {
        public IndieGameContext(IServiceRegistry services)
        {
            Services = services;
            Log = services.Get<ILogService>();
            Resources = services.Get<IResourceService>();
            services.TryGet<ILocalizationService>(out var localization);
            Localization = localization;
        }

        public IServiceRegistry Services { get; }
        public ILogService Log { get; }
        public IResourceService Resources { get; }

        /// <summary>Null when the project does not install the localization module.</summary>
        public ILocalizationService Localization { get; }

        /// <summary>Set once when <see cref="GameFlow"/> is constructed.</summary>
        public GameFlow Flow { get; set; }
    }
}
