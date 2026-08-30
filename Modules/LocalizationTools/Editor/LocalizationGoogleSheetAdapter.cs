using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine.Networking;

namespace Cascade.Modules.LocalizationTools.Editor
{
    /// <summary>
    /// Google Sheet transport adapter for the localization import pipeline (edit/export URL → CSV text).
    /// </summary>
    public static class LocalizationGoogleSheetAdapter
    {
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

        public static async Task<string> DownloadCsvAsync(string url)
        {
            var exportUrl = ToCsvExportUrl(url);
            using (var request = UnityWebRequest.Get(exportUrl))
            {
                request.timeout = 30;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Task.Yield();
                if (request.result != UnityWebRequest.Result.Success)
                    throw new IOException($"Localization download failed: {request.error} ({exportUrl})");
                return request.downloadHandler.text;
            }
        }
    }
}
