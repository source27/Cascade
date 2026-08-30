using Cascade.Core;
using Cascade.Service;

namespace Cascade.Indie
{
    /// <summary>
    /// Game-side composition bag after Bootstrap: host services + live flow.
    /// Managers/settings go here as the project grows.
    /// </summary>
    public sealed class IndieGameContext
    {
        public IndieGameContext(IGameHost host)
        {
            Host = host;
            Log = host.Log;
            Resources = host.Resources;
            Localization = host.Localization;
        }

        public IGameHost Host { get; }
        public ILogService Log { get; }
        public IResourceService Resources { get; }
        public ILocalizationService Localization { get; }

        /// <summary>Set once when <see cref="GameFlow"/> is constructed.</summary>
        public GameFlow Flow { get; set; }
    }
}
