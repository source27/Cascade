using System;
using System.IO;
using System.Linq;
using Cascade.Editor;
using NUnit.Framework;

namespace Cascade.Tests
{
    /// <summary>
    /// The 集成与模块 window relies on an embedded catalogue that must mirror the repository layout
    /// (scripts/validate-upm.mjs enforces the same on CI) and on two URL builders whose exact shape
    /// decides whether UPM can resolve the package.
    /// </summary>
    public sealed class PackageCatalogueTests
    {
        [Test]
        public void CatalogueLoadsAndMatchesPackagesOnDisk()
        {
            var catalogue = CascadePackageCatalogue.Load(forceReload: true);

            Assert.That(catalogue.packages, Is.Not.Empty, "PackageCatalogue.json produced no entries");
            Assert.That(catalogue.repository, Is.Not.Empty);

            foreach (var entry in catalogue.packages)
            {
                Assert.That(entry.name, Is.Not.Empty);
                Assert.That(entry.displayName, Is.Not.Empty);
                Assert.That(entry.when, Is.Not.Empty);
                Assert.That(entry.path, Is.Not.Empty);

                if (!CascadePackageCatalogue.IsLocalMonorepo)
                    continue;

                var directory = Path.Combine(CascadePackageCatalogue.RepositoryRoot, entry.path);
                Assert.That(Directory.Exists(directory), Is.True, $"missing directory for {entry.path}");

                var manifest = File.ReadAllText(Path.Combine(directory, "package.json"));
                Assert.That(manifest, Does.Contain($"\"{entry.name}\""),
                    $"{entry.path}/package.json does not declare {entry.name}");
            }
        }

        [Test]
        public void InstallReferenceIsUsableForTheCurrentInstallMode()
        {
            var entry = CascadePackageCatalogue.Load().packages.First();
            var url = CascadePackageCatalogue.ResolveInstallUrl(entry, out var mode);

            Assert.That(mode, Is.Not.Empty);
            if (CascadePackageCatalogue.IsLocalMonorepo)
            {
                Assert.That(url, Does.StartWith("file:"));
                Assert.That(url, Does.EndWith(entry.path));
            }
            else
            {
                Assert.That(url, Does.Contain("?path=" + entry.path));
            }
        }

        [Test]
        public void LocalUrlIsRelativeToThePackagesDirectory()
        {
            var packages = Path.Combine(CascadePackageCatalogue.ProjectRoot, "Packages");
            var target = Path.Combine(CascadePackageCatalogue.ProjectRoot, "..", "SomePackage");
            var url = CascadePackageCatalogue.BuildLocalUrl(packages, Path.GetFullPath(target));

            Assert.That(url, Is.EqualTo("file:../../SomePackage"));
            Assert.That(url, Does.Not.Contain("\\"));
        }

        [Test]
        public void GitUrlCarriesPathSelectorAndOptionalPin()
        {
            Assert.That(
                CascadePackageCatalogue.BuildGitUrl("https://github.com/source27/Cascade.git", "Modules/UI", "abc1234"),
                Is.EqualTo("https://github.com/source27/Cascade.git?path=Modules/UI#abc1234"));

            Assert.That(
                CascadePackageCatalogue.BuildGitUrl("https://github.com/source27/Cascade.git", "Modules/Audio", null),
                Is.EqualTo("https://github.com/source27/Cascade.git?path=Modules/Audio"));

            // A URL that already carries selectors is normalised, not concatenated.
            Assert.That(
                CascadePackageCatalogue.BuildGitUrl("https://host/repo.git?path=Cascade#deadbeef", "Integrations/Steam", ""),
                Is.EqualTo("https://host/repo.git?path=Integrations/Steam"));

            Assert.Throws<ArgumentException>(() => CascadePackageCatalogue.BuildGitUrl("", "Modules/UI", null));
            Assert.Throws<ArgumentException>(() => CascadePackageCatalogue.BuildGitUrl("https://host/r.git", "  ", null));
        }

        [Test]
        public void ManifestSnippetListsEntryAndPeersWithUrlsOnly()
        {
            var entry = new CascadePackageEntry
            {
                name = "com.source27.cascade.modules.uiextras",
                path = "Modules/UiExtras",
                peers = new[]
                {
                    new CascadePackagePeer { name = "com.annulusgames.lit-motion", url = "https://host/lit.git#1" },
                    new CascadePackagePeer { name = "com.rlabrecque.steamworks.net", url = "" }
                }
            };

            var snippet = CascadePackageCatalogue.BuildManifestSnippet(entry, "file:../../../Modules/UiExtras");

            Assert.That(snippet, Does.Contain("\"com.source27.cascade.modules.uiextras\": \"file:../../../Modules/UiExtras\""));
            Assert.That(snippet, Does.Contain("\"com.annulusgames.lit-motion\": \"https://host/lit.git#1\""));
            Assert.That(snippet, Does.Not.Contain("steamworks"), "peers without a URL must not be pasted as empty entries");
        }
    }
}
