using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;
using UnityEditor;
using UnityEngine;

namespace Cascade.Editor
{
    // Edit-mode ILocalizationService: loads Assets JSON directly, never writes player save.
    public sealed class EditorLocalizationPreview : ILocalizationService
    {
        public const string AssetRoot = "Assets/CascadeRes/Localization";
        public const string CatalogAssetPath = AssetRoot + "/localization_catalog.json";
        public const string EditorLocalePrefsKey = "Cascade.Localization.EditorLocale";
        public const string KeyMode = "key";

        private readonly Dictionary<string, string> _localeFiles =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _supported = new List<string>();
        private readonly List<string> _dropdownOptions = new List<string>();
        private Dictionary<string, string> _strings =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private string _defaultLocale = "en";
        private bool _showKeys;

        public string CurrentLocale { get; private set; } = string.Empty;
        public IReadOnlyList<string> SupportedLocales => _supported;
        public IReadOnlyList<string> DropdownOptions => _dropdownOptions;
        public event Action<string> LocaleChanged;
        public bool IsLoaded { get; private set; }
        public bool ShowKeys => _showKeys;

        public bool Reload()
        {
            try
            {
                if (!File.Exists(CatalogAssetPath))
                {
                    IsLoaded = false;
                    _supported.Clear();
                    _dropdownOptions.Clear();
                    _localeFiles.Clear();
                    _strings = new Dictionary<string, string>(StringComparer.Ordinal);
                    CurrentLocale = string.Empty;
                    _showKeys = false;
                    return false;
                }

                var catalogBytes = File.ReadAllBytes(CatalogAssetPath);
                var catalog = LocalizationDataParser.ParseCatalog(catalogBytes);
                _defaultLocale = catalog.DefaultLocale;
                _supported.Clear();
                _supported.AddRange(catalog.Locales);
                _dropdownOptions.Clear();
                _dropdownOptions.Add(KeyMode);
                _dropdownOptions.AddRange(catalog.Locales);
                _localeFiles.Clear();
                foreach (var pair in catalog.Locations)
                    _localeFiles[pair.Key] = ResolveTablePath(pair.Value);

                var preferred = EditorPrefs.GetString(EditorLocalePrefsKey, _defaultLocale);
                if (IsKeyMode(preferred))
                {
                    EnterKeyMode(notify: false);
                }
                else
                {
                    preferred = LocalizationService.NormalizeLocaleCode(preferred);
                    if (!_localeFiles.ContainsKey(preferred))
                        preferred = _defaultLocale;
                    LoadLocale(preferred, notify: false);
                }

                IsLoaded = true;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                IsLoaded = false;
                return false;
            }
        }

        public UniTask InitializeAsync(CancellationToken cancellationToken = default)
        {
            Reload();
            return UniTask.CompletedTask;
        }

        public UniTask SetLocaleAsync(string locale, CancellationToken cancellationToken = default)
        {
            SetLocale(locale);
            return UniTask.CompletedTask;
        }

        public void SetLocale(string locale)
        {
            if (!IsLoaded && !Reload())
                return;

            if (IsKeyMode(locale))
            {
                if (_showKeys && string.Equals(CurrentLocale, KeyMode, StringComparison.Ordinal))
                    return;
                EnterKeyMode(notify: true);
                EditorPrefs.SetString(EditorLocalePrefsKey, KeyMode);
                return;
            }

            var normalized = LocalizationService.NormalizeLocaleCode(locale);
            if (string.IsNullOrEmpty(normalized) || !_localeFiles.ContainsKey(normalized))
                return;
            if (!_showKeys && string.Equals(CurrentLocale, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            LoadLocale(normalized, notify: true);
            EditorPrefs.SetString(EditorLocalePrefsKey, normalized);
        }

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;
            if (_showKeys)
                return key;
            return _strings != null && _strings.TryGetValue(key, out var value) ? value : key;
        }

        private void EnterKeyMode(bool notify)
        {
            _showKeys = true;
            CurrentLocale = KeyMode;
            if (notify)
                LocaleChanged?.Invoke(KeyMode);
        }

        private void LoadLocale(string locale, bool notify)
        {
            if (!_localeFiles.TryGetValue(locale, out var path) || !File.Exists(path))
                throw new FileNotFoundException("Localization table not found.", path);

            var bytes = File.ReadAllBytes(path);
            _strings = LocalizationDataParser.ParseTable(bytes, locale);
            _showKeys = false;
            CurrentLocale = locale;
            if (notify)
                LocaleChanged?.Invoke(locale);
        }

        private static bool IsKeyMode(string locale)
        {
            return string.Equals(locale, KeyMode, StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveTablePath(string location)
        {
            var fileName = location;
            if (fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return $"{AssetRoot}/{fileName}";
            return $"{AssetRoot}/{fileName}.json";
        }
    }
}
