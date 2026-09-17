using System;
using System.Collections.Generic;
using System.Text;
using Cascade.Service;
using UnityEngine;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace Cascade.Integrations.Steam
{
    /// <summary>
    /// <see cref="ISaveService"/> 装饰器：读写转发内层；
    /// <see cref="Flush"/> 在内层 Flush 之后，可选将已脏键写入 Steam Remote Storage（UTF-8 文本）。
    /// <para>
    /// 与 Desktop 设置配合时：请只把 <b>roaming</b> 偏好键（如
    /// <c>cascade.desktop.gameSettings.roaming</c>）写入内层 ISaveService。
    /// <b>禁止</b>把 settings.local.json / 显示画质整包塞进此装饰器。
    /// </para>
    /// </summary>
    public sealed class SteamRemoteStorageSaveStore : ISaveService
    {
        readonly ISaveService _inner;
        readonly bool _flushToRemote;
        readonly HashSet<string> _dirtyKeys = new HashSet<string>(StringComparer.Ordinal);

        public SteamRemoteStorageSaveStore(ISaveService inner, bool flushToRemote = true)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _flushToRemote = flushToRemote;
        }

        public string GetString(string key, string defaultValue = "")
            => _inner.GetString(key, defaultValue);

        public void SetString(string key, string value)
        {
            _inner.SetString(key, value);
            if (!string.IsNullOrEmpty(key))
                _dirtyKeys.Add(key);
        }

        public void Flush()
        {
            _inner.Flush();

            if (!_flushToRemote || _dirtyKeys.Count == 0)
            {
                _dirtyKeys.Clear();
                return;
            }

#if STEAMWORKS_NET
            try
            {
                foreach (var key in _dirtyKeys)
                {
                    var text = _inner.GetString(key, string.Empty) ?? string.Empty;
                    var bytes = Encoding.UTF8.GetBytes(text);
                    if (!SteamRemoteStorage.FileWrite(key, bytes, bytes.Length))
                        Debug.LogWarning($"[SteamRemoteStorageSaveStore] FileWrite failed: {key}");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamRemoteStorageSaveStore] Flush remote: {e.Message}");
            }
#endif
            _dirtyKeys.Clear();
        }
    }
}
