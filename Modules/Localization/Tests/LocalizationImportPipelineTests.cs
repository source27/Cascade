using System;
using System.Collections.Generic;
using System.Text;
using Cascade.Modules.Localization.Editor;
using Cascade.Service;
using NUnit.Framework;

using Cascade.Modules.Localization;
namespace Cascade.Modules.Localization.Tests
{
    public sealed class LocalizationImportPipelineTests
    {
        [Test]
        public void GoogleSheetUrlAndF5CsvLayoutAreSupported()
        {
            const string editUrl = "https://docs.google.com/spreadsheets/d/1QbL_teiUTtvHrp_gOlNiyW92wuKS5NwPBwBJjwWyago/edit?gid=0#gid=0";
            Assert.That(
                LocalizationGoogleSheetAdapter.ToCsvExportUrl(editUrl),
                Is.EqualTo("https://docs.google.com/spreadsheets/d/1QbL_teiUTtvHrp_gOlNiyW92wuKS5NwPBwBJjwWyago/export?format=csv&gid=0"));

            const string csv = "note\r\nnote\r\nKey,EN,CN\r\n\r\n\r\n\r\nwelcome,\"Hello, \"\"Captain\"\"\",\"first\r\nsecond\"\r\n";
            var merged = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
            LocalizationImportPipeline.MergeCsv(
                csv,
                new LocalizationCsvSource { name = "test", headerRow = 3, dataStartRow = 7 },
                merged);

            Assert.That(merged["en"]["welcome"], Is.EqualTo("Hello, \"Captain\""));
            Assert.That(merged["zh-CN"]["welcome"], Is.EqualTo("first\nsecond"));
        }

        [Test]
        public void ImportPipelineBuildsRuntimeCompatibleCatalogAndTablesFromCsvFixtures()
        {
            const string mainCsv =
                "note\r\nnote\r\nKey,EN,CN\r\n\r\n\r\n\r\nwelcome,\"Hello, \"\"Captain\"\"\",\"你好\"\r\nbye,Goodbye,再见\r\n";
            const string extraCsv =
                "note\r\nnote\r\nKey,EN,CN\r\n\r\n\r\n\r\nextra,Extra,额外\r\n";
            const string outputRoot = "Assets/Content/I18n";

            var artifacts = LocalizationImportPipeline.BuildArtifacts(
                defaultLocale: "en",
                contributions: new[]
                {
                    new LocalizationCsvContribution(
                        new LocalizationCsvSource { name = "main", headerRow = 3, dataStartRow = 7 },
                        mainCsv),
                    new LocalizationCsvContribution(
                        new LocalizationCsvSource { name = "extra", headerRow = 3, dataStartRow = 7 },
                        extraCsv),
                    new LocalizationCsvContribution(
                        new LocalizationCsvSource { name = "disabled-ignored", enabled = false, headerRow = 3, dataStartRow = 7 },
                        "note\r\nnote\r\nKey,EN,CN\r\n\r\n\r\n\r\nignored,X,Y\r\n")
                },
                outputRoot: outputRoot);

            Assert.That(artifacts.ContainsKey($"{outputRoot}/localization_catalog.json"), Is.True);
            Assert.That(artifacts.ContainsKey($"{outputRoot}/localization_en.json"), Is.True);
            Assert.That(artifacts.ContainsKey($"{outputRoot}/localization_zh-cn.json"), Is.True);

            var catalog = LocalizationDataParser.ParseCatalog(
                Encoding.UTF8.GetBytes(artifacts[$"{outputRoot}/localization_catalog.json"]));
            Assert.That(catalog.DefaultLocale, Is.EqualTo("en"));
            Assert.That(catalog.Locales, Is.EquivalentTo(new[] { "en", "zh-CN" }));
            Assert.That(catalog.Locations["en"], Is.EqualTo("localization_en"));
            Assert.That(catalog.Locations["zh-CN"], Is.EqualTo("localization_zh-cn"));

            var en = LocalizationDataParser.ParseTable(
                Encoding.UTF8.GetBytes(artifacts[$"{outputRoot}/localization_en.json"]),
                "en");
            var zh = LocalizationDataParser.ParseTable(
                Encoding.UTF8.GetBytes(artifacts[$"{outputRoot}/localization_zh-cn.json"]),
                "zh-CN");

            Assert.That(en["welcome"], Is.EqualTo("Hello, \"Captain\""));
            Assert.That(en["bye"], Is.EqualTo("Goodbye"));
            Assert.That(en["extra"], Is.EqualTo("Extra"));
            Assert.That(en.ContainsKey("ignored"), Is.False);
            Assert.That(zh["welcome"], Is.EqualTo("你好"));
            Assert.That(zh["bye"], Is.EqualTo("再见"));
            Assert.That(zh["extra"], Is.EqualTo("额外"));
        }

        [Test]
        public void NormalizeOutputRootRejectsNonAssetsPathsAndDefaultsWhenEmpty()
        {
            Assert.That(
                LocalizationImportPipeline.NormalizeOutputRoot(null),
                Is.EqualTo(LocalizationImportPipeline.DefaultOutputRoot));
            Assert.That(
                LocalizationImportPipeline.NormalizeOutputRoot("  Assets/GameRes/Loc  "),
                Is.EqualTo("Assets/GameRes/Loc"));
            Assert.Throws<ArgumentException>(() => LocalizationImportPipeline.NormalizeOutputRoot("GameRes/Loc"));
        }
    }
}
