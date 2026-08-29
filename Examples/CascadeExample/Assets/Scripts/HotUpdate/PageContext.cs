using Cascade.Core;
using Cascade.Service;

namespace CascadeExample
{
    /// <summary>
    /// Consumer-side page context: how pages reach framework facilities.
    /// Created by <see cref="GameLogicEntry"/> and bound via
    /// <see cref="IUISystem.BindPageContext"/>.
    /// </summary>
    public sealed class PageContext
    {
        public PageContext(IServiceRegistry services, IUISystem ui, IUpdateLoop updateLoop)
        {
            Services = services;
            UI = ui;
            UpdateLoop = updateLoop;
        }

        public IServiceRegistry Services { get; }
        public IUISystem UI { get; }
        public IUpdateLoop UpdateLoop { get; }
    }
}
