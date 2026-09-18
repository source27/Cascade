using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cascade.Editor
{
    [Serializable]
    public sealed class CascadePackagePeer
    {
        public string name;
        public string url;
        public string note;
    }

    [Serializable]
    public sealed class CascadePackageEntry
    {
        public string name;
        public string displayName;
        public string kind;
        public string path;
        public string when;
        public CascadePackagePeer[] peers;
    }

    [Serializable]
    public sealed class CascadePackageCatalogueFile
    {
        public string repository;
        public string note;
        public CascadePackageEntry[] packages;
    }

    /// <summary>
    /// Reads the optional-package catalogue shipped inside this package and turns entries into
    /// installable UPM references.
    /// <para>
    /// Why a file inside the package: a project that installed Cascade by git URL only receives the
    /// <c>?path=Cascade</c> subtree, so the repository layout (Modules/, Integrations/) is not
    /// available at runtime. <c>PackageCatalogue.json</c> is the single source for the window, and
    /// <c>scripts/validate-upm.mjs</c> keeps it in sync with the actual directories.
    /// </para>
    /// </summary>
    public static class CascadePackageCatalogue
    {
        public const string FileName = "PackageCatalogue.json";
        public const string CatalogueAssetPath = "Editor/Cascade.Editor/" + FileName;

        private static CascadePackageCatalogueFile _cached;

        /// <summary>Resolved on-disk root of the main package (<c>…/Cascade</c> or PackageCache entry).</summary>
        public static string PackageRoot { get; } = ResolvePackageRoot();

        /// <summary>Repository root when the local monorepo layout is present, otherwise null.</summary>
        public static string RepositoryRoot { get; } = ResolveRepositoryRoot();

        /// <summary>Project root (parent of Assets/).</summary>
        public static string ProjectRoot { get; } = ResolveProjectRoot();

        /// <summary>True when this copy of Cascade sits inside the full monorepo checkout.</summary>
        public static bool IsLocalMonorepo => !string.IsNullOrEmpty(RepositoryRoot);

        public static CascadePackageCatalogueFile Load(bool forceReload = false)
        {
            if (_cached != null && !forceReload)
                return _cached;

            var path = Path.Combine(PackageRoot, CatalogueAssetPath);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Cascade] Package catalogue not found: {path}");
                return _cached = new CascadePackageCatalogueFile { packages = Array.Empty<CascadePackageEntry>() };
            }

            try
            {
                _cached = JsonUtility.FromJson<CascadePackageCatalogueFile>(File.ReadAllText(path));
                _cached.packages ??= Array.Empty<CascadePackageEntry>();
                return _cached;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Cascade] Failed to read {FileName}: {exception.Message}");
                return _cached = new CascadePackageCatalogueFile { packages = Array.Empty<CascadePackageEntry>() };
            }
        }

        /// <summary>
        /// Install reference for an entry: a relative <c>file:</c> path when the monorepo checkout is
        /// next to this package, otherwise a git URL (<c>?path=…</c>) pinned to the main package's
        /// revision when UPM knows one.
        /// </summary>
        public static string ResolveInstallUrl(CascadePackageEntry entry, out string mode)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.path))
            {
                mode = string.Empty;
                return string.Empty;
            }

            if (IsLocalMonorepo)
            {
                var target = Path.GetFullPath(Path.Combine(RepositoryRoot, entry.path));
                mode = Directory.Exists(target) ? $"本地 file: → {entry.path}" : $"本地目录缺失：{entry.path}";
                return BuildLocalUrl(PackagesDirectory(ProjectRoot), target);
            }

            var revision = MainPackageRevision();
            mode = string.IsNullOrEmpty(revision) ? "git URL（未 pin revision）" : $"git URL（pin {ShortRevision(revision)}）";
            return BuildGitUrl(ResolveRepositoryUrl(), entry.path, revision);
        }

        /// <summary>Manifest line fragment for an entry (and its peers), ready to paste.</summary>
        public static string BuildManifestSnippet(CascadePackageEntry entry, string installUrl)
        {
            var lines = new System.Collections.Generic.List<string>
            {
                $"\"{entry.name}\": \"{installUrl}\""
            };

            if (entry.peers != null)
            {
                foreach (var peer in entry.peers.Where(peer => peer != null && !string.IsNullOrWhiteSpace(peer.url)))
                    lines.Add($"\"{peer.name}\": \"{peer.url}\"");
            }

            return string.Join(",\n", lines);
        }

        // ---- pure helpers (unit-tested) -------------------------------------------------------

        /// <summary>Relative <c>file:</c> reference as UPM writes it into Packages/manifest.json.</summary>
        public static string BuildLocalUrl(string packagesDirectory, string targetDirectory)
        {
            if (string.IsNullOrWhiteSpace(packagesDirectory))
                throw new ArgumentException("Packages directory is required.", nameof(packagesDirectory));
            if (string.IsNullOrWhiteSpace(targetDirectory))
                throw new ArgumentException("Target directory is required.", nameof(targetDirectory));

            var relative = Path.GetRelativePath(packagesDirectory, targetDirectory)
                .Replace('\\', '/');
            return "file:" + relative;
        }

        /// <summary>Git reference with the repository's <c>?path=</c> selector and an optional revision pin.</summary>
        public static string BuildGitUrl(string repository, string packagePath, string revision)
        {
            if (string.IsNullOrWhiteSpace(repository))
                throw new ArgumentException("Repository URL is required.", nameof(repository));
            if (string.IsNullOrWhiteSpace(packagePath))
                throw new ArgumentException("Package path is required.", nameof(packagePath));

            var baseUrl = repository.Trim();
            var cut = baseUrl.IndexOfAny(new[] { '?', '#' });
            if (cut >= 0)
                baseUrl = baseUrl.Substring(0, cut);
            baseUrl = baseUrl.TrimEnd('/');

            var url = $"{baseUrl}?path={packagePath.Trim().Replace('\\', '/').Trim('/')}";
            if (!string.IsNullOrWhiteSpace(revision))
                url += "#" + revision.Trim();
            return url;
        }

        // ---- environment probing --------------------------------------------------------------

        private static string PackagesDirectory(string projectRoot) =>
            Path.Combine(projectRoot, "Packages");

        private static UnityEditor.PackageManager.PackageInfo MainPackage()
        {
            try
            {
                return UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CascadePackageCatalogue).Assembly);
            }
            catch
            {
                return null;
            }
        }

        private static string MainPackageRevision()
        {
            var git = MainPackage()?.git;
            if (git == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(git.revision))
                return git.revision;
            return git.hash ?? string.Empty;
        }

        private static string ShortRevision(string revision) =>
            revision.Length > 7 ? revision.Substring(0, 7) : revision;

        /// <summary>
        /// Repository URL of this package. UPM exposes it through <c>packageId</c> ("name@url"), not on
        /// <c>GitInfo</c>; a git-installed package therefore tells us which fork it came from.
        /// </summary>
        private static string ResolveRepositoryUrl()
        {
            var packageId = MainPackage()?.packageId;
            if (!string.IsNullOrEmpty(packageId))
            {
                var at = packageId.IndexOf('@');
                if (at >= 0)
                {
                    var url = packageId.Substring(at + 1);
                    if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        var cut = url.IndexOfAny(new[] { '?', '#' });
                        if (cut >= 0)
                            url = url.Substring(0, cut);
                        return url.TrimEnd('/');
                    }
                }
            }

            return Load().repository;
        }

        private static string ResolvePackageRoot()
        {
            var package = MainPackage();
            if (package != null && !string.IsNullOrWhiteSpace(package.resolvedPath))
                return package.resolvedPath;

            // Fallback for the (unusual) case where UPM cannot resolve this assembly: walk up from Assets/../Packages.
            var assets = Application.dataPath;
            var candidate = Path.GetFullPath(Path.Combine(assets, "..", "..", "Cascade"));
            return Directory.Exists(candidate) ? candidate : assets;
        }

        private static string ResolveRepositoryRoot()
        {
            var parent = Directory.GetParent(PackageRoot)?.FullName;
            if (string.IsNullOrEmpty(parent))
                return null;

            return Directory.Exists(Path.Combine(parent, "Modules")) &&
                   Directory.Exists(Path.Combine(parent, "Integrations"))
                ? parent
                : null;
        }

        private static string ResolveProjectRoot() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }
}
