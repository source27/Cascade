using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace Cascade.Editor
{
    /// <summary>
    /// Tracks resource package sequence per application version in ProjectSettings/CascadePackageVersion.json.
    /// Format: {appVersion}_{sequence}, e.g. 0.0.1_1, 0.0.1_2, 0.0.2_1.
    /// </summary>
    public static class PackageVersionTracker
    {
        public const string RelativePath = "ProjectSettings/CascadePackageVersion.json";

        [Serializable]
        private sealed class State
        {
            public string applicationVersion = string.Empty;
            public int sequence;
        }

        public static string FilePath
        {
            get
            {
                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                                  ?? throw new InvalidOperationException("Unable to resolve project root.");
                return Path.Combine(projectRoot, RelativePath.Replace('/', Path.DirectorySeparatorChar));
            }
        }

        public static string PeekNext(string applicationVersion)
        {
            var appVersion = NormalizeAppVersion(applicationVersion);
            var state = Load();
            var next = string.Equals(state.applicationVersion, appVersion, StringComparison.Ordinal)
                ? state.sequence + 1
                : 1;
            if (next < 1)
                next = 1;
            return Format(appVersion, next);
        }

        public static void CommitBuiltVersion(string packageVersion)
        {
            if (!TryParse(packageVersion, out var appVersion, out var sequence))
                return;
            if (sequence < 1)
                return;

            var state = Load();
            if (string.Equals(state.applicationVersion, appVersion, StringComparison.Ordinal) &&
                sequence <= state.sequence)
            {
                return;
            }

            Save(new State
            {
                applicationVersion = appVersion,
                sequence = sequence
            });
        }

        public static bool TryParse(string packageVersion, out string applicationVersion, out int sequence)
        {
            applicationVersion = null;
            sequence = 0;
            if (string.IsNullOrWhiteSpace(packageVersion))
                return false;

            var value = packageVersion.Trim();
            var separator = value.LastIndexOf('_');
            if (separator <= 0 || separator >= value.Length - 1)
                return false;

            applicationVersion = value.Substring(0, separator);
            if (string.IsNullOrWhiteSpace(applicationVersion))
                return false;

            return int.TryParse(
                       value.Substring(separator + 1),
                       NumberStyles.Integer,
                       CultureInfo.InvariantCulture,
                       out sequence) &&
                   sequence > 0;
        }

        public static string Format(string applicationVersion, int sequence)
        {
            return $"{NormalizeAppVersion(applicationVersion)}_{sequence}";
        }

        private static State Load()
        {
            var path = FilePath;
            if (!File.Exists(path))
                return new State();

            try
            {
                var json = File.ReadAllText(path);
                var state = JsonUtility.FromJson<State>(json);
                if (state == null)
                    return new State();
                if (string.IsNullOrWhiteSpace(state.applicationVersion))
                    state.applicationVersion = string.Empty;
                if (state.sequence < 0)
                    state.sequence = 0;
                return state;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PackageVersionTracker] Failed to read {RelativePath}: {exception.Message}");
                return new State();
            }
        }

        private static void Save(State state)
        {
            var path = FilePath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var json = JsonUtility.ToJson(state, true);
            File.WriteAllText(path, json + Environment.NewLine);
            Debug.Log($"[PackageVersionTracker] Saved {state.applicationVersion}_{state.sequence} → {RelativePath}");
        }

        private static string NormalizeAppVersion(string applicationVersion)
        {
            return string.IsNullOrWhiteSpace(applicationVersion)
                ? "0.0.0"
                : applicationVersion.Trim();
        }
    }
}
