using System;
using UnityEngine;

namespace Cascade.Service
{
    public sealed class UnityLogService : ILogService
    {
        public bool Enabled { get; set; } = true;
        public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

        public void Trace(string category, string message)
        {
            Write(LogLevel.Trace, category, message);
        }

        public void Info(string category, string message)
        {
            Write(LogLevel.Info, category, message);
        }

        public void Warning(string category, string message)
        {
            Write(LogLevel.Warning, category, message);
        }

        public void Error(string category, string message)
        {
            Write(LogLevel.Error, category, message);
        }

        public void Exception(string category, Exception exception, string message = null)
        {
            if (!ShouldLog(LogLevel.Error))
                return;

            var text = string.IsNullOrEmpty(message)
                ? Format(category, exception?.ToString() ?? "Exception")
                : Format(category, $"{message}\n{exception}");
            Debug.LogError(text);
        }

        private void Write(LogLevel level, string category, string message)
        {
            if (!ShouldLog(level))
                return;

            var text = Format(category, message);
            switch (level)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(text);
                    break;
                case LogLevel.Error:
                    Debug.LogError(text);
                    break;
                default:
                    Debug.Log(text);
                    break;
            }
        }

        private bool ShouldLog(LogLevel level)
        {
            return Enabled && level >= MinimumLevel && MinimumLevel != LogLevel.None && level != LogLevel.None;
        }

        private static string Format(string category, string message)
        {
            if (string.IsNullOrEmpty(category))
                return message ?? string.Empty;
            return $"[{category}] {message}";
        }
    }
}
