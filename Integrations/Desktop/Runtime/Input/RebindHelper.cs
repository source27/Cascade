using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 封装 Input System 交互式重绑定，并将 overrides 存为 JSON。
    /// 路径：<c>persistentDataPath/cascade-desktop/input_overrides.json</c>。
    /// <para>
    /// 概念上 overrides 属 <b>roaming-eligible</b>（可跨机偏好）；当前文件仍只写本机路径。
    /// 日后可选仅云同步此 JSON；<b>切勿</b>把显示/画质设置写入本文件。
    /// </para>
    /// </summary>
    public sealed partial class RebindHelper
    {
        public const string RelativeOverridesPath = "cascade-desktop/input_overrides.json";

        readonly InputActionAsset _asset;
        InputActionRebindingExtensions.RebindingOperation _operation;

        /// <summary>本机路径；内容 conceptually roaming-eligible。</summary>
        public string OverridesPath =>
            Path.Combine(Application.persistentDataPath, RelativeOverridesPath);

        public RebindHelper(InputActionAsset asset)
        {
            _asset = asset;
        }

        public void LoadOverrides()
        {
            if (_asset == null)
                return;
            try
            {
                if (!File.Exists(OverridesPath))
                    return;
                var json = File.ReadAllText(OverridesPath);
                if (!string.IsNullOrEmpty(json))
                    _asset.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RebindHelper] LoadOverrides: {e.Message}");
            }
        }

        public void SaveOverrides()
        {
            if (_asset == null)
                return;
            try
            {
                var dir = Path.GetDirectoryName(OverridesPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = _asset.SaveBindingOverridesAsJson();
                File.WriteAllText(OverridesPath, json ?? "{}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RebindHelper] SaveOverrides: {e.Message}");
            }
        }

        public void StartRebind(InputAction action, int bindingIndex, Action<string> onComplete, Action onCancel = null)
        {
            if (action == null || _asset == null)
            {
                onCancel?.Invoke();
                return;
            }

            Cancel();

            action.Disable();
            _operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Pointer>/position")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op =>
                {
                    action.Enable();
                    var path = op.selectedControl != null ? op.selectedControl.path : "";
                    op.Dispose();
                    _operation = null;
                    SaveOverrides();
                    onComplete?.Invoke(path);
                })
                .OnCancel(op =>
                {
                    action.Enable();
                    op.Dispose();
                    _operation = null;
                    onCancel?.Invoke();
                })
                .Start();
        }

        public void ResetAll()
        {
            if (_asset == null)
                return;
            _asset.RemoveAllBindingOverrides();
            SaveOverrides();
        }

        public void Cancel()
        {
            if (_operation == null)
                return;
            _operation.Cancel();
            _operation.Dispose();
            _operation = null;
        }
    }
}
