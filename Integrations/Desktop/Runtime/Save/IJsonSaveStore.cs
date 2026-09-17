using System.Collections.Generic;

namespace Cascade.Integrations.Desktop
{
    /// <summary>
    /// 槽位 JSON/字节存档底层存储。与 Cascade <c>ISaveService</c>（字符串偏好）分离。
    /// </summary>
    public interface IJsonSaveStore
    {
        bool Exists(int slot);

        /// <summary>读取槽位原始文本；不存在返回 null。</summary>
        string Load(int slot);

        /// <summary>写入槽位原始文本（通常为整份 envelope JSON）。</summary>
        void Save(int slot, string json);

        /// <summary>写入槽位原始字节（UTF-8 JSON 或自定义 .dat）。</summary>
        void Save(int slot, byte[] data);

        void Delete(int slot);

        /// <summary>返回当前存在的槽位索引（升序）。</summary>
        IReadOnlyList<int> ListSlots();
    }
}
