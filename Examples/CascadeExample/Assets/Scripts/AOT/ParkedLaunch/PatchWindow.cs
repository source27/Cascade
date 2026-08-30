using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cascade.Service;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Launcher
{
    public sealed class PatchWindow : MonoBehaviour, ILauncherView
    {
        [SerializeField] private Slider slider;
        [SerializeField] private Text statusText;
        [SerializeField] private GameObject messageBox;
        [SerializeField] private Text messageContent;
        [SerializeField] private Button okButton;

        private UniTaskCompletionSource _confirmSource;
        private Action _retryAction;

        public void HideWindow()
        {
            gameObject.SetActive(false);
        }

        public void ShowWindow()
        {
            gameObject.SetActive(true);
        }

        private void Awake()
        {
            BindReferences();
            if (slider != null)
            {
                slider.minValue = 0f;
                slider.maxValue = 1f;
                slider.value = 0f;
            }

            if (messageBox != null)
                messageBox.SetActive(false);

            if (okButton != null)
            {
                okButton.onClick.RemoveListener(OnOkClicked);
                okButton.onClick.AddListener(OnOkClicked);
            }

            SetStatus(LauncherText.Get(LauncherText.Starting));
        }

        private void OnDestroy()
        {
            if (okButton != null)
                okButton.onClick.RemoveListener(OnOkClicked);
            _confirmSource?.TrySetCanceled();
        }

        public void SetStatus(string text)
        {
            if (statusText != null)
                statusText.text = text ?? string.Empty;
        }

        public void SetProgress(float normalized01)
        {
            if (slider != null)
                slider.value = Mathf.Clamp01(normalized01);
        }

        public void SetDownloadProgress(LauncherDownloadProgress progress)
        {
            SetProgress(progress.NormalizedProgress);
            var percent = Mathf.RoundToInt(progress.NormalizedProgress * 100f);
            var currentMb = FormatBytes(progress.CurrentBytes);
            var totalMb = FormatBytes(progress.TotalBytes);
            SetStatus(LauncherText.Format(LauncherText.DownloadingSize, percent, currentMb, totalMb));
        }

        public UniTask WaitConfirmDownloadAsync(long totalBytes, CancellationToken cancellationToken = default)
        {
            if (messageBox == null || messageContent == null || okButton == null)
                return UniTask.CompletedTask;

            _confirmSource?.TrySetCanceled();
            _confirmSource = new UniTaskCompletionSource();

            messageContent.text = LauncherText.Format(LauncherText.ConfirmUpdate, FormatBytes(totalBytes));
            messageBox.SetActive(true);
            messageBox.transform.SetAsLastSibling();
            SetStatus(LauncherText.Get(LauncherText.WaitingConfirm));

            if (cancellationToken.CanBeCanceled)
            {
                cancellationToken.Register(() =>
                {
                    HideMessageBox();
                    _confirmSource?.TrySetCanceled();
                });
            }

            return _confirmSource.Task;
        }

        public void ShowError(string message, Action onRetry)
        {
            _retryAction = onRetry;
            if (messageBox == null || messageContent == null)
            {
                SetStatus(message);
                return;
            }

            _confirmSource = null;
            var text = message ?? string.Empty;
            messageContent.text = text;
            messageBox.SetActive(true);
            messageBox.transform.SetAsLastSibling();
            SetStatus(LauncherText.Get(LauncherText.ErrorTitle));
        }

        public void HideError()
        {
            _retryAction = null;
            HideMessageBox();
        }

        private void OnOkClicked()
        {
            if (_confirmSource != null)
            {
                var source = _confirmSource;
                _confirmSource = null;
                HideMessageBox();
                SetStatus(LauncherText.Get(LauncherText.StartDownload));
                source.TrySetResult();
                return;
            }

            if (_retryAction != null)
            {
                var retry = _retryAction;
                _retryAction = null;
                HideMessageBox();
                retry.Invoke();
            }
        }

        private void HideMessageBox()
        {
            if (messageBox != null)
                messageBox.SetActive(false);
        }

        private void BindReferences()
        {
            if (slider == null)
            {
                var sliderTransform = transform.Find("UIWindow/Slider");
                if (sliderTransform != null)
                    slider = sliderTransform.GetComponent<Slider>();
            }

            if (statusText == null)
            {
                var tips = transform.Find("UIWindow/Slider/txt_tips");
                if (tips != null)
                    statusText = tips.GetComponent<Text>();
            }

            if (messageBox == null)
            {
                var box = transform.Find("UIWindow/MessgeBox");
                if (box != null)
                    messageBox = box.gameObject;
            }

            if (messageBox != null)
            {
                if (messageContent == null)
                {
                    var content = messageBox.transform.Find("txt_content");
                    if (content != null)
                        messageContent = content.GetComponent<Text>();
                }

                if (okButton == null)
                {
                    var button = messageBox.transform.Find("btn_ok");
                    if (button != null)
                        okButton = button.GetComponent<Button>();
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
