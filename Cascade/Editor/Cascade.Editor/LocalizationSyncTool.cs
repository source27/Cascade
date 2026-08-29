using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cascade.Service;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Cascade.Editor
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

    public static class LocalizationSyncTool
    {
        public const string OutputRoot = "Assets/CascadeRes/Localization";

        public static string ConfigureProject()
        {
            LocalizationSyncSettings.GetOrCreate();
            return $"Configured {LocalizationSyncSettings.AssetPath}.";
        }

        [MenuItem("Cascade/更新多语言", priority = 110)]
        public static async void SyncFromMenu()
        {
            try
            {
                var count = await SyncAsync(LocalizationSyncSettings.GetOrCreate());
                EditorUtility.DisplayDialog("Cascade Localization", $"同步完成，共生成 {count} 个语言表。", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Cascade Localization", exception.Message, "OK");
            }
        }

        public static async Task<int> SyncAsync(LocalizationSyncSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            var enabledSources = settings.sources?.Where(source => source != null && source.enabled).ToList();
            if (enabledSources == null || enabledSources.Count == 0)
                throw new InvalidOperationException("No enabled localization sources are configured.");

            var merged = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);
            foreach (var source in enabledSources)
            {
                var csv = await DownloadAsync(ToCsvExportUrl(source.url));
                Merge(csv, source, merged);
            }

            var defaultLocale = LocalizationService.NormalizeLocaleCode(settings.defaultLocale);
            if (!merged.ContainsKey(defaultLocale))
                throw new InvalidDataException($"Default locale '{defaultLocale}' has no generated entries.");

            var outputs = BuildOutputs(defaultLocale, merged);
            WriteOutputs(outputs);
            Debug.Log($"Cascade Localization: generated {merged.Count} locale table(s) from {enabledSources.Count} source(s).");
            return merged.Count;
        }

        public static string ToCsvExportUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("Google Sheet URL is required.", nameof(url));
            if (url.IndexOf("/export?", StringComparison.OrdinalIgnoreCase) >= 0)
                return url;

            var sheet = Regex.Match(url, @"/spreadsheets/d/([^/]+)", RegexOptions.IgnoreCase);
            if (!sheet.Success)
                return url;
            var gid = Regex.Match(url, @"(?:[?&#])gid=(\d+)", RegexOptions.IgnoreCase);
            var gidValue = gid.Success ? gid.Groups[1].Value : "0";
            return $"https://docs.google.com/spreadsheets/d/{sheet.Groups[1].Value}/export?format=csv&gid={gidValue}";
        }

        public static void Merge(
            string csv,
            LocalizationCsvSource source,
            SortedDictionary<string, SortedDictionary<string, string>> merged)
        {
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

        private static async Task<string> DownloadAsync(string url)
        {
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 30;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException($"Localization download failed: {request.error} ({url})");
                return request.downloadHandler.text;
            }
        }

        private static Dictionary<string, string> BuildOutputs(
            string defaultLocale,
            SortedDictionary<string, SortedDictionary<string, string>> merged)
        {
            var outputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var locales = new List<LocalizationCatalogLocaleFile>();
            foreach (var language in merged)
            {
                if (language.Value.Count == 0)
                    continue;
                var location = "localization_" + language.Key.ToLowerInvariant();
                locales.Add(new LocalizationCatalogLocaleFile { code = language.Key, location = location });
                outputs[$"{OutputRoot}/{location}.json"] = JsonUtility.ToJson(
                    new LocalizationTableFile
                    {
                        entries = language.Value.Select(pair => new LocalizationEntryFile { id = pair.Key, value = pair.Value }).ToArray()
                    },
                    true) + "\n";
            }

            outputs[$"{OutputRoot}/localization_catalog.json"] = JsonUtility.ToJson(
                new LocalizationCatalogFile { defaultLocale = defaultLocale, locales = locales.ToArray() },
                true) + "\n";
            return outputs;
        }

        private static void WriteOutputs(Dictionary<string, string> outputs)
        {
            EnsureFolder(OutputRoot);
            foreach (var output in outputs)
                File.WriteAllText(Path.GetFullPath(output.Key), output.Value, new UTF8Encoding(false));

            var expected = new HashSet<string>(outputs.Keys.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(Path.GetFullPath(OutputRoot), "localization_*.json"))
            {
                var assetPath = NormalizePath(Path.GetRelativePath(Directory.GetCurrentDirectory(), file));
                if (!expected.Contains(assetPath))
                    AssetDatabase.DeleteAsset(assetPath);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = NormalizePath(Path.GetDirectoryName(path));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string NormalizeNewlines(string value) => value.Replace("\r\n", "\n").Replace('\r', '\n');
        private static string NormalizePath(string path) => path?.Replace('\\', '/');

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
