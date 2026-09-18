using Cascade.Modules.UI;
using UnityEditor;
using UnityEngine;

namespace Cascade.Modules.UI.Editor
{
    [CustomEditor(typeof(UIBindingHost))]
    public sealed class UIBindingHostEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var host = (UIBindingHost)target;
            var buttonLabel = "Update UI Binding";
            using (new EditorGUI.DisabledScope(!UIScriptGenerator.TryGetPrefabPath(
                       host, out _, out _)))
            {
                if (GUILayout.Button(buttonLabel))
                {
                    try
                    {
                        UIScriptGenerator.UpdateBindingsFromHost(host);
                    }
                    catch (global::System.Exception ex)
                    {
                        Debug.LogException(ex);
                        EditorUtility.DisplayDialog(buttonLabel, ex.Message, "OK");
                    }
                }
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
