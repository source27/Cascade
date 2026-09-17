using System;
using System.IO;
using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>追加写入 persistentDataPath/cascade-desktop/logs/desktop_yyyyMMdd.log</summary>
    public static class FileLogger
    {
        static string _path;
        static readonly object _lock = new object();
        static bool _init;

        public static void EnsureInit()
        {
            if (_init)
                return;
            try
            {
                var dir = Path.Combine(Application.persistentDataPath, GameSettingsStore.RelativeDir, "logs");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                _path = Path.Combine(dir, $"desktop_{DateTime.Now:yyyyMMdd}.log");
                _init = true;
                Info($"=== FileLogger start {DateTime.Now:o} ===");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FileLogger] init: {e.Message}");
            }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message) => Write("ERROR", message);

        static void Write(string level, string message)
        {
            if (!_init)
                EnsureInit();
            if (string.IsNullOrEmpty(_path))
                return;
            try
            {
                var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}\n";
                lock (_lock)
                {
                    File.AppendAllText(_path, line);
                }
            }
            catch
            {
                // ignore IO errors
            }
        }
    }
}
