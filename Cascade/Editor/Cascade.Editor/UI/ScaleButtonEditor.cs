using System;
using Cascade.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Editor
{
    [CustomEditor(typeof(ScaleButton))]
    public sealed class ScaleButtonEditor : UnityEditor.Editor
    {
        private const string UndoLabel = "ScaleButton 转为子级缩放表现";

        private SerializedProperty _target;
        private SerializedProperty _pressedScale;
        private SerializedProperty _duration;

        private void OnEnable()
        {
            _target = serializedObject.FindProperty("_target");
            _pressedScale = serializedObject.FindProperty("_pressedScale");
            _duration = serializedObject.FindProperty("_duration");
        }

        public override void OnInspectorGUI()
        {
            var scaleButton = (ScaleButton)target;
            if (scaleButton.GetComponent<Image>() != null)
            {
                EditorGUILayout.HelpBox(
                    "Image 位于按钮自身：按压缩放会连同交互区域一起缩放。\n" +
                    "点击下方按钮，生成子级 root（承载 Image），并把现有子节点全部挂到 root 下一起缩放；\n" +
                    "按钮自身补 EmptyRaycast 作为固定交互区域。",
                    MessageType.Info);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
                {
                    if (GUILayout.Button("转为 root 子级缩放"))
                    {
                        try
                        {
                            ConvertToChildVisual(scaleButton);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogException(ex);
                            EditorUtility.DisplayDialog("ScaleButton", ex.Message, "确定");
                        }
                    }
                }
                EditorGUILayout.Space();
            }

            serializedObject.Update();
            EditorGUILayout.PropertyField(_target, new GUIContent("缩放目标"));
            EditorGUILayout.PropertyField(_pressedScale, new GUIContent("按下缩放"));
            EditorGUILayout.PropertyField(_duration, new GUIContent("动画时长"));
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 检测到 Image 位于按钮自身时，一键重构：
        /// 生成子级 root（铺满按钮、承载 Image，作为缩放目标），
        /// 把按钮下既有子节点全部挂到 root 下跟随缩放，
        /// 按钮自身补 EmptyRaycast 作为固定交互区域。
        /// 按钮自身无 Image 时返回 false。
        /// </summary>
        public static bool ConvertToChildVisual(ScaleButton scaleButton)
        {
            var buttonGo = scaleButton.gameObject;
            var image = buttonGo.GetComponent<Image>();
            if (image == null)
                return false;

            var buttonRect = (RectTransform)buttonGo.transform;
            var button = buttonGo.GetComponent<Button>();
            var retargetGraphic = button != null && button.targetGraphic == image;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(UndoLabel);

            // 先记下既有子节点（创建 root 前），转换后全部挂到 root 下。
            var existingChildren = new Transform[buttonRect.childCount];
            for (var i = 0; i < buttonRect.childCount; i++)
                existingChildren[i] = buttonRect.GetChild(i);

            // 1. 子级 root：铺满按钮，承载视觉 Image，作为唯一缩放目标
            var rootGo = new GameObject("root", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rootGo, UndoLabel);
            var rootRect = (RectTransform)rootGo.transform;
            rootRect.SetParent(buttonRect, false);
            // pivot 须在 offset 之前设置，否则 Unity 会为保角而改写 offset。
            rootRect.pivot = buttonRect.pivot;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localRotation = Quaternion.identity;
            rootRect.localScale = Vector3.one;

            // 2. root 上重建 Image：复制原组件字段，关闭 raycast（交互由 EmptyRaycast 负责）
            var newImage = Undo.AddComponent<Image>(rootGo);
            Undo.RecordObject(newImage, UndoLabel);
            EditorUtility.CopySerialized(image, newImage);
            newImage.raycastTarget = false;

            // 3. 既有子节点挂到 root 下（保持原 sibling 顺序与本地布局）
            for (var i = 0; i < existingChildren.Length; i++)
            {
                var child = existingChildren[i];
                if (child == null)
                    continue;
                Undo.SetTransformParent(child, rootRect, UndoLabel);
            }

            // 4. Button.targetGraphic 原指向被移除的 Image 时，重定向到 root 上的 Image
            if (retargetGraphic)
            {
                var buttonSo = new SerializedObject(button);
                buttonSo.FindProperty("m_TargetGraphic").objectReferenceValue = newImage;
                buttonSo.ApplyModifiedProperties();
            }

            // 5. 移除按钮自身上的 Image
            Undo.DestroyObjectImmediate(image);

            // 6. 按钮自身补 EmptyRaycast 作为固定交互区域（已存在则不重复）
            if (buttonGo.GetComponent<EmptyRaycast>() == null)
                Undo.AddComponent<EmptyRaycast>(buttonGo);

            // 7. 缩放目标指向 root
            var so = new SerializedObject(scaleButton);
            var targetProp = so.FindProperty("_target");
            if (targetProp == null)
                throw new InvalidOperationException("ScaleButton 缺少 _target 字段。");
            targetProp.objectReferenceValue = rootGo.transform;
            so.ApplyModifiedProperties();

            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            return true;
        }
    }
}
