using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Cascade.Integrations.Desktop
{
    /// <summary>检测同一 Action Map 内绑定路径冲突。</summary>
    public static class RebindConflictDetector
    {
        public struct Conflict
        {
            public InputAction actionA;
            public InputAction actionB;
            public string path;
        }

        public static List<Conflict> FindConflicts(InputActionMap map)
        {
            var result = new List<Conflict>();
            if (map == null) return result;

            var pathToAction = new Dictionary<string, InputAction>();
            foreach (var action in map.actions)
            {
                if (action == null) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    if (b.isComposite || b.isPartOfComposite)
                        continue;
                    var path = b.effectivePath;
                    if (string.IsNullOrEmpty(path))
                        continue;
                    if (pathToAction.TryGetValue(path, out var other) && other != action)
                    {
                        result.Add(new Conflict { actionA = other, actionB = action, path = path });
                    }
                    else
                    {
                        pathToAction[path] = action;
                    }
                }
            }
            return result;
        }

        public static bool HasConflict(InputActionMap map, string path, InputAction exclude)
        {
            if (map == null || string.IsNullOrEmpty(path)) return false;
            foreach (var action in map.actions)
            {
                if (action == null || action == exclude) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var b = action.bindings[i];
                    if (b.isComposite) continue;
                    if (b.effectivePath == path)
                        return true;
                }
            }
            return false;
        }
    }
}
