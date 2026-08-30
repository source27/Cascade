using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;
using Cascade.Bootstrap;
using Cascade.Core;
using UnityEngine;

namespace Cascade.Launcher
{
    public sealed class LauncherFlow
    {
        private readonly MobileBootstrapConfiguration _configuration;
        private readonly IServiceRegistry _services;
        private readonly IGameHost _host;
        private readonly ILauncherView _view;
        private readonly ILogService _log;
        private readonly IResourceService _resources;
            private readonly ILocalizationService _localization;
        private readonly CodeLoader _codeLoader;
        private CancellationTokenSource _cts;
        private bool _running;

        public LauncherFlow(
            MobileBootstrapConfiguration configuration,
            IServiceRegistry services,
            IGameHost host,
            ILauncherView view = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _view = view;
            _log = services.Get<ILogService>();
            _resources = services.Get<IResourceService>();
            _localization = services.Get<ILocalizationService>();
            _codeLoader = new CodeLoader(_log, configuration.AssemblyLoadMode, configuration.GameLogicEntryType, configuration.HotUpdateAssemblyName);
        }

        public MobileBootstrapConfiguration Configuration => _configuration;
        public LauncherStage Stage { get; private set; }
        public string StatusText { get; private set; } = "Waiting.";
        public bool HasFailed { get; private set; }
        public LauncherFailure? Failure { get; private set; }
        public string GameLogicVersion { get; private set; }

        public void Start()
        {
            if (_running)
                return;
            _running = true;
            _cts = new CancellationTokenSource();
            RunAsync(_cts.Token).Forget();
        }

        public void Retry()
        {
            if (!HasFailed || _running)
                return;
            ShutdownGameLogic();
            HasFailed = false;
            Failure = null;
            _view?.HideError();
            _view?.ShowWindow();
            SetStatus(LauncherText.Retrying);
            Start();
        }

        public void Shutdown()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            _running = false;
            ShutdownGameLogic();
        }

        private void ShutdownGameLogic()
        {
            try
            {
                _codeLoader.InvokeStop(_loadedAssembly);
            }
            catch (Exception exception)
            {
                _log.Exception("Launcher", exception, "GameLogic stop failed.");
            }

            try
            {
                _host.DestroyUISystem();
            }
            catch (Exception exception)
            {
                _log.Exception("Launcher", exception, "UISystem destroy failed.");
            }

            GameLogicVersion = null;
        }

        private async UniTaskVoid RunAsync(CancellationToken cancellationToken)
        {
            HasFailed = false;
            Failure = null;
            Stage = LauncherStage.None;
            SetStatus(LauncherText.Get(LauncherText.Starting));
            _view?.SetProgress(0f);

            try
            {
                await RunInstallAsync(cancellationToken);
                await RunInitializeResourceAsync(cancellationToken);
                await RunCheckUpdateAsync(cancellationToken);
                if (_configuration.PlayMode == BootstrapPlayMode.Host)
                {
                    await RunDownloadPatchAsync(cancellationToken);
                }
                else if (_configuration.PlayMode == BootstrapPlayMode.EditorSimulate)
                {
                    SetStatus(LauncherText.Get(LauncherText.EditorSimulate));
                }
                else
                {
                    SetStatus(LauncherText.Get(LauncherText.BuiltinResource));
                }

                await RunInitializeLocalizationAsync(cancellationToken);
                await RunLoadAotMetadataAsync(cancellationToken);
                await RunLoadGameLogicAssemblyAsync(cancellationToken);
                await RunLaunchGameAsync(cancellationToken);

                Stage = LauncherStage.Completed;
                SetStatus(LauncherText.Format(LauncherText.Completed, ResourceUpdateBridge.ActivePackageVersion(_resources)));
                _view?.SetProgress(1f);
                // The game is up — the launcher UI's job is done.
                _view?.HideWindow();
                _log.Info("Launcher", StatusText);
            }
            catch (OperationCanceledException)
            {
                SetStatus(LauncherText.Get(LauncherText.Cancelled));
            }
            catch (Exception)
            {
                // Fail already recorded.
            }
            finally
            {
                _running = false;
            }
        }

