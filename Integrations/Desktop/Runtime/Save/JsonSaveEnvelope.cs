using System;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 版本化槽位信封：<c>{ "version", "updatedUtc", "payload" }</c>。
    /// <see cref="payload"/> 为游戏自定义 JSON 字符串。
    /// </summary>
    [Serializable]
    public class JsonSaveEnvelope
    {
        public int version = 1;
        public string updatedUtc = "";
        public string payload = "{}";

        public static JsonSaveEnvelope Create(int version, string payload)
        {
            return new JsonSaveEnvelope
            {
                version = version,
                updatedUtc = DateTime.UtcNow.ToString("o"),
                payload = payload ?? "{}"
            };
        }
    }

    /// <summary>槽位元数据（列表 UI 用）。</summary>
    [Serializable]
    public struct JsonSaveSlotMeta
    {
        public int slot;
        public bool exists;
        public int version;
        public string updatedUtc;

        public static JsonSaveSlotMeta Empty(int slot) => new JsonSaveSlotMeta
        {
            slot = slot,
            exists = false,
            version = 0,
            updatedUtc = ""
        };
    }
}
