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
    /// 只读/读写 Steam Remote Storage 上的槽位文件（不经本地）。
    /// 远程名：<c>cascade-desktop-slot{N}.json</c>（与 <see cref="SteamRemoteJsonSaveStore"/> 一致）。
    /// </summary>
    public sealed class SteamRemoteOnlyJsonSaveStore : IJsonSaveStore
    {
        static string RemoteName(int slot) => $"cascade-desktop-slot{slot}.json";

        public bool Exists(int slot)
        {
#if STEAMWORKS_NET
            try { return SteamRemoteStorage.FileExists(RemoteName(slot)); }
            catch { return false; }
#else
            return false;
#endif
        }

        public string Load(int slot)
        {
#if STEAMWORKS_NET
            try
            {
                var name = RemoteName(slot);
                if (!SteamRemoteStorage.FileExists(name))
                    return null;
                var size = SteamRemoteStorage.GetFileSize(name);
                if (size <= 0)
                    return null;
                var buf = new byte[size];
                var read = SteamRemoteStorage.FileRead(name, buf, size);
                if (read <= 0)
                    return null;
                return Encoding.UTF8.GetString(buf, 0, read);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamRemoteOnlyJsonSaveStore] Load: {e.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        public void Save(int slot, string json)
        {
            if (json == null) return;
            Save(slot, Encoding.UTF8.GetBytes(json));
        }

        public void Save(int slot, byte[] data)
        {
            if (data == null) return;
#if STEAMWORKS_NET
            try
            {
                if (!SteamRemoteStorage.FileWrite(RemoteName(slot), data, data.Length))
                    Debug.LogWarning($"[SteamRemoteOnlyJsonSaveStore] FileWrite failed slot {slot}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamRemoteOnlyJsonSaveStore] Save: {e.Message}");
            }
#endif
        }

        public void Delete(int slot)
        {
#if STEAMWORKS_NET
            try
            {
                var name = RemoteName(slot);
                if (SteamRemoteStorage.FileExists(name))
                    SteamRemoteStorage.FileDelete(name);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamRemoteOnlyJsonSaveStore] Delete: {e.Message}");
            }
#endif
        }

        public IReadOnlyList<int> ListSlots()
        {
            var list = new List<int>();
#if STEAMWORKS_NET
            try
            {
                var count = SteamRemoteStorage.GetFileCount();
                for (int i = 0; i < count; i++)
                {
                    int size;
                    var name = SteamRemoteStorage.GetFileNameAndSize(i, out size);
                    if (string.IsNullOrEmpty(name)) continue;
                    const string prefix = "cascade-desktop-slot";
                    const string suffix = ".json";
                    if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(suffix, StringComparison.Ordinal))
                        continue;
                    var mid = name.Substring(prefix.Length, name.Length - prefix.Length - suffix.Length);
                    if (int.TryParse(mid, out var idx) && idx >= 0)
                        list.Add(idx);
                }
                list.Sort();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamRemoteOnlyJsonSaveStore] ListSlots: {e.Message}");
            }
#endif
            return list;
        }
    }
}
