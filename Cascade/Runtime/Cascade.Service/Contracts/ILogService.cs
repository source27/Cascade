using System;

namespace Cascade.Service
{
    public interface ILogService
    {
        bool Enabled { get; set; }
        LogLevel MinimumLevel { get; set; }

        void Trace(string category, string message);
        void Debug(string category, string message);
        void Info(string category, string message);
        void Warning(string category, string message);
        void Error(string category, string message);
        void Exception(string category, Exception exception, string message = null);
    }
}
