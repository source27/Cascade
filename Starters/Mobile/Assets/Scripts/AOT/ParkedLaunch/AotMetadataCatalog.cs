using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cascade.Mobile
{
    /// <summary>
    /// Resolves AOT metadata resource locations from HybridCLR generated
    /// <c>AOTGenericReferences.PatchedAOTAssemblyList</c>. Empty list is valid.
    /// Location convention: module name as-is (e.g. mscorlib.dll).
    /// </summary>
    public static class AotMetadataCatalog
    {
        public static IReadOnlyList<string> ResolveLocations()
        {
            var patched = TryReadPatchedAssemblyList();
            if (patched == null || patched.Count == 0)
                return Array.Empty<string>();

            return patched
                .Select(NormalizeLocation)
                .Where(location => !string.IsNullOrEmpty(location))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        public static string NormalizeLocation(string patchedAssemblyName)
        {
            if (string.IsNullOrWhiteSpace(patchedAssemblyName))
                return null;

            var name = patchedAssemblyName.Trim().Replace('\\', '/');
            var slash = name.LastIndexOf('/');
            if (slash >= 0)
                name = name.Substring(slash + 1);
            return name;
        }

        private static IReadOnlyList<string> TryReadPatchedAssemblyList()
        {
            var type = FindAotGenericReferencesType();
            if (type == null)
                return null;

            var field = type.GetField("PatchedAOTAssemblyList", BindingFlags.Public | BindingFlags.Static);
            if (field == null)
                return null;

            if (field.GetValue(null) is IEnumerable<string> values)
                return values.ToArray();

            return null;
        }

        private static Type FindAotGenericReferencesType()
        {
            const string typeName = "AOTGenericReferences";
            var direct = Type.GetType(typeName)
                ?? Type.GetType(typeName + ", Assembly-CSharp");
            if (direct != null)
                return direct;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try
                {
                    type = assembly.GetType(typeName, false);
                }
                catch
                {
                    continue;
                }

                if (type != null)
                    return type;
            }

            return null;
        }
    }
}
