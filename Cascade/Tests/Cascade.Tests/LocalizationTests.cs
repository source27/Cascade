using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Editor;
using Cascade.Service;
using NUnit.Framework;

namespace Cascade.Tests
{
    public sealed class LocalizationTests
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
                });

            Assert.That(artifacts.ContainsKey($"{LocalizationImportPipeline.OutputRoot}/localization_catalog.json"), Is.True);
            Assert.That(artifacts.ContainsKey($"{LocalizationImportPipeline.OutputRoot}/localization_en.json"), Is.True);
            Assert.That(artifacts.ContainsKey($"{LocalizationImportPipeline.OutputRoot}/localization_zh-cn.json"), Is.True);

            var catalog = LocalizationDataParser.ParseCatalog(
                Encoding.UTF8.GetBytes(artifacts[$"{LocalizationImportPipeline.OutputRoot}/localization_catalog.json"]));
            Assert.That(catalog.DefaultLocale, Is.EqualTo("en"));
            Assert.That(catalog.Locales, Is.EquivalentTo(new[] { "en", "zh-CN" }));
            Assert.That(catalog.Locations["en"], Is.EqualTo("localization_en"));
            Assert.That(catalog.Locations["zh-CN"], Is.EqualTo("localization_zh-cn"));

            var en = LocalizationDataParser.ParseTable(
                Encoding.UTF8.GetBytes(artifacts[$"{LocalizationImportPipeline.OutputRoot}/localization_en.json"]),
                "en");
            var zh = LocalizationDataParser.ParseTable(
                Encoding.UTF8.GetBytes(artifacts[$"{LocalizationImportPipeline.OutputRoot}/localization_zh-cn.json"]),
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
        public void RuntimeSelectsSavedLocaleAndKeepsItWhenSwitchFails()
        {
            var resources = new FakeResourceService();
            resources.Add("localization_catalog", "{\"defaultLocale\":\"en\",\"locales\":[{\"code\":\"en\",\"location\":\"localization_en\"},{\"code\":\"fr\",\"location\":\"localization_fr\"}]}");
            resources.Add("localization_fr", "{\"entries\":[{\"id\":\"hello\",\"value\":\"Bonjour\"}]}");
            var save = new FakeSaveService();
            save.SetString(LocalizationService.LocaleSaveKey, "fr");
            var log = new FakeLogService();
            using (var service = new LocalizationService(resources, save, log))
            {
                service.InitializeAsync().GetAwaiter().GetResult();

                Assert.That(service.CurrentLocale, Is.EqualTo("fr"));
                Assert.That(service.Get("hello"), Is.EqualTo("Bonjour"));
                Assert.That(service.Get("missing"), Is.EqualTo("missing"));
                Assert.That(service.Get("missing"), Is.EqualTo("missing"));
                Assert.That(log.WarningCount, Is.EqualTo(1));

                FileNotFoundException loadFailure = null;
                try
                {
                    service.SetLocaleAsync("en").GetAwaiter().GetResult();
                }
                catch (FileNotFoundException exception)
                {
                    loadFailure = exception;
                }
                Assert.That(loadFailure, Is.Not.Null);
                Assert.That(service.CurrentLocale, Is.EqualTo("fr"));
                Assert.That(save.GetString(LocalizationService.LocaleSaveKey), Is.EqualTo("fr"));

                resources.Add("localization_en", "{\"entries\":[{\"id\":\"hello\",\"value\":\"Hello\"}]}");
                var changed = string.Empty;
                service.LocaleChanged += locale => changed = locale;
                service.SetLocaleAsync("en").GetAwaiter().GetResult();
                Assert.That(changed, Is.EqualTo("en"));
                Assert.That(service.Get("hello"), Is.EqualTo("Hello"));
            }
        }

        [Test]
        public void LocalizationDataParserReadsCatalogAndTable()
        {
            var catalog = LocalizationDataParser.ParseCatalog(
                Encoding.UTF8.GetBytes(
                    "{\"defaultLocale\":\"en\",\"locales\":[{\"code\":\"en\",\"location\":\"localization_en\"},{\"code\":\"zh-CN\",\"location\":\"localization_zh-cn\"}]}"));
            Assert.That(catalog.DefaultLocale, Is.EqualTo("en"));
            Assert.That(catalog.Locales, Is.EquivalentTo(new[] { "en", "zh-CN" }));
            Assert.That(catalog.Locations["zh-CN"], Is.EqualTo("localization_zh-cn"));

            var table = LocalizationDataParser.ParseTable(
                Encoding.UTF8.GetBytes("{\"entries\":[{\"id\":\"hello\",\"value\":\"Hello\"}]}"),
                "en");
            Assert.That(table["hello"], Is.EqualTo("Hello"));
        }

        [Test]
        public void LocalizationAccessBindsForAotWidgetsAndUnbindsOnDispose()
        {
            LocalizationAccess.Unbind();
            Assert.That(LocalizationAccess.IsBound, Is.False);
            Assert.That(LocalizationAccess.Get("hello"), Is.EqualTo("hello"));

            var resources = new FakeResourceService();
            resources.Add("localization_catalog", "{\"defaultLocale\":\"en\",\"locales\":[{\"code\":\"en\",\"location\":\"localization_en\"}]}");
            resources.Add("localization_en", "{\"entries\":[{\"id\":\"hello\",\"value\":\"Hello\"}]}");
            var service = new LocalizationService(resources, new FakeSaveService(), new FakeLogService());
            service.InitializeAsync().GetAwaiter().GetResult();
            LocalizationAccess.Bind(service);

            Assert.That(LocalizationAccess.IsBound, Is.True);
            Assert.That(LocalizationAccess.Current, Is.SameAs(service));
            Assert.That(LocalizationAccess.Get("hello"), Is.EqualTo("Hello"));

            service.Dispose();
            Assert.That(LocalizationAccess.IsBound, Is.False);
            Assert.That(LocalizationAccess.Get("hello"), Is.EqualTo("hello"));
        }

        private sealed class FakeResourceService : IResourceService
        {
            private readonly Dictionary<string, byte[]> _raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            public bool IsInitialized => true;

            public void Add(string location, string json) => _raw[location] = Encoding.UTF8.GetBytes(json);
            public UniTask InitializeAsync(ResourceInitOptions options, CancellationToken cancellationToken = default) => UniTask.CompletedTask;
            public UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default) where T : UnityEngine.Object =>
                UniTask.FromException<IAssetHandle<T>>(new NotSupportedException());

            public UniTask<ISceneHandle> LoadSceneAsync(
                string location,
                ResourceSceneLoadMode loadMode = ResourceSceneLoadMode.Single,
                CancellationToken cancellationToken = default) =>
                UniTask.FromException<ISceneHandle>(new NotSupportedException());

            public UniTask<byte[]> LoadRawBytesAsync(string location, CancellationToken cancellationToken = default)
            {
                return _raw.TryGetValue(location, out var bytes)
                    ? UniTask.FromResult(bytes)
                    : UniTask.FromException<byte[]>(new FileNotFoundException(location));
            }

            public void UnloadUnused() { }
        }

        private sealed class FakeSaveService : ISaveService
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();
            public string GetString(string key, string defaultValue = "") => _values.TryGetValue(key, out var value) ? value : defaultValue;
            public void SetString(string key, string value) => _values[key] = value;
            public void Flush() { }
        }

        private sealed class FakeLogService : ILogService
        {
            public bool Enabled { get; set; } = true;
            public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
            public int WarningCount { get; private set; }
            public void Trace(string category, string message) { }
            public void Info(string category, string message) { }
            public void Warning(string category, string message) => WarningCount++;
            public void Error(string category, string message) { }
            public void Exception(string category, Exception exception, string message = null) { }
        }
    }
}
