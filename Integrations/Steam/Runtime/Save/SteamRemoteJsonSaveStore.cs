using System;
using System.Collections.Generic;
using System.Text;
using Cascade.Integrations.Desktop;
using UnityEngine;
#if STEAMWORKS_NET
using Steamworks;
#endif

namespace Cascade.Integrations.Steam
{
    /// <summary>
    /// <see cref="IJsonSaveStore"/> 装饰器：本地读写转发内层（通常为 Desktop <see cref="JsonFileSaveStore"/>）；
    /// Save/Delete 时在定义了 <c>STEAMWORKS_NET</c> 的情况下额外同步到 Steam Remote Storage。
    /// 远程文件名：<c>cascade-desktop-slot{N}.json</c>。
    /// </summary>
    public sealed class SteamRemoteJsonSaveStore : IJsonSaveStore
    {
        readonly IJsonSaveStore _inner;
        readonly bool _syncToRemote;

        public SteamRemoteJsonSaveStore(IJsonSaveStore inner, bool syncToRemote = true)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _syncToRemote = syncToRemote;
        }

        static string RemoteName(int slot) => $"cascade-desktop-slot{slot}.json";

        public bool Exists(int slot) => _inner.Exists(slot);

        public string Load(int slot) => _inner.Load(slot);

        public void Save(int slot, string json)
        {
            _inner.Save(slot, json);
            if (_syncToRemote && json != null)
                TryRemoteWrite(slot, Encoding.UTF8.GetBytes(json));
        }

        public void Save(int slot, byte[] data)
        {
            _inner.Save(slot, data);
            if (_syncToRemote && data != null)
                TryRemoteWrite(slot, data);
        }

        public void Delete(int slot)
        {
            _inner.Delete(slot);
            if (!_syncToRemote)
                return;

#if STEAMWORKS_NET
            try
            {
                var name = RemoteName(slot);
                if (SteamRemoteStorage.FileExists(name))
                    SteamRemoteStorage.FileDelete(name);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamRemoteJsonSaveStore] DeleteRemote: {e.Message}");
            }
#endif
        }

        public IReadOnlyList<int> ListSlots() => _inner.ListSlots();

        static void TryRemoteWrite(int slot, byte[] bytes)
        {
#if STEAMWORKS_NET
            try
            {
                if (!SteamRemoteStorage.FileWrite(RemoteName(slot), bytes, bytes.Length))
                    Debug.LogWarning($"[SteamRemoteJsonSaveStore] FileWrite failed: slot {slot}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamRemoteJsonSaveStore] FileWrite: {e.Message}");
            }
#else
            _ = slot;
            _ = bytes;
#endif
        }
    }
}
