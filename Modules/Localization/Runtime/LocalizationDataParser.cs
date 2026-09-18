using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Cascade.Modules.Localization
{
    public static class LocalizationDataParser
    {
        public readonly struct Catalog
        {
            public Catalog(string defaultLocale, string[] locales, IReadOnlyDictionary<string, string> locations)
            {
                DefaultLocale = defaultLocale;
                Locales = locales;
                Locations = locations;
            }

            public string DefaultLocale { get; }
            public string[] Locales { get; }
            public IReadOnlyDictionary<string, string> Locations { get; }
        }

        public static Catalog ParseCatalog(byte[] bytes)
        {
            var dto = JsonUtility.FromJson<LocalizationCatalogDto>(Decode(bytes));
            if (dto == null || dto.locales == null || dto.locales.Length == 0)
                throw new InvalidDataException("Localization catalog has no locales.");

            var locations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var locales = new List<string>(dto.locales.Length);
            foreach (var locale in dto.locales)
            {
                var code = LocalizationService.NormalizeLocaleCode(locale?.code);
                if (string.IsNullOrEmpty(code) || string.IsNullOrWhiteSpace(locale?.location))
                    throw new InvalidDataException("Localization catalog contains an invalid locale.");
                if (locations.ContainsKey(code))
                    throw new InvalidDataException($"Localization catalog contains duplicate locale '{code}'.");
                locations.Add(code, locale.location.Trim());
                locales.Add(code);
            }

            var defaultLocale = LocalizationService.NormalizeLocaleCode(dto.defaultLocale);
            if (!locations.ContainsKey(defaultLocale))
                throw new InvalidDataException($"Default locale '{defaultLocale}' is not in the catalog.");
            return new Catalog(defaultLocale, locales.ToArray(), locations);
        }

        public static Dictionary<string, string> ParseTable(byte[] bytes, string locale)
        {
            var dto = JsonUtility.FromJson<LocalizationTableDto>(Decode(bytes));
            if (dto == null || dto.entries == null)
                throw new InvalidDataException($"Localization table '{locale}' is invalid.");

            var strings = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in dto.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id))
                    throw new InvalidDataException($"Localization table '{locale}' contains an empty key.");
                if (!strings.TryAdd(entry.id, entry.value ?? string.Empty))
                    throw new InvalidDataException($"Localization table '{locale}' contains duplicate key '{entry.id}'.");
            }
            return strings;
        }

        public static string Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                throw new InvalidDataException("Localization data is empty.");
            return Encoding.UTF8.GetString(bytes);
        }

        [Serializable]
        private sealed class LocalizationCatalogDto
        {
            public string defaultLocale;
            public LocalizationCatalogLocaleDto[] locales;
        }

        [Serializable]
        private sealed class LocalizationCatalogLocaleDto
        {
            public string code;
            public string location;
        }

        [Serializable]
        private sealed class LocalizationTableDto
        {
            public LocalizationEntryDto[] entries;
        }

        [Serializable]
        private sealed class LocalizationEntryDto
        {
            public string id;
            public string value;
        }
    }
}
