using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 最小字符串表：从 TextAsset 或文件加载 JSON 字典（扁平 key→value）。
    /// JSON 形如 <c>{"ui.play":"开始","ui.quit":"退出"}</c>。
    /// </summary>
    public sealed class SimpleStringTable
    {
        readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);

        public int Count => _map.Count;

        public void Clear() => _map.Clear();

        public void LoadFromTextAsset(TextAsset asset)
        {
            if (asset == null)
                return;
            LoadFromJson(asset.text);
        }

        public void LoadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;
            try
            {
                LoadFromJson(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SimpleStringTable] LoadFromFile: {e.Message}");
            }
        }

        public void LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                // Unity JsonUtility 不支持 Dictionary；用简易解析包装
                var wrapper = JsonUtility.FromJson<KvArray>(WrapAsArray(json));
                if (wrapper != null && wrapper.items != null)
                {
                    foreach (var kv in wrapper.items)
                    {
                        if (!string.IsNullOrEmpty(kv.key))
                            _map[kv.key] = kv.value ?? string.Empty;
                    }
                    return;
                }
            }
            catch
            {
                // fall through to line/manual
            }

            // 兜底：逐行 "key":"value"
            try
            {
                ParseLoose(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SimpleStringTable] LoadFromJson: {e.Message}");
            }
        }

        public void Set(string key, string value)
        {
            if (!string.IsNullOrEmpty(key))
                _map[key] = value ?? string.Empty;
        }

        public string Get(string key, string fallback = null)
        {
            if (string.IsNullOrEmpty(key))
                return fallback ?? string.Empty;
            return _map.TryGetValue(key, out var v) ? v : (fallback ?? key);
        }

        static string WrapAsArray(string json)
        {
            json = json.Trim();
            // 期望 {"k":"v",...} → 转成 {"items":[{"key":"k","value":"v"},...]}
            if (json.StartsWith("{") && !json.Contains("\"items\""))
            {
                var items = new List<string>();
                int i = 1;
                while (i < json.Length)
                {
                    while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ',')) i++;
                    if (i >= json.Length || json[i] == '}') break;
                    if (json[i] != '"') break;
                    var key = ReadString(json, ref i);
                    while (i < json.Length && json[i] != ':') i++;
                    if (i < json.Length) i++;
                    while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                    string val;
                    if (i < json.Length && json[i] == '"')
                        val = ReadString(json, ref i);
                    else
                    {
                        int start = i;
                        while (i < json.Length && json[i] != ',' && json[i] != '}') i++;
                        val = json.Substring(start, i - start).Trim();
                    }
                    items.Add($"{{\"key\":{JsonEscape(key)},\"value\":{JsonEscape(val)}}}");
                }
                return "{\"items\":[" + string.Join(",", items) + "]}";
            }
            return json;
        }

        static string ReadString(string s, ref int i)
        {
            i++; // skip "
            var start = i;
            var sb = new System.Text.StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '\\' && i < s.Length)
                {
                    sb.Append(s[i++]);
                    continue;
                }
                if (c == '"')
                    break;
                sb.Append(c);
            }
            return sb.ToString();
        }

        static string JsonEscape(string s)
        {
            if (s == null) return "\"\"";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        void ParseLoose(string json)
        {
            var i = 0;
            var s = json;
            while (i < s.Length)
            {
                while (i < s.Length && s[i] != '"') i++;
                if (i >= s.Length) break;
                var key = ReadString(s, ref i);
                while (i < s.Length && s[i] != '"') i++;
                if (i >= s.Length) break;
                var val = ReadString(s, ref i);
                _map[key] = val;
            }
        }

        [Serializable]
        class KvArray
        {
            public Kv[] items;
        }

        [Serializable]
        class Kv
        {
            public string key;
            public string value;
        }
    }
}
