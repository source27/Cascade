using System.Reflection;
using Cascade.Editor;
using Cascade.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cascade.Tests
{
    public sealed class ScaleButtonTests
    {
        [Test]
        public void AddingScaleButtonAddsButtonWithNoneTransition()
        {
            var go = new GameObject("ScaleButton", typeof(RectTransform), typeof(ScaleButton));
            try
            {
                var button = go.GetComponent<Button>();
                Assert.That(button, Is.Not.Null);
                Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PointerDownDoesNotScaleWhenNotInteractable()
        {
            var go = new GameObject("ScaleButton", typeof(RectTransform), typeof(Button), typeof(ScaleButton));
            try
            {
                var button = go.GetComponent<Button>();
                var scaleButton = go.GetComponent<ScaleButton>();
                InvokeLifecycle(scaleButton, "Awake");
                InvokeLifecycle(scaleButton, "OnEnable");

                var target = go.transform;
                var rest = target.localScale;
                button.interactable = false;

                scaleButton.OnPointerDown(new PointerEventData(null));

                Assert.That(target.localScale, Is.EqualTo(rest));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void PointerDownScalesWhenInteractable()
        {
            var go = new GameObject("ScaleButton", typeof(RectTransform), typeof(Button), typeof(ScaleButton));
            try
            {
                var button = go.GetComponent<Button>();
                var scaleButton = go.GetComponent<ScaleButton>();
                var so = new SerializedObject(scaleButton);
                so.FindProperty("_duration").floatValue = 0f;
                so.FindProperty("_pressedScale").floatValue = 0.9f;
                so.ApplyModifiedPropertiesWithoutUndo();

                InvokeLifecycle(scaleButton, "Awake");
                InvokeLifecycle(scaleButton, "OnEnable");

                button.interactable = true;
                scaleButton.OnPointerDown(new PointerEventData(null));

                Assert.That(go.transform.localScale.x, Is.EqualTo(0.9f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        private static void InvokeLifecycle(object target, string methodName)
        {
            var method = typeof(ScaleButton).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }


        [Test]
        public void ConvertToChildVisualMovesImageToChildAddsEmptyRaycastAndRetargets()
        {
            var texture = new Texture2D(2, 2);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            var go = new GameObject("PlayButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ScaleButton));
            try
            {
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.25f, 0.25f);
                rect.anchorMax = new Vector2(0.75f, 0.75f);
                rect.pivot = new Vector2(0.4f, 0.6f);
                rect.anchoredPosition = new Vector2(12f, -8f);
                rect.sizeDelta = new Vector2(200f, 80f);

                var image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.color = Color.red;
                image.raycastTarget = true;
                var button = go.GetComponent<Button>();
                button.targetGraphic = image;
                var scaleButton = go.GetComponent<ScaleButton>();

                // 预置子节点：转换后应挂到 root 下，与 Image 一起缩放。
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, false);
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(go.transform, false);

                Assert.That(ScaleButtonEditor.ConvertToChildVisual(scaleButton), Is.True);

                // 原 Image 已从按钮移除
                Assert.That(image == null, Is.True);
                Assert.That(go.GetComponent<Image>(), Is.Null);

                // 缩放目标 = root，铺满按钮并承载 Image
                var target = (Transform)new SerializedObject(scaleButton).FindProperty("_target").objectReferenceValue;
                var rootRect = (RectTransform)target;
                Assert.That(rootRect.name, Is.EqualTo("root"));
                Assert.That(rootRect.parent, Is.EqualTo(go.transform));
                Assert.That(go.transform.childCount, Is.EqualTo(1));
                Assert.That(rootRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rootRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(rootRect.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(rootRect.offsetMax, Is.EqualTo(Vector2.zero));
                Assert.That(rootRect.localScale, Is.EqualTo(Vector3.one));
                Assert.That(rootRect.pivot, Is.EqualTo(rect.pivot));
                Assert.That(rootRect.rect.size, Is.EqualTo(rect.rect.size));

                var newImage = rootRect.GetComponent<Image>();
                Assert.That(newImage, Is.Not.Null);
                Assert.That(newImage.sprite, Is.SameAs(sprite));
                Assert.That(newImage.color, Is.EqualTo(Color.red));
                Assert.That(newImage.raycastTarget, Is.False);

                // 原有子节点挂到 root 下，保持相对顺序
                Assert.That(labelGo.transform.parent, Is.EqualTo(rootRect));
                Assert.That(iconGo.transform.parent, Is.EqualTo(rootRect));
                Assert.That(labelGo.transform.GetSiblingIndex(), Is.EqualTo(0));
                Assert.That(iconGo.transform.GetSiblingIndex(), Is.EqualTo(1));

                // 按钮自身 EmptyRaycast 作为固定交互区域
                var emptyRaycast = go.GetComponent<EmptyRaycast>();
                Assert.That(emptyRaycast, Is.Not.Null);
                Assert.That(go.GetComponents<EmptyRaycast>().Length, Is.EqualTo(1));

                // targetGraphic 指向 root 上的 Image
                Assert.That(button.targetGraphic, Is.EqualTo(newImage));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }


        [Test]
        public void ConvertToChildVisualKeepsExistingEmptyRaycast()
        {
            var go = new GameObject(
                "Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(ScaleButton), typeof(EmptyRaycast));
            try
            {
                Assert.That(ScaleButtonEditor.ConvertToChildVisual(go.GetComponent<ScaleButton>()), Is.True);
                Assert.That(go.GetComponents<EmptyRaycast>().Length, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ConvertToChildVisualIsNoopWhenImageNotOnButton()
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Button), typeof(ScaleButton));
            var visual = new GameObject("Image", typeof(RectTransform), typeof(Image));
            visual.transform.SetParent(go.transform, false);
            try
            {
                Assert.That(ScaleButtonEditor.ConvertToChildVisual(go.GetComponent<ScaleButton>()), Is.False);
                Assert.That(go.GetComponent<EmptyRaycast>(), Is.Null);
                Assert.That(visual.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(visual.transform.parent, Is.EqualTo(go.transform));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
