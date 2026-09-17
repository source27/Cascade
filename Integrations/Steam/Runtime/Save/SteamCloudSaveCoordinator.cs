using System;
using Cascade.Integrations.Desktop;
using UnityEngine;

namespace Cascade.Integrations.Steam
{
    /// <summary>
    /// 协调本地 <see cref="JsonFileSaveStore"/> 与 Steam 远程槽位：
    /// 比较冲突 → Resolve 将选定侧写回两侧。
    /// </summary>
    public sealed class SteamCloudSaveCoordinator
    {
        readonly IJsonSaveStore _local;
        readonly IJsonSaveStore _remote;
        readonly JsonSaveSlotService _slots;

        public IJsonSaveStore Local => _local;
        public IJsonSaveStore Remote => _remote;
        public JsonSaveSlotService Slots => _slots;

        public SteamCloudSaveCoordinator(IJsonSaveStore local = null, IJsonSaveStore remote = null, int slotCount = JsonSaveSlotService.DefaultSlotCount)
        {
            _local = local ?? new JsonFileSaveStore();
            _remote = remote ?? new SteamRemoteOnlyJsonSaveStore();
            _slots = new JsonSaveSlotService(_local, slotCount);
        }

        public bool TryCompare(int slot, out CloudSaveConflict conflict)
            => _slots.TryLoadWithRemote(_local, _remote, slot, out conflict);

        /// <summary>
        /// 按决议把选定信封写到 local + remote。
        /// Manual / None 返回 false（不写）。
        /// </summary>
        public bool Resolve(int slot, CloudSaveConflictResolution choice)
        {
            if (choice != CloudSaveConflictResolution.UseLocal && choice != CloudSaveConflictResolution.UseRemote)
                return false;

            if (!_slots.TryLoadWithRemote(_local, _remote, slot, out var conflict))
                return false;

            JsonSaveEnvelope chosen = null;
            if (choice == CloudSaveConflictResolution.UseLocal)
                chosen = conflict.local;
            else
                chosen = conflict.remote;

            if (chosen == null)
                return false;

            try
            {
                var json = JsonUtility.ToJson(chosen, true);
                _local.Save(slot, json);
                _remote.Save(slot, json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SteamCloudSaveCoordinator] Resolve slot {slot}: {e.Message}");
                return false;
            }
        }

        /// <summary>使用比较结果的 suggested（若为 Manual/None 则失败）。</summary>
        public bool ResolveSuggested(int slot)
        {
            if (!TryCompare(slot, out var conflict))
                return false;
            return Resolve(slot, conflict.suggested);
        }
    }
}
