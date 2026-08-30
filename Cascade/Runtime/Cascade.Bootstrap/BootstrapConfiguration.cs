using Cascade.Service;

namespace Cascade.Bootstrap
{
    public sealed class BootstrapConfiguration
    {
        public BootstrapConfiguration(BootstrapEnvironment environment, string appVersion)
        {
            Environment = environment;
            AppVersion = appVersion ?? string.Empty;
        }

        public BootstrapEnvironment Environment { get; }
        public string AppVersion { get; }
        public ResourceInitOptions ResourceInitOptions { get; set; }

        public static BootstrapEnvironment ResolvePlayerEnvironment()
        {
#if UNITY_EDITOR
            return BootstrapEnvironment.Dev;
#else
            return BootstrapEnvironment.Gold;
#endif
        }

        public static LogLevel DefaultLogLevel(BootstrapEnvironment environment)
        {
            switch (environment)
            {
                case BootstrapEnvironment.Dev:
                    return LogLevel.Trace;
                case BootstrapEnvironment.Beta:
                    return LogLevel.Debug;
                default:
                    return LogLevel.Info;
            }
        }
    }
}
