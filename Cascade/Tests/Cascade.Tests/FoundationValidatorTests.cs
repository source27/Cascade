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
                    Definition("Cascade.Launcher", "Cascade.Service", "Cascade.Core", "Cascade.Module"),
                    Definition("Cascade.Service"),
                    Definition("Cascade.Core", "Cascade.Service"),
                    Definition("Cascade.Module"),
                    Definition("Cascade.Editor", "Cascade.Service", "Cascade.Core"),
                    Definition("Cascade.Tests", "Cascade.Editor", "Cascade.Launcher", "Cascade.Service", "Cascade.Core")
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
                    Definition("Cascade.Launcher"),
                    Definition("Cascade.Service", "Cascade.Core"),
                    Definition("Cascade.Core", "Cascade.Service"),
                    Definition("Cascade.Module"),
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
                    Definition("Cascade.Launcher"),
                    Definition("Cascade.Core"),
                    Definition("Cascade.Editor"),
                    Definition("Cascade.Tests")
                },
                new[]
                {
                    Source("/project/Runtime/Cascade.Launcher/BootstrapEntry.cs", "namespace Cascade.Launcher { }"),
                    Source("/project/Runtime/Cascade.Core/Event/EventBus.cs", "namespace Cascade.Core { }"),
                    Source("/project/Editor/Cascade.Editor/UIScriptGenerator.cs", "namespace Cascade.Editor { }")
                });

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
        }

        [Test]
        public void NamespaceOwnershipRejectsWrongRoots()
        {
            var result = Cascade.Editor.FoundationValidator.ValidateNamespaces(
                new[]
                {
                    DefinitionWithRoot("Cascade.Launcher", "Cascade.Service"),
                    DefinitionWithRoot("Cascade.Core", "Cascade.Launcher"),
                    Definition("Cascade.Editor"),
                    Definition("Cascade.Tests")
                },
                new[]
                {
                    Source("/project/Runtime/Cascade.Launcher/BootstrapEntry.cs", "namespace Cascade.Service { }"),
                    Source("/project/Runtime/Cascade.Core/Event/EventBus.cs", "namespace Cascade.Launcher { }")
                });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("expected root Cascade.Launcher"));
            Assert.That(result.Errors, Has.Some.Contains("expected root Cascade.Core"));
            Assert.That(result.Errors, Has.Some.Contains("must use root namespace Cascade.Launcher"));
        }

        [Test]
        public void NamespaceOwnershipPassesForTestArea()
        {
            var result = Cascade.Editor.FoundationValidator.ValidateNamespaces(
                new[]
                {
                    Definition("Cascade.Tests")
                },
                new[]
                {
                    Source("/project/Tests/Cascade.Tests/AudioServiceTests.cs", "namespace Cascade.Tests { }"),
                    Source("/project/Tests/Cascade.Tests/FoundationValidatorTests.cs", "namespace Cascade.Tests { }")
                });

            Assert.That(result.IsValid, Is.True, string.Join("\n", result.Errors));
        }

        [Test]
        public void NamespaceOwnershipRejectsForeignTestNamespace()
        {
            var result = Cascade.Editor.FoundationValidator.ValidateNamespaces(
                new[]
                {
                    Definition("Cascade.Tests")
                },
                new[]
                {
                    Source("/project/Tests/Cascade.Tests/GameLogicTests.cs", "namespace Cascade.Launcher { }")
                });

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors, Has.Some.Contains("expected root Cascade.Tests"));
        }

        private static Cascade.Editor.AssemblyDefinitionInfo Definition(string name, params string[] references)
        {
            return DefinitionWithRoot(name, ExpectedRoot(name), references);
        }

        private static Cascade.Editor.AssemblyDefinitionInfo DefinitionWithRoot(string name, string rootNamespace, params string[] references)
        {
            return new Cascade.Editor.AssemblyDefinitionInfo
            {
                name = name,
                rootNamespace = rootNamespace,
                references = references,
                path = name == "Cascade.Tests"
                    ? "/project/Tests/Cascade.Tests.asmdef"
                    : name == "Cascade.Editor"
                        ? "/project/Editor/Cascade.Editor.asmdef"
                        : "/project/Runtime/" + name + "/" + name + ".asmdef"
            };
        }

        private static Cascade.Editor.SourceFileInfo Source(string path, string contents)
        {
            return new Cascade.Editor.SourceFileInfo { path = path, contents = contents };
        }

        private static string ExpectedRoot(string name)
        {
            switch (name)
            {
                case "Cascade.Launcher": return "Cascade.Launcher";
                case "Cascade.Service": return "Cascade.Service";
                case "Cascade.Core": return "Cascade.Core";
                case "Cascade.Module": return "Cascade.Module";
                case "Cascade.Editor": return "Cascade.Editor";
                case "Cascade.Tests": return "Cascade.Tests";
                default: return null;
            }
        }
    }
}
