using System.Collections.Generic;
using Cascade.Bootstrap;
using Cascade.Service;

namespace Cascade.Launcher
{
    public sealed class MobileBootstrapConfiguration
    {
        public const string DefaultHotUpdateDllLocation = "GameLogic.HotUpdate.dll";
        public const string DefaultHotUpdateAssemblyName = "GameLogic.HotUpdate";
        public const string DefaultGameLogicEntryType = "GameLogic.GameLogicEntry";

        public MobileBootstrapConfiguration(
            BootstrapEnvironment environment,
            BootstrapPlayMode playMode,
            string appVersion,
            string hotUpdateDllLocation = DefaultHotUpdateDllLocation,
            string hotUpdateAssemblyName = DefaultHotUpdateAssemblyName,
            string gameLogicEntryType = DefaultGameLogicEntryType)
        {
            Environment = environment;
            PlayMode = playMode;
            AppVersion = appVersion ?? string.Empty;
            HotUpdateDllLocation = string.IsNullOrWhiteSpace(hotUpdateDllLocation)
                ? DefaultHotUpdateDllLocation
                : hotUpdateDllLocation;
            HotUpdateAssemblyName = string.IsNullOrWhiteSpace(hotUpdateAssemblyName)
                ? DefaultHotUpdateAssemblyName
                : hotUpdateAssemblyName;
            GameLogicEntryType = string.IsNullOrWhiteSpace(gameLogicEntryType)
                ? DefaultGameLogicEntryType
                : gameLogicEntryType;
            AssemblyLoadMode = ResolveAssemblyLoadMode(playMode);
            AotMetadataLocations = AotMetadataCatalog.ResolveLocations();
        }

        public BootstrapEnvironment Environment { get; }
        public BootstrapPlayMode PlayMode { get; }
        public string AppVersion { get; }
        public string HotUpdateDllLocation { get; }
        public string HotUpdateAssemblyName { get; }
        public string GameLogicEntryType { get; }
        public ResourceInitOptions ResourceInitOptions { get; set; }
        public IReadOnlyList<string> AotMetadataLocations { get; }
        public BootstrapAssemblyLoadMode AssemblyLoadMode { get; }

        public static BootstrapAssemblyLoadMode ResolveAssemblyLoadMode(BootstrapPlayMode playMode)
        {
            return playMode == BootstrapPlayMode.EditorSimulate
                ? BootstrapAssemblyLoadMode.EditorLoaded
                : BootstrapAssemblyLoadMode.RawFile;
        }

        public static LauncherFailureKind GetFailureKind(LauncherStage stage)
        {
            switch (stage)
            {
                case LauncherStage.InitializeResource:
                case LauncherStage.CheckUpdate:
                case LauncherStage.DownloadPatch:
                    return LauncherFailureKind.Resource;
                case LauncherStage.LoadAOTMetadata:
                case LauncherStage.LoadGameLogicAssembly:
                    return LauncherFailureKind.Code;
                default:
                    return LauncherFailureKind.GameLogic;
            }
        }
    }
}
