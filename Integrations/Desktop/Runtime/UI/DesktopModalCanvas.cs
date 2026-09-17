using UnityEngine;
using UnityEngine.UI;

namespace Cascade.Integrations.Desktop
{
    /// <summary>确保存在一个高 sortingOrder 的 Overlay Canvas，供模态/Toast 挂载。</summary>
    public static class DesktopModalCanvas
    {
        const string RootName = "CascadeDesktopModalCanvas";
        static Canvas _canvas;

        public static Canvas Ensure(int sortingOrder = 5000)
        {
            if (_canvas != null)
                return _canvas;

            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                _canvas = existing.GetComponent<Canvas>();
                if (_canvas != null)
                    return _canvas;
            }

            var go = new GameObject(RootName);
            Object.DontDestroyOnLoad(go);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;
            go.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            go.AddComponent<GraphicRaycaster>();
            return _canvas;
        }

        public static Transform Root => Ensure().transform;
    }
}
