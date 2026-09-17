using System;
using UnityEngine.InputSystem;

namespace Cascade.Integrations.Desktop
{
    public sealed partial class RebindHelper
    {
        /// <summary>
        /// 重绑定完成后检测冲突；若冲突则撤销本次 override 并回调 onConflict。
        /// </summary>
        public void StartRebindWithConflictCheck(
            InputAction action,
            int bindingIndex,
            Action<string> onComplete,
            Action onCancel = null,
            Action<string> onConflict = null)
        {
            if (action == null)
            {
                onCancel?.Invoke();
                return;
            }

            var map = action.actionMap;
            string previousOverride = null;
            try
            {
                if (bindingIndex >= 0 && bindingIndex < action.bindings.Count)
                    previousOverride = action.bindings[bindingIndex].overridePath;
            }
            catch { /* ignore */ }

            StartRebind(action, bindingIndex, path =>
            {
                if (map != null && RebindConflictDetector.HasConflict(map, path, action))
                {
                    try
                    {
                        if (string.IsNullOrEmpty(previousOverride))
                            action.RemoveBindingOverride(bindingIndex);
                        else
                            action.ApplyBindingOverride(bindingIndex, previousOverride);
                        SaveOverrides();
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"[RebindHelper] conflict revert: {e.Message}");
                    }
                    onConflict?.Invoke(path);
                    return;
                }
                onComplete?.Invoke(path);
            }, onCancel);
        }
    }
}
