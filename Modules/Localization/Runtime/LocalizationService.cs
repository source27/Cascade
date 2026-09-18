using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

using Cascade.Service;
namespace Cascade.Modules.Localization
{
    public sealed class LocalizationService : ILocalizationService, IDisposable
    {
        public const string DefaultCatalogLocation = "localization_catalog";
        public const string LocaleSaveKey = "localization.locale";

        private readonly IResourceService _resources;
        private readonly ISaveService _save;
        private readonly ILogService _log;
        private readonly string _catalogLocation;
        private readonly SemaphoreSlim _switchLock = new SemaphoreSlim(1, 1);
        private readonly HashSet<string> _missingKeys = new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, string> _localeLocations;
        private Dictionary<string, string> _strings;
        private string[] _supportedLocales = Array.Empty<string>();
        private bool _disposed;

        public LocalizationService(
            IResourceService resources,
            ISaveService save,
            ILogService log,
            string catalogLocation = DefaultCatalogLocation)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _catalogLocation = string.IsNullOrWhiteSpace(catalogLocation)
                ? throw new ArgumentException("Catalog location is required.", nameof(catalogLocation))
                : catalogLocation;
        }

        public string CurrentLocale { get; private set; } = string.Empty;
        public IReadOnlyList<string> SupportedLocales => _supportedLocales;
        public event Action<string> LocaleChanged;

        public async UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            var catalogBytes = await _resources.LoadRawBytesAsync(_catalogLocation, cancellationToken);
            var catalog = LocalizationDataParser.ParseCatalog(catalogBytes);
            _localeLocations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in catalog.Locations)
                _localeLocations.Add(pair.Key, pair.Value);
            _supportedLocales = catalog.Locales;

            var saved = _save.GetString(LocaleSaveKey);
            var initialLocale = ResolveInitialLocale(
                saved,
                GetSystemLocale(Application.systemLanguage),
                catalog.DefaultLocale,
                _supportedLocales);
            await SetLocaleAsync(initialLocale, cancellationToken);
            LocalizationAccess.Bind(this);
        }

        public async UniTask SetLocaleAsync(string locale, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (_localeLocations == null)
                throw new InvalidOperationException("Localization service is not initialized.");

            var normalized = NormalizeLocaleCode(locale);
            if (!_localeLocations.TryGetValue(normalized, out var location))
                throw new ArgumentException($"Unsupported locale: {locale}", nameof(locale));
            if (string.Equals(CurrentLocale, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            await _switchLock.WaitAsync(cancellationToken);
            try
            {
                if (string.Equals(CurrentLocale, normalized, StringComparison.OrdinalIgnoreCase))
                    return;

                var bytes = await _resources.LoadRawBytesAsync(location, cancellationToken);
                var strings = LocalizationDataParser.ParseTable(bytes, normalized);

                _strings = strings;
                CurrentLocale = normalized;
                _missingKeys.Clear();
                _save.SetString(LocaleSaveKey, normalized);
                _save.Flush();
                NotifyLocaleChanged(normalized);
                _log.Info("Localization", $"Locale selected: {normalized}");
            }
            finally
            {
                _switchLock.Release();
            }
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            if (_strings != null && _strings.TryGetValue(key, out var value))
                return value;

            var missingId = $"{CurrentLocale}:{key}";
            if (_missingKeys.Add(missingId))
                _log.Warning("Localization", $"Missing key '{key}' for locale '{CurrentLocale}'.");
            return key;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            LocalizationAccess.Unbind(this);
            _switchLock.Dispose();
            LocaleChanged = null;
        }

        public static string NormalizeLocaleCode(string locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
                return string.Empty;

            var normalized = locale.Trim().Replace('_', '-');
            switch (normalized.ToLowerInvariant())
            {
                case "cn": return "zh-CN";
                case "tw": return "zh-TW";
                case "iw": return "he";
            }

            var parts = normalized.Split('-');
            if (parts.Length == 1)
                return parts[0].ToLowerInvariant();
            parts[0] = parts[0].ToLowerInvariant();
            parts[1] = parts[1].ToUpperInvariant();
            return string.Join("-", parts);
        }

        public static string ResolveInitialLocale(
            string savedLocale,
            string systemLocale,
            string defaultLocale,
            IReadOnlyList<string> supportedLocales)
        {
            if (supportedLocales == null || supportedLocales.Count == 0)
                throw new InvalidDataException("Localization catalog has no locales.");

            var supported = new HashSet<string>(supportedLocales, StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in new[] { savedLocale, systemLocale, defaultLocale })
            {
                var normalized = NormalizeLocaleCode(candidate);
                if (supported.Contains(normalized))
                    return normalized;
            }
            return supportedLocales[0];
        }

        private static string GetSystemLocale(SystemLanguage language)
        {
            switch (language)
            {
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified: return "zh-CN";
                case SystemLanguage.ChineseTraditional: return "zh-TW";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Turkish: return "tr";
                default: return "en";
            }
        }

        private void NotifyLocaleChanged(string locale)
        {
            var handlers = LocaleChanged;
            if (handlers == null)
                return;
            foreach (Action<string> handler in handlers.GetInvocationList())
            {
                try { handler(locale); }
                catch (Exception exception) { _log.Exception("Localization", exception, "LocaleChanged subscriber failed."); }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LocalizationService));
        }
    }
}
