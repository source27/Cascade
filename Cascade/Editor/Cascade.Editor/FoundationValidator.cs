using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Cascade.Editor
{
    [Serializable]
    public sealed class AssemblyDefinitionInfo
    {
        public string name;
        public string rootNamespace;
        public string[] references = Array.Empty<string>();
        public string path;
    }

    public sealed class SourceFileInfo
    {
        public string path;
        public string contents;
    }

    public sealed class FoundationValidationResult
    {
        public readonly List<string> Errors = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    public static class FoundationValidator
    {
        private static readonly string[] RequiredRuntimeAssemblies =
        {
            "Cascade.Launcher",
            "Cascade.Service",
            "Cascade.Core",
            "Cascade.Module"
        };

        private static readonly Dictionary<string, string[]> AllowedReferences = new Dictionary<string, string[]>
        {
            ["Cascade.Launcher"] = new[] { "Cascade.Service", "Cascade.Core", "Cascade.Module", "HybridCLR.Runtime", "UniTask", "UnityEngine.UI" },
            ["Cascade.Service"] = new[] { "UniTask" },
            ["Cascade.Core"] = new[] { "Cascade.Service", "UniTask", "UnityEngine.UI", "Unity.TextMeshPro", "LoopScrollRect.Runtime", "LitMotion", "LitMotion.Extensions" },
            ["Cascade.Module"] = new string[0]
        };

        public static FoundationValidationResult ValidateProject(string projectRoot)
        {
            var assetsRoot = projectRoot;
            var definitions = Directory.Exists(assetsRoot)
                ? Directory.GetFiles(assetsRoot, "*.asmdef", SearchOption.AllDirectories)
                    .Select(path => ParseDefinition(path, assetsRoot))
                    .ToArray()
                : Array.Empty<AssemblyDefinitionInfo>();
            var sourceFiles = Directory.Exists(assetsRoot)
                ? Directory.GetFiles(assetsRoot, "*.cs", SearchOption.AllDirectories)
                : Array.Empty<string>();

            var result = ValidateDefinitions(definitions, sourceFiles, assetsRoot);
            var sourceInfos = sourceFiles.Select(path => new SourceFileInfo
            {
                path = path,
                contents = File.ReadAllText(path)
            });
            AppendErrors(result, ValidateNamespaces(definitions, sourceInfos));
            return result;
        }

        public static FoundationValidationResult ValidateDefinitions(
            IEnumerable<AssemblyDefinitionInfo> definitions,
            IEnumerable<string> runtimeSourceFiles,
            string assetsRoot)
        {
            var result = new FoundationValidationResult();
            var definitionsByName = definitions
                .GroupBy(definition => definition.name)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            foreach (var required in RequiredRuntimeAssemblies)
            {
                if (!definitionsByName.ContainsKey(required))
                    result.Errors.Add($"Missing required assembly definition: {required}");
            }

            foreach (var definition in definitionsByName.Values)
            {
                if (!AllowedReferences.TryGetValue(definition.name, out var allowed))
                    continue;

                foreach (var reference in definition.references ?? Array.Empty<string>())
                {
                    if (!allowed.Contains(reference, StringComparer.Ordinal))
                        result.Errors.Add($"Assembly {definition.name} has disallowed reference {reference} ({definition.path}).");
                }
            }

            foreach (var sourceFile in runtimeSourceFiles)
            {
                var owner = FindOwner(sourceFile, definitionsByName.Values);
                if (owner == null)
                    result.Errors.Add($"Runtime script is outside an explicit assembly boundary: {Normalize(sourceFile)}");
            }

            definitionsByName.TryGetValue("Cascade.Tests", out var testDefinition);
            if (testDefinition == null)
                result.Errors.Add("Missing explicit test assembly: Cascade.Tests");
            else if (testDefinition.path.Replace('\\', '/').IndexOf("/Tests/", StringComparison.Ordinal) < 0)
                result.Errors.Add($"Test assembly has an unexpected owner path: {testDefinition.path}");

            return result;
        }

        public static FoundationValidationResult ValidateNamespaces(
            IEnumerable<AssemblyDefinitionInfo> definitions,
            IEnumerable<SourceFileInfo> sourceFiles)
        {
            var result = new FoundationValidationResult();
            var definitionsByName = definitions.ToDictionary(definition => definition.name, StringComparer.Ordinal);

            foreach (var definition in definitionsByName.Values)
            {
                var expected = ExpectedNamespaceForAssembly(definition.name);
                if (expected != null && !string.Equals(definition.rootNamespace, expected, StringComparison.Ordinal))
                    result.Errors.Add($"Assembly {definition.name} at {definition.path} must use root namespace {expected}, but declares {definition.rootNamespace}.");
            }

            foreach (var sourceFile in sourceFiles)
            {
                var expected = ExpectedNamespaceForPath(sourceFile.path);
                if (expected == null)
                    continue;

                var declaredNamespaces = Regex.Matches(sourceFile.contents ?? string.Empty, @"(?m)^\s*namespace\s+([A-Za-z_][A-Za-z0-9_.]*)")
                    .Cast<Match>()
                    .Select(match => match.Groups[1].Value)
                    .ToArray();
                if (declaredNamespaces.Length == 0)
                {
                    result.Errors.Add($"Namespace violation at {Normalize(sourceFile.path)}: owning area expects {expected}, but no namespace is declared.");
                    continue;
                }

                foreach (var actual in declaredNamespaces)
                {
                    if (!actual.Equals(expected, StringComparison.Ordinal) &&
                        !actual.StartsWith(expected + ".", StringComparison.Ordinal))
                    {
                        result.Errors.Add($"Namespace violation at {Normalize(sourceFile.path)}: expected root {expected}, actual namespace root {actual}.");
                    }
                }
            }

            return result;
        }

        private static AssemblyDefinitionInfo ParseDefinition(string path, string assetsRoot)
        {
            var definition = JsonUtility.FromJson<AssemblyDefinitionInfo>(File.ReadAllText(path));
            definition.path = Normalize(path).Replace(Normalize(assetsRoot), "Assets/Scripts");
            return definition;
        }

        private static string ExpectedNamespaceForAssembly(string assemblyName)
        {
            switch (assemblyName)
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

        private static string ExpectedNamespaceForPath(string path)
        {
            var normalized = Normalize(path);
            if (normalized.IndexOf("/Runtime/Cascade.Launcher/", StringComparison.OrdinalIgnoreCase) >= 0) return "Cascade.Launcher";
            if (normalized.IndexOf("/Runtime/Cascade.Service/", StringComparison.OrdinalIgnoreCase) >= 0) return "Cascade.Service";
            if (normalized.IndexOf("/Runtime/Cascade.Core/", StringComparison.OrdinalIgnoreCase) >= 0) return "Cascade.Core";
            if (normalized.IndexOf("/Runtime/Cascade.Module/", StringComparison.OrdinalIgnoreCase) >= 0) return "Cascade.Module";
            if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0) return "Cascade.Editor";
            if (normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0) return "Cascade.Tests";
            return null;
        }

        private static void AppendErrors(FoundationValidationResult target, FoundationValidationResult source)
        {
            target.Errors.AddRange(source.Errors);
        }

        private static AssemblyDefinitionInfo FindOwner(string sourceFile, IEnumerable<AssemblyDefinitionInfo> definitions)
        {
            var normalizedSource = Path.GetFullPath(sourceFile).Replace('\\', '/');
            return definitions
                .Where(definition => normalizedSource.StartsWith(Path.GetDirectoryName(Path.GetFullPath(definition.path))?.Replace('\\', '/') + "/", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(definition => definition.path.Length)
                .FirstOrDefault();
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
