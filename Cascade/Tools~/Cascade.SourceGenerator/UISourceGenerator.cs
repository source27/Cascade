using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Cascade.SourceGenerator
{
    [Generator]
    public sealed class UISourceGenerator : ISourceGenerator
    {
        private const string UIAttributeName = "Cascade.Core.UIAttribute";
        private const string UIBaseName = "Cascade.Core.UIBase";
        private const string IUIArgsName = "Cascade.Core.IUIArgs`1";
        private const string GeneratedNamespace = "Cascade.Generated";

        private static readonly DiagnosticDescriptor NotUIPage = new DiagnosticDescriptor(
            "CSCD001",
            "UI page must inherit UIBase",
            "Type '{0}' is marked with [UI] but does not inherit Cascade.Core.UIBase",
            "Cascade.UI",
            DiagnosticSeverity.Error,
            true);

        private static readonly DiagnosticDescriptor DuplicatePage = new DiagnosticDescriptor(
            "CSCD002",
            "Duplicate UI page name",
            "More than one [UI] page uses the generated name '{0}'",
            "Cascade.UI",
            DiagnosticSeverity.Error,
            true);

        private static readonly DiagnosticDescriptor MissingBindings = new DiagnosticDescriptor(
            "CSCD003",
            "UI bindings are missing",
            "UI page '{0}' has no generated binding type '{1}.{0}Bindings'. Generate bindings from its Prefab.",
            "Cascade.UI",
            DiagnosticSeverity.Error,
            true);

        private static readonly DiagnosticDescriptor DuplicateAddress = new DiagnosticDescriptor(
            "CSCD004",
            "Duplicate UI address",
            "UI address '{0}' is used by both '{1}' and '{2}'",
            "Cascade.UI",
            DiagnosticSeverity.Error,
            true);

        private static readonly DiagnosticDescriptor MissingArgs = new DiagnosticDescriptor(
            "CSCD006",
            "UI page Args are missing or invalid",
            "UI page '{0}' must declare a nested Args type implementing IUIArgs<{0}>",
            "Cascade.UI",
            DiagnosticSeverity.Error,
            true);

        private static readonly DiagnosticDescriptor InvalidPolicy = new DiagnosticDescriptor(
            "CSCD007",
            "Invalid UI policy combination",
            "UI page '{0}' has invalid [UI] policies: {1}",
            "Cascade.UI",
            DiagnosticSeverity.Error,
            true);

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        public void Execute(GeneratorExecutionContext context)
        {
            var uiAttribute = context.Compilation.GetTypeByMetadataName(UIAttributeName);
            var uiBase = context.Compilation.GetTypeByMetadataName(UIBaseName);
            if (uiAttribute == null || uiBase == null)
                return;

            var pages = FindPages(context.Compilation, uiAttribute, uiBase).ToList();
            if (pages.Count == 0)
                return;

            var validPages = new List<PageModel>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var addresses = new Dictionary<string, PageModel>(StringComparer.Ordinal);

            foreach (var page in pages)
            {
                if (!page.Symbol.InheritsFrom(uiBase))
                {
                    context.ReportDiagnostic(Diagnostic.Create(NotUIPage, page.Location, page.Symbol.Name));
                    continue;
                }

                if (!HasValidArgs(page.Symbol))
                {
                    context.ReportDiagnostic(Diagnostic.Create(MissingArgs, page.Location, page.Symbol.Name));
                    continue;
                }

                var policyError = ValidatePolicies(page);
                if (policyError != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(InvalidPolicy, page.Location, page.Symbol.Name, policyError));
                    continue;
                }

                if (!names.Add(page.Symbol.Name))
                {
                    context.ReportDiagnostic(Diagnostic.Create(DuplicatePage, page.Location, page.Symbol.Name));
                    continue;
                }

                if (!TryResolveBindingsType(context.Compilation, page.Symbol, out var bindingsType))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        MissingBindings,
                        page.Location,
                        page.Symbol.Name,
                        bindingsType ?? GeneratedNamespace + "." + page.Symbol.Name + "Bindings"));
                    continue;
                }

                page.BindingsTypeDisplay = bindingsType;

                if (addresses.TryGetValue(page.Address, out var previous))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DuplicateAddress,
                        page.Location,
                        page.Address,
                        previous.Symbol.Name,
                        page.Symbol.Name));
                    continue;
                }

                addresses.Add(page.Address, page);
                validPages.Add(page);
            }

            if (validPages.Count == 0)
                return;

            context.AddSource(
                "UIRegistryGenerated.g.cs",
                GenerateRegistry(validPages));
        }

        /// <summary>
        /// Prefer the bindings type from a generic base (e.g. UIPage&lt;TArgs,TBindings&gt;),
        /// then fall back to Cascade.Generated.{Page}Bindings.
        /// Returns the fully-qualified metadata name for codegen.
        /// </summary>
        private static bool TryResolveBindingsType(
            Compilation compilation,
            INamedTypeSymbol page,
            out string bindingsTypeDisplay)
        {
            var expectedName = page.Name + "Bindings";
            for (var baseType = page.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                foreach (var arg in baseType.TypeArguments)
                {
                    if (!(arg is INamedTypeSymbol named) || named.Name != expectedName)
                        continue;
                    bindingsTypeDisplay = named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    return true;
                }
            }

            var conventional = GeneratedNamespace + "." + expectedName;
            var found = compilation.GetTypeByMetadataName(conventional);
            if (found != null)
            {
                bindingsTypeDisplay = found.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return true;
            }

            bindingsTypeDisplay = conventional;
            return false;
        }



        private static bool HasValidArgs(INamedTypeSymbol page)
        {
            var args = page.GetTypeMembers("Args").FirstOrDefault();
            if (args == null)
                return false;

            foreach (var iface in args.AllInterfaces)
            {
                if (iface.OriginalDefinition == null ||
                    iface.OriginalDefinition.MetadataName != "IUIArgs`1" ||
                    iface.OriginalDefinition.ContainingNamespace?.ToDisplayString() != "Cascade.Core")
                    continue;
                if (iface.TypeArguments.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(iface.TypeArguments[0], page))
                    return true;
            }

            return false;
        }

        private static string ValidatePolicies(PageModel page)
        {
            // Presentation: 0 Screen, 1 Modal, 2 Overlay, 3 WorldWidget
            if (page.Presentation == 0)
            {
                if (!page.FullScreen)
                    return "Screen must be FullScreen";
                if (page.Mask)
                    return "Screen cannot use Mask";
            }
            else if (page.Presentation == 2 || page.Presentation == 3)
            {
                if (page.FullScreen)
                    return "Overlay/WorldWidget cannot be FullScreen";
                if (page.Mask)
                    return "Overlay/WorldWidget cannot use Mask";
            }

            if (page.FullScreen && page.Mask)
                return "FullScreen cannot use Mask";
            if (page.Mask && page.Input != 1)
                return "Mask requires Input.Block";
            if (page.CloseOnMaskClick && (page.Presentation != 1 || page.Input != 1))
                return "CloseOnMaskClick requires Modal + Block";
            if (page.Presentation == 3 && page.SafeArea != 0)
                return "WorldWidget cannot use SafeArea";
            if ((page.SafeArea & 4) != 0 && (page.SafeArea & 1) == 0)
                return "TopMask requires FitTop";
            if ((page.SafeArea & 8) != 0 && (page.SafeArea & 2) == 0)
                return "BottomMask requires FitBottom";
            return null;
        }

        private static IEnumerable<PageModel> FindPages(
            Compilation compilation,
            INamedTypeSymbol uiAttribute,
            INamedTypeSymbol uiBase)
        {
            foreach (var type in GetNamedTypes(compilation.Assembly.GlobalNamespace))
            {

                var attribute = type.GetAttributes().FirstOrDefault(item =>
                    SymbolEqualityComparer.Default.Equals(item.AttributeClass, uiAttribute));
                if (attribute == null)
                    continue;

                var presentation = GetEnumProperty(attribute, "Presentation") ?? 0;
                var fullScreenSpecified = HasNamed(attribute, "FullScreen");
                var maskSpecified = HasNamed(attribute, "Mask");
                var inputSpecified = HasNamed(attribute, "Input");
                var backSpecified = HasNamed(attribute, "Back");

                // Defaults aligned with UIAttribute + Presentation rules.
                var fullScreen = fullScreenSpecified
                    ? GetBoolProperty(attribute, "FullScreen")
                    : presentation == 0; // Screen true; others false
                var mask = maskSpecified
                    ? GetBoolProperty(attribute, "Mask")
                    : presentation == 1; // Modal true
                var input = inputSpecified
                    ? GetEnumProperty(attribute, "Input").Value
                    : presentation == 0 || presentation == 1 ? 1 : 0; // Screen/Modal Block
                var back = backSpecified
                    ? GetEnumProperty(attribute, "Back").Value
                    : 0; // Close

                yield return new PageModel(
                    type,
                    GetStringProperty(attribute, "Address") ?? type.Name,
                    GetEnumProperty(attribute, "Layer") ?? 0,
                    presentation,
                    GetEnumProperty(attribute, "Cache") ?? 0,
                    input,
                    back,
                    fullScreen,
                    mask,
                    GetBoolProperty(attribute, "CloseOnMaskClick"),
                    GetEnumProperty(attribute, "SafeArea") ?? 0,
                    GetStringProperty(attribute, "SafeAreaPath"));
            }
        }

        private static IEnumerable<INamedTypeSymbol> GetNamedTypes(INamespaceSymbol namespaceSymbol)
        {
            foreach (var type in namespaceSymbol.GetTypeMembers())
                foreach (var nested in GetNestedTypes(type))
                    yield return nested;

            foreach (var child in namespaceSymbol.GetNamespaceMembers())
                foreach (var type in GetNamedTypes(child))
                    yield return type;
        }

        private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
        {
            yield return type;
            foreach (var nested in type.GetTypeMembers())
                foreach (var child in GetNestedTypes(nested))
                    yield return child;
        }

        private static bool HasNamed(AttributeData attribute, string name)
        {
            return attribute.NamedArguments.Any(item => item.Key == name);
        }

        private static string GetStringProperty(AttributeData attribute, string name)
        {
            var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
            return value.Kind == TypedConstantKind.Primitive ? value.Value as string : null;
        }

        private static bool GetBoolProperty(AttributeData attribute, string name)
        {
            var value = attribute.NamedArguments.FirstOrDefault(item => item.Key == name).Value;
            if (value.Kind != TypedConstantKind.Primitive || value.Value == null)
                return false;
            return Convert.ToBoolean(value.Value);
        }

        private static int? GetEnumProperty(AttributeData attribute, string name)
        {
            var pair = attribute.NamedArguments.FirstOrDefault(item => item.Key == name);
            if (pair.Key == null)
                return null;
            var value = pair.Value;
            if (value.Kind != TypedConstantKind.Enum || value.Value == null)
                return null;
            return Convert.ToInt32(value.Value);
        }

        private static string GenerateRegistry(IReadOnlyList<PageModel> pages)
        {
            var builder = new StringBuilder();
            builder.AppendLine("// <auto-generated />");
            builder.AppendLine("#nullable disable");
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine();
            builder.AppendLine("namespace " + GeneratedNamespace);
            builder.AppendLine("{");
            builder.AppendLine("    public static class UIRegistryGenerated");
            builder.AppendLine("    {");
            builder.AppendLine("        public static void RegisterAll(global::Cascade.Core.UIRegistry registry)");
            builder.AppendLine("        {");

            foreach (var page in pages.OrderBy(item => item.Symbol.Name, StringComparer.Ordinal))
            {
                builder.AppendLine("            registry.Register(new global::Cascade.Core.UIPageRegistration(");
                builder.AppendLine("                typeof(global::" + page.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Substring("global::".Length) + "),");
                builder.AppendLine("                \"" + Escape(page.Address) + "\",");
                builder.AppendLine("                (global::Cascade.Core.UILayer)" + page.Layer + ",");
                builder.AppendLine("                (global::Cascade.Core.UIPresentation)" + page.Presentation + ",");
                builder.AppendLine("                (global::Cascade.Core.UICachePolicy)" + page.Cache + ",");
                builder.AppendLine("                (global::Cascade.Core.UIInputPolicy)" + page.Input + ",");
                builder.AppendLine("                (global::Cascade.Core.UIBackPolicy)" + page.Back + ",");
                builder.AppendLine("                " + (page.FullScreen ? "true" : "false") + ",");
                builder.AppendLine("                " + (page.Mask ? "true" : "false") + ",");
                builder.AppendLine("                " + (page.CloseOnMaskClick ? "true" : "false") + ",");
                builder.AppendLine("                (global::Cascade.Core.UISafeAreaPolicy)" + page.SafeArea + ",");
                builder.AppendLine(page.SafeAreaPath == null
                    ? "                null,"
                    : "                \"" + Escape(page.SafeAreaPath) + "\",");
                builder.AppendLine("                new " + page.Symbol.Name + "Factory()));");
            }

            builder.AppendLine("        }");
            foreach (var page in pages.OrderBy(item => item.Symbol.Name, StringComparer.Ordinal))
            {
                builder.AppendLine();
                builder.AppendLine("        private sealed class " + page.Symbol.Name + "Factory : global::Cascade.Core.IUIPageFactory");
                builder.AppendLine("        {");
                builder.AppendLine("            public global::Cascade.Core.UIBase Create(GameObject root, object bindings, object pageContext)");
                builder.AppendLine("            {");
                builder.AppendLine("                var page = new global::" + page.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Substring("global::".Length) + "();");
                builder.AppendLine("                page.Initialize(root, new " + page.BindingsTypeDisplay + "((global::Cascade.Core.UIBindingHost)bindings), pageContext);");
                builder.AppendLine("                return page;");
                builder.AppendLine("            }");
                builder.AppendLine("        }");
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private sealed class PageModel
        {
            public PageModel(
                INamedTypeSymbol symbol,
                string address,
                int layer,
                int presentation,
                int cache,
                int input,
                int back,
                bool fullScreen,
                bool mask,
                bool closeOnMaskClick,
                int safeArea,
                string safeAreaPath)
            {
                Symbol = symbol;
                Address = address;
                Layer = layer;
                Presentation = presentation;
                Cache = cache;
                Input = input;
                Back = back;
                FullScreen = fullScreen;
                Mask = mask;
                CloseOnMaskClick = closeOnMaskClick;
                SafeArea = safeArea;
                SafeAreaPath = safeAreaPath;
                Location = symbol.Locations.FirstOrDefault();
                BindingsTypeDisplay = "global::" + GeneratedNamespace + "." + symbol.Name + "Bindings";
            }

            public INamedTypeSymbol Symbol { get; }
            public string Address { get; }
            public int Layer { get; }
            public int Presentation { get; }
            public int Cache { get; }
            public int Input { get; }
            public int Back { get; }
            public bool FullScreen { get; }
            public bool Mask { get; }
            public bool CloseOnMaskClick { get; }
            public int SafeArea { get; }
            public string SafeAreaPath { get; }
            public Location Location { get; }
            public string BindingsTypeDisplay { get; set; }

        }
    }

    internal static class SymbolExtensions
    {
        public static bool InheritsFrom(this INamedTypeSymbol type, INamedTypeSymbol baseType)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(current, baseType))
                    return true;
            }
            return false;
        }
    }
}
