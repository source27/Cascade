using System;
using System.Reflection;
using UnityEngine;

namespace Cascade.Integrations.InputGlyphs
{
    /// <summary>
    /// 反射调用 <c>InputGlyphs.Display.InputGlyphBootstrap.EnsureRegistered</c>。
    /// 类型或方法缺失时记录日志并安全返回（便于 peer 包未装 / API 更名）。
    /// </summary>
    public static class InputGlyphBootstrapGate
    {
        const string TypeName = "InputGlyphs.Display.InputGlyphBootstrap";
        const string MethodName = "EnsureRegistered";

        public static void EnsureRegistered()
        {
            try
            {
                var type = Type.GetType(TypeName + ", InputGlyphs.Display")
                           ?? FindType(TypeName);
                if (type == null)
                {
                    Debug.LogWarning($"[InputGlyphBootstrapGate] Type not found: {TypeName}. Place InputGlyphsSetup or ensure peer package.");
                    return;
                }

                var method = type.GetMethod(MethodName, BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    Debug.LogWarning($"[InputGlyphBootstrapGate] Method not found: {TypeName}.{MethodName}");
                    return;
                }

                method.Invoke(null, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InputGlyphBootstrapGate] EnsureRegistered failed: {e.Message}");
            }
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName);
                    if (t != null)
                        return t;
                }
                catch
                {
                    // ignore
                }
            }

            return null;
        }
    }
}
