namespace Cascade.Mobile
{
    /// <summary>
    /// Build/stage classification, owned by the starter. Maps to concrete policy (log level, CDN root …)
    /// inside <see cref="MobileBootstrapEntry" />.
    /// </summary>
    public enum BootstrapEnvironment
    {
        Dev,
        Beta,
        Gold
    }

    public enum BootstrapPlayMode
    {
        EditorSimulate,
        Offline,
        Host
    }

    public enum BootstrapAssemblyLoadMode
    {
        EditorLoaded,
        RawFile
    }

    public enum LauncherStage
    {
        None,
        Install,
        InitializeResource,
        CheckUpdate,
        DownloadPatch,
        InitializeLocalization,
        LoadAOTMetadata,
        LoadGameLogicAssembly,
        LaunchGame,
        Completed
    }

    public enum LauncherFailureKind
    {
        Resource,
        Code,
        GameLogic
    }

    public readonly struct LauncherFailure
    {
        public LauncherFailure(LauncherFailureKind kind, LauncherStage stage, string message)
        {
            Kind = kind;
            Stage = stage;
            Message = message ?? string.Empty;
        }

        public LauncherFailureKind Kind { get; }
        public LauncherStage Stage { get; }
        public string Message { get; }
        public string Code => $"T03-{Kind.ToString().ToUpperInvariant()}-{Stage.ToString().ToUpperInvariant()}";
    }
}
