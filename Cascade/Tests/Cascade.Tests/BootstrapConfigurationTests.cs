using Cascade.Bootstrap;
using Cascade.Service;
using NUnit.Framework;

namespace Cascade.Tests
{
    public sealed class BootstrapConfigurationTests
    {
        [Test]
        public void Configuration_IsSlim_WithoutHotUpdateFields()
        {
            var configuration = new BootstrapConfiguration(BootstrapEnvironment.Dev, "1.0.0");
            Assert.That(configuration.Environment, Is.EqualTo(BootstrapEnvironment.Dev));
            Assert.That(configuration.AppVersion, Is.EqualTo("1.0.0"));
            Assert.That(configuration.ResourceInitOptions, Is.Null);
            Assert.That(typeof(BootstrapConfiguration).GetProperty("HotUpdateDllLocation"), Is.Null);
            Assert.That(typeof(BootstrapConfiguration).GetProperty("PlayMode"), Is.Null);
        }

        [Test]
        public void ResourceInitOptions_CanBeAssigned()
        {
            var configuration = new BootstrapConfiguration(BootstrapEnvironment.Beta, "2.0.0")
            {
                ResourceInitOptions = new ResourceInitOptions()
            };
            Assert.That(configuration.ResourceInitOptions, Is.Not.Null);
            Assert.That(BootstrapConfiguration.DefaultLogLevel(BootstrapEnvironment.Dev), Is.EqualTo(LogLevel.Trace));
            Assert.That(BootstrapConfiguration.DefaultLogLevel(BootstrapEnvironment.Beta), Is.EqualTo(LogLevel.Debug));
            Assert.That(BootstrapConfiguration.DefaultLogLevel(BootstrapEnvironment.Gold), Is.EqualTo(LogLevel.Info));
        }
    }
}
