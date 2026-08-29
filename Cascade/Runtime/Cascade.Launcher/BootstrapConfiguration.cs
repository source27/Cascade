using System;
using System.Collections.Generic;
using Cascade.Service;

namespace Cascade.Launcher
{
    /// <summary>
    /// Launcher configuration. Resource-provider-specific settings (YooAsset play mode,
    /// remote root, …) are intentionally NOT part of this class: assign a concrete
    /// <see cref="ResourceInitOptions"/> subclass (from a resource integration package)
    /// via <see cref="ResourceInitOptions"/> at the composition root.
    /// </summary>
    public sealed class BootstrapConfiguration
    {
        public const string DefaultHotUpdateDllLocation = "GameLogic.HotUpdate.dll";
        public const string DefaultHotUpdateAssemblyName = "GameLogic.HotUpdate";
        public const string DefaultGameLogicEntryType = "GameLogic.GameLogicEntry";

        public BootstrapConfiguration(
            BootstrapEnvironment environment,
            BootstrapPlayMode playMode,
            string appVersion,
            string hotUpdateDllLocation = DefaultHotUpdateDllLocation,
            string hotUpdateAssemblyName = DefaultHotUpdateAssemblyName,
            string gameLogicEntryType = DefaultGameLogicEntryType)
        {
            Environment = environment;
            PlayMode = playMode;
            AppVersion = appVersion;
            HotUpdateDllLocation = hotUpdateDllLocation;
            HotUpdateAssemblyName = hotUpdateAssemblyName;
            GameLogicEntryType = gameLogicEntryType;
            AotMetadataLocations = AotMetadataCatalog.ResolveLocations();
            AssemblyLoadMode = ResolveAssemblyLoadMode(playMode);
        }

        public BootstrapEnvironment Environment { get; }
        public BootstrapPlayMode PlayMode { get; }
        public string AppVersion { get; }
        public string HotUpdateDllLocation { get; }
        public string HotUpdateAssemblyName { get; }
        public string GameLogicEntryType { get; }

        /// <summary>
        /// Provider-specific resource initialization options, injected by the composition
        /// root. Null lets the resource provider apply its own defaults.
        /// </summary>
        public ResourceInitOptions ResourceInitOptions { get; set; }

        public IReadOnlyList<string> AotMetadataLocations { get; }
        public BootstrapAssemblyLoadMode AssemblyLoadMode { get; }

        public static BootstrapAssemblyLoadMode ResolveAssemblyLoadMode(BootstrapPlayMode playMode)
        {
            return playMode == BootstrapPlayMode.EditorSimulate
                ? BootstrapAssemblyLoadMode.EditorLoaded
                : BootstrapAssemblyLoadMode.RawFile;
        }

        public static BootstrapEnvironment ResolvePlayerEnvironment()
        {
#if UNITY_EDITOR
            throw new InvalidOperationException("Player environment cannot be resolved in the Unity Editor.");
#elif DEV
            return BootstrapEnvironment.Dev;
#elif BETA
            return BootstrapEnvironment.Beta;
#elif GOLD
            return BootstrapEnvironment.Gold;
#else
            return BootstrapEnvironment.Dev;
#endif
        }

        public static LauncherFailureKind GetFailureKind(LauncherStage stage)
        {
            switch (stage)
            {
                case LauncherStage.Install:
                case LauncherStage.InitializeResource:
                case LauncherStage.CheckUpdate:
                case LauncherStage.DownloadPatch:
                case LauncherStage.InitializeLocalization:
                    return LauncherFailureKind.Resource;
                case LauncherStage.LoadAOTMetadata:
                case LauncherStage.LoadGameLogicAssembly:
                    return LauncherFailureKind.Code;
                case LauncherStage.LaunchGame:
                    return LauncherFailureKind.GameLogic;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage), stage, null);
            }
        }

        public static LogLevel DefaultLogLevel(BootstrapEnvironment environment)
        {
            switch (environment)
            {
                case BootstrapEnvironment.Dev:
                    return LogLevel.Trace;
                case BootstrapEnvironment.Beta:
                    return LogLevel.Info;
                case BootstrapEnvironment.Gold:
                    return LogLevel.Warning;
                default:
                    return LogLevel.Info;
            }
        }
    }
}