        private UniTask RunInstallAsync(CancellationToken cancellationToken)
        {
            Stage = LauncherStage.Install;
            SetStatus(LauncherText.Get(LauncherText.CheckInstall));
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        private async UniTask RunInitializeResourceAsync(CancellationToken cancellationToken)
        {
            Stage = LauncherStage.InitializeResource;
            SetStatus(LauncherText.Get(LauncherText.InitResource));
            if (_resources.IsInitialized)
            {
                _log.Info("Launcher", "Resource package already initialized (thin Bootstrap); skipping.");
                return;
            }

            try
            {
                var options = _configuration.ResourceInitOptions ?? new ResourceInitOptions();
                await _resources.InitializeAsync(options, cancellationToken);
                _log.Info("Launcher", "Resource package initialized.");
            }
            catch (Exception exception)
            {
                Fail(LauncherFailureKind.Resource, exception);
                throw;
            }
        }

        private async UniTask RunCheckUpdateAsync(CancellationToken cancellationToken)
        {
            Stage = LauncherStage.CheckUpdate;
            SetStatus(LauncherText.Get(LauncherText.CheckVersion));
            try
            {
                var version = await ResourceUpdateBridge.RequestVersionAsync(_resources, cancellationToken);
                if (ResourceUpdateBridge.IsUsingLocalVersion(_resources))
                {
                    SetStatus(LauncherText.Format(LauncherText.NetworkOfflineUseLocal, version));
                    _log.Warning("Launcher", $"CDN unavailable; fallback to local package version: {version}");
                }
                else
                {
                    _log.Info("Launcher", $"Package version: {version}");
                    SetStatus(LauncherText.Format(LauncherText.LoadManifest, version));
                }
                await ResourceUpdateBridge.UpdateManifestAsync(_resources, version, cancellationToken);
                if (ResourceUpdateBridge.IsUsingLocalVersion(_resources))
                    SetStatus(LauncherText.Format(LauncherText.NetworkOfflineUseLocal, ResourceUpdateBridge.ActivePackageVersion(_resources)));
            }
            catch (Exception exception)
            {
                Fail(LauncherFailureKind.Resource, exception);
                throw;
            }
        }

        private async UniTask RunDownloadPatchAsync(CancellationToken cancellationToken)
        {
            Stage = LauncherStage.DownloadPatch;
            SetStatus(LauncherText.Get(LauncherText.CountingUpdate));
            try
            {
                var plan = ResourceUpdateBridge.PrepareDownload(_resources);
                if (ResourceUpdateBridge.IsUsingLocalVersion(_resources) && plan.NeedsDownload)
                    throw new InvalidOperationException("本地资源缓存不完整，且当前无法连接更新服务器。");

                if (plan.NeedsDownload)
                {
                    if (_view != null)
                        await _view.WaitConfirmDownloadAsync(plan.TotalBytes, cancellationToken);
                    else
                        SetStatus(LauncherText.Format(LauncherText.UpdateFoundStartDownload, FormatBytes(plan.TotalBytes)));

                    SetStatus(LauncherText.Get(LauncherText.StartDownload));
                    var progress = new Progress<LauncherDownloadProgress>(p =>
                    {
                        if (_view != null)
                            _view.SetDownloadProgress(p);
                        else
                            StatusText = LauncherText.Format(LauncherText.DownloadingPercent, Mathf.RoundToInt(p.NormalizedProgress * 100f));
                    });
                    await ResourceUpdateBridge.DownloadAsync(_resources, progress, cancellationToken);
                    SetStatus(LauncherText.Get(LauncherText.DownloadComplete));
                    _log.Info("Launcher", "Package download completed.");
                }
                else
                {
                    SetStatus(LauncherText.Get(LauncherText.UpToDate));
                }

                SetStatus(LauncherText.Get(LauncherText.CleaningObsolete));
                await ResourceUpdateBridge.ClearUnusedCacheAsync(_resources, cancellationToken);
                _view?.SetProgress(1f);
                _log.Info("Launcher", "Unused cache cleared.");
            }
            catch (Exception exception)
            {
                Fail(LauncherFailureKind.Resource, exception);
                throw;
            }
        }

        private async UniTask RunLoadAotMetadataAsync(CancellationToken cancellationToken)
        {
            Stage = LauncherStage.LoadAOTMetadata;
            var locations = _configuration.AotMetadataLocations;
            if (locations == null || locations.Count == 0)
            {
                SetStatus(LauncherText.Get(LauncherText.NoAotMetadata));
                _log.Info("Launcher", "PatchedAOTAssemblyList is empty; skip AOT metadata load.");
                return;
            }

            SetStatus(LauncherText.Format(LauncherText.LoadingAotMetadata, locations.Count));
            try
            {
                foreach (var location in locations)
                {
                    var bytes = await _resources.LoadRawBytesAsync(location, cancellationToken);
                    _codeLoader.LoadMetadata(bytes, location);
                }
            }
            catch (Exception exception)
            {
                Fail(LauncherFailureKind.Code, exception);
                throw;
            }
        }

        private async UniTask RunInitializeLocalizationAsync(CancellationToken cancellationToken)
        {
            Stage = LauncherStage.InitializeLocalization;
            SetStatus(LauncherText.Get(LauncherText.LoadingLocalization));
            if (LocalizationAccess.IsBound)
            {
                _log.Info("Launcher", "Localization already bound (thin Bootstrap); skipping.");
                return;
            }

            try
            {
                await _localization.InitializeAsync(cancellationToken);
                LocalizationAccess.Bind(_localization);
                _log.Info("Launcher", $"Localization initialized: {_localization.CurrentLocale}");
            }
            catch (Exception exception)
            {
                Fail(LauncherFailureKind.Resource, exception);
                throw;
            }
        }

        private async UniTask RunLoadGameLogicAssemblyAsync(CancellationToken cancellationToken)
        {
            Stage = LauncherStage.LoadGameLogicAssembly;
            SetStatus(LauncherText.Get(LauncherText.LoadingHotUpdate));
            try
            {
                // EditorSimulate: no dll fetch — CodeLoader uses the editor-compiled assembly.
                if (_configuration.AssemblyLoadMode == BootstrapAssemblyLoadMode.EditorLoaded)
                {
                    _loadedAssembly = _codeLoader.LoadGameLogicAssembly(null);
                }
                else
                {
                    var bytes = await _resources.LoadRawBytesAsync(_configuration.HotUpdateDllLocation, cancellationToken);
                    _loadedAssembly = _codeLoader.LoadGameLogicAssembly(bytes);
                }
            }
            catch (Exception exception)
            {
                Fail(LauncherFailureKind.Code, exception);
                throw;
            }
        }

        private global::System.Reflection.Assembly _loadedAssembly;

        private async UniTask RunLaunchGameAsync(CancellationToken cancellationToken)
        {
            Stage = LauncherStage.LaunchGame;
            SetStatus(LauncherText.Get(LauncherText.LaunchingGame));
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (_loadedAssembly == null)
                    throw new InvalidOperationException("GameLogic assembly is not loaded.");
                ShutdownGameLogic();
                GameLogicVersion = await _codeLoader.InvokeEntryAsync(_loadedAssembly, _host, cancellationToken);
            }
            catch (Exception exception)
            {
                Fail(LauncherFailureKind.GameLogic, exception);
                throw;
            }
        }

