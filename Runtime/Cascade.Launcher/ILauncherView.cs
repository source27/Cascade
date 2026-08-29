using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;

namespace Cascade.Launcher
{
    public interface ILauncherView
    {
        void SetStatus(string text);
        void SetProgress(float normalized01);
        void SetDownloadProgress(ResourceDownloadProgress progress);
        UniTask WaitConfirmDownloadAsync(long totalBytes, CancellationToken cancellationToken = default);
        void ShowError(string message, Action onRetry);
        void HideError();
    }
}
