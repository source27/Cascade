using NUnit.Framework;

namespace Cascade.Tests
{
    public sealed class FoundationValidatorTests
    {
        [Test]
        public void ValidLayeredDefinitionsPass()
        {
            var result = Cascade.Editor.FoundationValidator.ValidateDefinitions(
                new[]
                {
                    Definition("Cascade.Bootstrap", "Cascade.Service", "Cascade.Core"),
                    Definition("Cascade.Service"),
                    Definition("Cascade.Core", "Cascade.Service"),
                    Definition("Cascade.Editor", "Cascade.Service", "Cascade.Core"),
                    Definition("Cascade.Tests", "Cascade.Editor", "Cascade.Bootstrap", "Cascade.Service", "Cascade.Core")
                },
                new[] { "/project/Runtime/Cascade.Core/Event/EventBus.cs" },
                "/project");
            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
        }

        [Test]
        public void ReverseReferenceAndUnownedScriptFail()
        {
            var result = Cascade.Editor.FoundationValidator.ValidateDefinitions(
                new[]
                {
                    Definition("Cascade.Bootstrap"),
                    Definition("Cascade.Service", "Cascade.Core"),
                    Definition("Cascade.Core", "Cascade.Service"),
                    Definition("Cascade.Editor"),
                    Definition("Cascade.Tests")
                },
                new[] { "/project/Unowned.cs" },
                "/project");
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("disallowed reference"));
            Assert.That(result.Errors, Has.Some.Contains("outside an explicit assembly boundary"));
        }

        [Test]
        public void NamespaceOwnershipPasses()
        {
            var result = Cascade.Editor.FoundationValidator.ValidateNamespaces(
                new[]
                {
                    Definition("Cascade.Bootstrap"),
                    Definition("Cascade.Service"),
                    Definition("Cascade.Core"),
                },
                new[]
                {
                    Source("/project/Runtime/Cascade.Core/Event/EventBus.cs", "namespace Cascade.Core { class EventBus {} }"),
                    Source("/project/Runtime/Cascade.Service/Log/UnityLogService.cs", "namespace Cascade.Service { class UnityLogService {} }"),
                    Source("/project/Runtime/Cascade.Bootstrap/BootstrapEntry.cs", "namespace Cascade.Bootstrap { class BootstrapEntry {} }"),
                });
            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
        }

        [Test]
        public void NamespaceOwnershipRejectsWrongRoots()
        {
            var result = Cascade.Editor.FoundationValidator.ValidateNamespaces(
                new[] { Definition("Cascade.Core") },
                new[] { Source("/project/Runtime/Cascade.Core/Bad.cs", "namespace Wrong.Root { class Bad {} }") });
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("namespace"));
        }

        [Test]
        public void NamespaceOwnershipPassesForTestArea()
        {
            var result = Cascade.Editor.FoundationValidator.ValidateNamespaces(
                new[] { Definition("Cascade.Tests") },
                new[] { Source("/project/Tests/Cascade.Tests/FooTests.cs", "namespace Cascade.Tests { class FooTests {} }") });
            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
        }

        [Test]
        public void NamespaceOwnershipRejectsForeignTestNamespace()
        {
            var result = Cascade.Editor.FoundationValidator.ValidateNamespaces(
                new[] { Definition("Cascade.Tests") },
                new[] { Source("/project/Tests/Cascade.Tests/FooTests.cs", "namespace Other.Tests { class FooTests {} }") });
            Assert.That(result.IsValid, Is.False);
        }

        private static Cascade.Editor.AssemblyDefinitionInfo Definition(string name, params string[] references)
        {
            return new Cascade.Editor.AssemblyDefinitionInfo
            {
                name = name,
                rootNamespace = name,
                references = references,
                path = $"/project/Runtime/{name}/{name}.asmdef"
            };
        }

        private static Cascade.Editor.SourceFileInfo Source(string path, string contents)
        {
            return new Cascade.Editor.SourceFileInfo { path = path, contents = contents };
        }
    }
}
