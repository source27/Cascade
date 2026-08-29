using Cascade.Launcher;
using Cascade.Service;

namespace CascadeExample
{
    /// <summary>
    /// Composition root for the example. The framework's <see cref="BootstrapEntry"/>
    /// drives the launch flow; this subclass only supplies the resource provider.
    /// No YooAsset/Addressables: the demo service serves prefabs/clips from Resources
    /// and embedded localization tables (see <see cref="DemoResourceService"/>).
    /// </summary>
    public sealed class ExampleBootstrapEntry : BootstrapEntry
    {
        protected override IResourceService CreateResourceService() => new DemoResourceService();
    }
}
