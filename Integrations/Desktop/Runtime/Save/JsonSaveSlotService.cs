using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 多槽 JSON 存档服务（默认 3 槽）。信封带 version / updatedUtc / payload。
    /// </summary>
    public sealed class JsonSaveSlotService
    {
        public const int DefaultSlotCount = 3;
        public const int DefaultSchemaVersion = 1;

        readonly IJsonSaveStore _store;
        readonly int _slotCount;

        public int SlotCount => _slotCount;
        public int CurrentSchemaVersion { get; set; } = DefaultSchemaVersion;

        public Func<JsonSaveEnvelope, JsonSaveEnvelope> Migrator { get; set; }

        public JsonSaveSlotService(IJsonSaveStore store = null, int slotCount = DefaultSlotCount)
        {
            _store = store ?? new JsonFileSaveStore();
            _slotCount = Math.Max(1, slotCount);
        }

        public IJsonSaveStore Store => _store;

        public bool Exists(int slot) => ValidSlot(slot) && _store.Exists(slot);

        public bool TryLoad(int slot, out JsonSaveEnvelope envelope)
        {
            envelope = null;
            if (!ValidSlot(slot))
                return false;

            var raw = _store.Load(slot);
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            try
            {
                envelope = JsonUtility.FromJson<JsonSaveEnvelope>(raw);
                if (envelope == null)
                    return false;

                if (envelope.version < CurrentSchemaVersion)
                    envelope = RunMigrate(envelope);

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonSaveSlotService] TryLoad slot {slot}: {e.Message}");
                envelope = null;
                return false;
            }
        }

        public bool TryLoadPayload(int slot, out string payload, out int version)
        {
            payload = null;
            version = 0;
            if (!TryLoad(slot, out var env) || env == null)
                return false;
            payload = env.payload;
            version = env.version;
            return true;
        }

        public void Save(int slot, string payload, int? version = null)
        {
            if (!ValidSlot(slot))
                return;

            var env = JsonSaveEnvelope.Create(version ?? CurrentSchemaVersion, payload);
            SaveEnvelope(slot, env);
        }

        public void SaveEnvelope(int slot, JsonSaveEnvelope envelope)
        {
            if (!ValidSlot(slot) || envelope == null)
                return;

            if (string.IsNullOrEmpty(envelope.updatedUtc))
                envelope.updatedUtc = DateTime.UtcNow.ToString("o");
            if (envelope.version <= 0)
                envelope.version = CurrentSchemaVersion;

            try
            {
                _store.Save(slot, JsonUtility.ToJson(envelope, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonSaveSlotService] Save slot {slot}: {e.Message}");
            }
        }

        public void Delete(int slot)
        {
            if (!ValidSlot(slot))
                return;
            _store.Delete(slot);
        }

        public JsonSaveSlotMeta GetSlotMeta(int slot)
        {
            if (!ValidSlot(slot) || !_store.Exists(slot))
                return JsonSaveSlotMeta.Empty(slot);

            var raw = _store.Load(slot);
            if (string.IsNullOrWhiteSpace(raw))
                return JsonSaveSlotMeta.Empty(slot);

            try
            {
                var env = JsonUtility.FromJson<JsonSaveEnvelope>(raw);
                if (env == null)
                    return JsonSaveSlotMeta.Empty(slot);

                return new JsonSaveSlotMeta
                {
                    slot = slot,
                    exists = true,
                    version = env.version,
                    updatedUtc = env.updatedUtc ?? ""
                };
            }
            catch
            {
                return new JsonSaveSlotMeta
                {
                    slot = slot,
                    exists = true,
                    version = 0,
                    updatedUtc = ""
                };
            }
        }

        public JsonSaveSlotMeta[] ListSlotMeta()
        {
            var arr = new JsonSaveSlotMeta[_slotCount];
            for (int i = 0; i < _slotCount; i++)
                arr[i] = GetSlotMeta(i);
            return arr;
        }

        public IReadOnlyList<int> ListExistingSlots()
        {
            var all = _store.ListSlots();
            var filtered = new List<int>();
            foreach (var s in all)
            {
                if (s >= 0 && s < _slotCount)
                    filtered.Add(s);
            }
            return filtered;
        }

        /// <summary>
        /// 同时从 local / remote 存储读取同一槽，比较冲突。
        /// <paramref name="remote"/> 为 null 时等同于仅本地 <see cref="TryLoad"/>（suggested=None 或 UseLocal）。
        /// </summary>
        public bool TryLoadWithRemote(
            IJsonSaveStore local,
            IJsonSaveStore remote,
            int slot,
            out CloudSaveConflict conflict)
        {
            conflict = CloudSaveConflict.None(slot);
            if (!ValidSlot(slot))
                return false;

            var localStore = local ?? _store;
            JsonSaveEnvelope localEnv = null;
            JsonSaveEnvelope remoteEnv = null;

            TryParseEnvelope(localStore, slot, out localEnv);
            if (remote != null)
                TryParseEnvelope(remote, slot, out remoteEnv);

            conflict = CloudSaveConflictResolver.Compare(slot, localEnv, remoteEnv);
            return conflict.hasLocal || conflict.hasRemote;
        }

        /// <summary>使用构造时的 store 作为 local，另传 remote。</summary>
        public bool TryLoadWithRemote(IJsonSaveStore remote, int slot, out CloudSaveConflict conflict)
            => TryLoadWithRemote(_store, remote, slot, out conflict);

        static bool TryParseEnvelope(IJsonSaveStore store, int slot, out JsonSaveEnvelope envelope)
        {
            envelope = null;
            if (store == null)
                return false;
            try
            {
                if (!store.Exists(slot))
                    return false;
                var raw = store.Load(slot);
                if (string.IsNullOrWhiteSpace(raw))
                    return false;
                envelope = UnityEngine.JsonUtility.FromJson<JsonSaveEnvelope>(raw);
                return envelope != null;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[JsonSaveSlotService] TryParseEnvelope slot {slot}: {e.Message}");
                envelope = null;
                return false;
            }
        }

        bool ValidSlot(int slot) => slot >= 0 && slot < _slotCount;

        JsonSaveEnvelope RunMigrate(JsonSaveEnvelope env)
        {
            try
            {
                if (Migrator != null)
                {
                    var next = Migrator(env);
                    if (next != null)
                    {
                        if (next.version < CurrentSchemaVersion)
                            next.version = CurrentSchemaVersion;
                        return next;
                    }
                }

                env.version = CurrentSchemaVersion;
                return env;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonSaveSlotService] Migrate: {e.Message}");
                env.version = CurrentSchemaVersion;
                return env;
            }
        }
    }
}
