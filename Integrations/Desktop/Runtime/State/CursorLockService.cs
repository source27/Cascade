using UnityEngine;

namespace Cascade.Integrations.Desktop
{
    /// <summary>鼠标光标锁定 / 释放 / Confined。</summary>
    public static class CursorLockService
    {
        public static void Lock()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public static void Unlock()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Confine()
        {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        public static void Apply(CursorLockMode mode, bool visible)
        {
            Cursor.lockState = mode;
            Cursor.visible = visible;
        }

        public static void ForceFree() => Unlock();
    }
}
