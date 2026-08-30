using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cascade.Service;
using UnityEngine;

namespace Cascade.Modules.LocalizationTools.Editor
{
    public static class LocalizationCsvParser
    {
        public static List<List<string>> Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            var inQuotes = false;
            var fieldStarted = false;

            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];
                if (inQuotes)
                {
                    if (current == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else if (current == '\r')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '\n')
                            i++;
                        field.Append('\n');
                    }
                    else
                    {
                        field.Append(current);
                    }
                    continue;
                }

                if (current == '"' && !fieldStarted)
                {
                    inQuotes = true;
                    fieldStarted = true;
                }
                else if (current == ',')
                {
                    row.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                }
                else if (current == '\r' || current == '\n')
                {
                    if (current == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    row.Add(field.ToString());
                    rows.Add(row);
                    row = new List<string>();
                    field.Clear();
                    fieldStarted = false;
                }
                else
                {
                    field.Append(current);
                    fieldStarted = true;
                }
            }

            if (inQuotes)
                throw new FormatException("CSV contains an unterminated quoted field.");
            if (fieldStarted || field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }

    /// <summary>
    /// One CSV contribution into the localization import pipeline (Sheet/Excel/etc. adapters produce these).
    /// </summary>
    public readonly struct LocalizationCsvContribution
    {
        public LocalizationCsvContribution(LocalizationCsvSource source, string csv)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Csv = csv ?? throw new ArgumentNullException(nameof(csv));
        }

        public LocalizationCsvSource Source { get; }
        public string Csv { get; }
    }

    /// <summary>
    /// Pure authoring seam: tabular CSV contributions → runtime catalog/locale JSON artifacts.
    /// No network, no AssetDatabase.
    /// </summary>
    public static class LocalizationImportPipeline
    {
        /// <summary>Neutral default disk folder when settings omit outputRoot. Not a runtime location.</summary>
        public const string DefaultOutputRoot = "Assets/Localization";

        public static IReadOnlyDictionary<string, string> BuildArtifacts(
            string defaultLocale,
            IReadOnlyList<LocalizationCsvContribution> contributions,
            string outputRoot = null)
        {
            if (contributions == null)
                throw new ArgumentNullException(nameof(contributions));

            var root = NormalizeOutputRoot(outputRoot);
            var enabled = contributions.Where(contribution => contribution.Source != null && contribution.Source.enabled).ToList();
            if (enabled.Count == 0)
                throw new InvalidOperationException("No enabled localization sources are configured.");

            var merged = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
            foreach (var contribution in enabled)
                MergeCsv(contribution.Csv, contribution.Source, merged);

            var normalizedDefault = LocalizationService.NormalizeLocaleCode(defaultLocale);
            if (string.IsNullOrEmpty(normalizedDefault))
                throw new InvalidDataException("Default locale is required.");
            if (!merged.ContainsKey(normalizedDefault) || merged[normalizedDefault].Count == 0)
                throw new InvalidDataException($"Default locale '{normalizedDefault}' has no generated entries.");

            return BuildOutputMap(normalizedDefault, merged, root);
        }

        public static string NormalizeOutputRoot(string outputRoot)
        {
            if (string.IsNullOrWhiteSpace(outputRoot))
                return DefaultOutputRoot;
            var normalized = outputRoot.Replace('\\', '/').Trim().TrimEnd('/');
            if (normalized.StartsWith("./", StringComparison.Ordinal))
                normalized = normalized.Substring(2);
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"Localization output root must be under Assets/ (got '{outputRoot}').",
                    nameof(outputRoot));
            return normalized;
        }

        public static void MergeCsv(
            string csv,
            LocalizationCsvSource source,
            SortedDictionary<string, SortedDictionary<string, string>> merged)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (merged == null)
                throw new ArgumentNullException(nameof(merged));
            if (source.headerRow < 1 || source.dataStartRow < 1)
                throw new InvalidDataException($"Source '{source.name}' row numbers must be positive.");

            var rows = LocalizationCsvParser.Parse(csv);
            var headerIndex = source.headerRow - 1;
            if (headerIndex >= rows.Count)
                throw new InvalidDataException($"Source '{source.name}' has no header row {source.headerRow}.");

            var header = rows[headerIndex];
            if (header.Count < 2)
                throw new InvalidDataException($"Source '{source.name}' must contain a key column and at least one locale.");

            var localeColumns = new Dictionary<int, string>();
            var seenLocales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var column = 1; column < header.Count; column++)
            {
                var locale = LocalizationService.NormalizeLocaleCode(header[column]);
                if (string.IsNullOrEmpty(locale))
                    continue;
                if (!seenLocales.Add(locale))
                    throw new InvalidDataException($"Source '{source.name}' contains duplicate locale column '{locale}'.");
                localeColumns.Add(column, locale);
                if (!merged.ContainsKey(locale))
                    merged.Add(locale, new SortedDictionary<string, string>(StringComparer.Ordinal));
            }
            if (localeColumns.Count == 0)
                throw new InvalidDataException($"Source '{source.name}' has no locale columns.");

            for (var rowIndex = source.dataStartRow - 1; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (row.Count == 0)
                    continue;
                var key = row[0].Trim();
                if (string.IsNullOrEmpty(key))
                    continue;

                foreach (var localeColumn in localeColumns)
                {
                    if (localeColumn.Key >= row.Count || string.IsNullOrEmpty(row[localeColumn.Key]))
                        continue;
                    merged[localeColumn.Value][key] = NormalizeNewlines(row[localeColumn.Key]);
                }
            }
        }

        private static IReadOnlyDictionary<string, string> BuildOutputMap(
            string defaultLocale,
            SortedDictionary<string, SortedDictionary<string, string>> merged,
            string outputRoot)
        {
            var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var locales = new List<LocalizationCatalogLocaleFile>();
            foreach (var language in merged)
            {
                if (language.Value.Count == 0)
                    continue;
                var location = "localization_" + language.Key.ToLowerInvariant();
                locales.Add(new LocalizationCatalogLocaleFile { code = language.Key, location = location });
                outputs[$"{outputRoot}/{location}.json"] = JsonUtility.ToJson(
                    new LocalizationTableFile
                    {
                        entries = language.Value
                            .Select(pair => new LocalizationEntryFile { id = pair.Key, value = pair.Value })
                            .ToArray()
                    },
                    true) + "\n";
            }

            outputs[$"{outputRoot}/localization_catalog.json"] = JsonUtility.ToJson(
                new LocalizationCatalogFile { defaultLocale = defaultLocale, locales = locales.ToArray() },
                true) + "\n";
            return outputs;
        }

        private static string NormalizeNewlines(string value) =>
            value.Replace("\r\n", "\n").Replace('\r', '\n');

        [Serializable]
        private sealed class LocalizationCatalogFile
        {
            public string defaultLocale;
            public LocalizationCatalogLocaleFile[] locales;
        }

        [Serializable]
        private sealed class LocalizationCatalogLocaleFile
        {
            public string code;
            public string location;
        }

        [Serializable]
        private sealed class LocalizationTableFile
        {
            public LocalizationEntryFile[] entries;
        }

        [Serializable]
        private sealed class LocalizationEntryFile
        {
            public string id;
            public string value;
        }
    }
}
