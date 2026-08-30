using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;
using NUnit.Framework;

namespace Cascade.Tests
{
    public sealed class LocalizationTests
    {
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
        }

        private sealed class FakeResourceService : IResourceService
        {
            private readonly Dictionary<string, byte[]> _raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            public bool IsInitialized => true;

            public void Add(string location, string json) => _raw[location] = Encoding.UTF8.GetBytes(json);

            public UniTask InitializeAsync(ResourceInitOptions options, CancellationToken cancellationToken = default) =>
                UniTask.CompletedTask;

            public UniTask<IAssetHandle<T>> LoadAssetAsync<T>(string location, CancellationToken cancellationToken = default)
                where T : UnityEngine.Object =>
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
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

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
