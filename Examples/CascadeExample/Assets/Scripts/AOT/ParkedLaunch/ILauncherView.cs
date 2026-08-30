using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Cascade.Launcher
{
    public interface ILauncherView
    {
        void SetStatus(string text);
        void SetProgress(float normalized01);
        void SetDownloadProgress(LauncherDownloadProgress progress);
        UniTask WaitConfirmDownloadAsync(long totalBytes, CancellationToken cancellationToken = default);
        void ShowError(string message, Action onRetry);
        void HideError();

        /// <summary>Hides the launcher UI once the game has launched. No-op by default.</summary>
        void HideWindow()
        {
        }

        /// <summary>Restores the launcher UI on retry. No-op by default.</summary>
        void ShowWindow()
        {
        }
    }
}