        private void SetStatus(string text)
        {
            StatusText = text ?? string.Empty;
            _view?.SetStatus(StatusText);
        }

        private void Fail(LauncherFailureKind kind, Exception exception)
        {
            HasFailed = true;
            var detail = exception?.ToString() ?? "Unknown error";
            Failure = new LauncherFailure(kind, Stage, detail);
            var userMessage = GetUserFacingErrorMessage(kind, Stage);
            StatusText = userMessage;
            _log.Exception("Launcher", exception, $"{Failure.Value.Code} {userMessage}");
            _view?.ShowError(userMessage, Retry);
        }

        private string GetUserFacingErrorMessage(LauncherFailureKind kind, LauncherStage stage)
        {
            switch (stage)
            {
                case LauncherStage.InitializeResource:
                    return LauncherText.Get(LauncherText.ErrorInitResource);
                case LauncherStage.CheckUpdate:
                    return LauncherText.Get(LauncherText.ErrorNoServerNoLocal);
                case LauncherStage.DownloadPatch:
                    return ResourceUpdateBridge.IsUsingLocalVersion(_resources)
                        ? LauncherText.Get(LauncherText.ErrorLocalIncomplete)
                        : LauncherText.Get(LauncherText.ErrorDownload);
                case LauncherStage.InitializeLocalization:
                    return LauncherText.Get(LauncherText.ErrorLoadLocalization);
                case LauncherStage.LoadAOTMetadata:
                    return LauncherText.Get(LauncherText.ErrorLoadAotMetadata);
                case LauncherStage.LoadGameLogicAssembly:
                    return LauncherText.Get(LauncherText.ErrorLoadGameCode);
                case LauncherStage.LaunchGame:
                    return LauncherText.Get(LauncherText.ErrorLaunchGame);
                default:
                    switch (kind)
                    {
                        case LauncherFailureKind.Resource:
                            return LauncherText.Get(LauncherText.ErrorResource);
                        case LauncherFailureKind.Code:
                            return LauncherText.Get(LauncherText.ErrorLoadGame);
                        case LauncherFailureKind.GameLogic:
                            return LauncherText.Get(LauncherText.ErrorLaunchGame);
                        default:
                            return LauncherText.Get(LauncherText.ErrorUnknown);
                    }
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes <= 0)
                return "0 B";
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024f:0.#} KB";
            return $"{bytes / (1024f * 1024f):0.##} MB";
        }

    }
}
