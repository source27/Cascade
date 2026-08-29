using NUnit.Framework;

namespace Cascade.Tests
{
    public sealed class BootstrapConfigurationTests
    {
        [Test]
        public void Defaults_AreFrameworkNeutral()
        {
            var configuration = new Cascade.Launcher.BootstrapConfiguration(
                Cascade.Launcher.BootstrapEnvironment.Dev,
                Cascade.Launcher.BootstrapPlayMode.Host,
                "1.0.0");

            Assert.That(configuration.HotUpdateDllLocation, Is.EqualTo(Cascade.Launcher.BootstrapConfiguration.DefaultHotUpdateDllLocation));
            Assert.That(configuration.HotUpdateAssemblyName, Is.EqualTo(Cascade.Launcher.BootstrapConfiguration.DefaultHotUpdateAssemblyName));
            Assert.That(configuration.GameLogicEntryType, Is.EqualTo(Cascade.Launcher.BootstrapConfiguration.DefaultGameLogicEntryType));
            Assert.That(configuration.ResourceInitOptions, Is.Null);
        }

        [Test]
        public void CustomHotUpdateSettings_AreRespected()
        {
            var configuration = new Cascade.Launcher.BootstrapConfiguration(
                Cascade.Launcher.BootstrapEnvironment.Dev,
                Cascade.Launcher.BootstrapPlayMode.Offline,
                "1.0.0",
                "HotUpdate.dll",
                "HotUpdate",
                "CascadeExample.GameEntry");

            Assert.That(configuration.HotUpdateDllLocation, Is.EqualTo("HotUpdate.dll"));
            Assert.That(configuration.HotUpdateAssemblyName, Is.EqualTo("HotUpdate"));
            Assert.That(configuration.GameLogicEntryType, Is.EqualTo("CascadeExample.GameEntry"));
        }

        [Test]
        public void AssemblyLoadMode_FollowsPlayMode()
        {
            Assert.That(
                Cascade.Launcher.BootstrapConfiguration.ResolveAssemblyLoadMode(Cascade.Launcher.BootstrapPlayMode.EditorSimulate),
                Is.EqualTo(Cascade.Launcher.BootstrapAssemblyLoadMode.EditorLoaded));
            Assert.That(
                Cascade.Launcher.BootstrapConfiguration.ResolveAssemblyLoadMode(Cascade.Launcher.BootstrapPlayMode.Host),
                Is.EqualTo(Cascade.Launcher.BootstrapAssemblyLoadMode.RawFile));
            Assert.That(
                Cascade.Launcher.BootstrapConfiguration.ResolveAssemblyLoadMode(Cascade.Launcher.BootstrapPlayMode.Offline),
                Is.EqualTo(Cascade.Launcher.BootstrapAssemblyLoadMode.RawFile));
        }

        [Test]
        public void AotMetadataCatalog_NormalizesModuleNameToLocation()
        {
            Assert.That(Cascade.Launcher.AotMetadataCatalog.NormalizeLocation("mscorlib.dll"), Is.EqualTo("mscorlib.dll"));
            Assert.That(Cascade.Launcher.AotMetadataCatalog.NormalizeLocation("path/to/System.Core.dll"), Is.EqualTo("System.Core.dll"));
            Assert.That(Cascade.Launcher.AotMetadataCatalog.ResolveLocations(), Is.Not.Null);
        }
    }
}
