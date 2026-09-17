using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 本地文件槽位存储：<c>persistentDataPath/cascade-desktop/saves/slot{N}.json</c>。
    /// </summary>
    public sealed class JsonFileSaveStore : IJsonSaveStore
    {
        public const string DefaultRelativeDir = "cascade-desktop/saves";

        readonly string _root;
        readonly string _extension;

        public JsonFileSaveStore(string rootFolder = null, string extension = ".json")
        {
            _root = rootFolder ?? Path.Combine(Application.persistentDataPath, DefaultRelativeDir);
            _extension = string.IsNullOrEmpty(extension) ? ".json" : extension;
            EnsureRoot();
        }

        public string RootPath => _root;

        string PathFor(int slot) => Path.Combine(_root, $"slot{slot}{_extension}");

        void EnsureRoot()
        {
            try
            {
                if (!Directory.Exists(_root))
                    Directory.CreateDirectory(_root);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonFileSaveStore] CreateDirectory: {e.Message}");
            }
        }

        public bool Exists(int slot) => File.Exists(PathFor(slot));

        public string Load(int slot)
        {
            try
            {
                var path = PathFor(slot);
                if (!File.Exists(path))
                    return null;
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonFileSaveStore] Load slot {slot}: {e.Message}");
                return null;
            }
        }

        public void Save(int slot, string json)
        {
            if (json == null)
                return;
            Save(slot, Encoding.UTF8.GetBytes(json));
        }

        public void Save(int slot, byte[] data)
        {
            if (data == null)
                return;

            try
            {
                EnsureRoot();
                File.WriteAllBytes(PathFor(slot), data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonFileSaveStore] Save slot {slot}: {e.Message}");
            }
        }

        public void Delete(int slot)
        {
            try
            {
                var path = PathFor(slot);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonFileSaveStore] Delete slot {slot}: {e.Message}");
            }
        }

        public IReadOnlyList<int> ListSlots()
        {
            var list = new List<int>();
            try
            {
                if (!Directory.Exists(_root))
                    return list;

                foreach (var file in Directory.GetFiles(_root, $"slot*{_extension}"))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (name != null && name.StartsWith("slot", StringComparison.Ordinal)
                        && int.TryParse(name.Substring(4), out var idx) && idx >= 0)
                    {
                        list.Add(idx);
                    }
                }

                list.Sort();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[JsonFileSaveStore] ListSlots: {e.Message}");
            }

            return list;
        }
    }
}
