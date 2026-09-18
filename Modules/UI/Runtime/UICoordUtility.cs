using UnityEngine;

namespace Cascade.Modules.UI
{
    /// <summary>
    /// World ↔ screen/canvas geometry helpers for world-tracked UI widgets.
    /// </summary>
    public static class UICoordUtility
    {
        public static bool TryWorldToScreen(
            Camera worldCamera,
            Vector3 worldPosition,
            out Vector2 screenPoint,
            out bool inFront)
        {
            screenPoint = default;
            inFront = false;
            if (worldCamera == null)
                return false;

            var sp = worldCamera.WorldToScreenPoint(worldPosition);
            inFront = sp.z > 0f;
            screenPoint = new Vector2(sp.x, sp.y);
            return true;
        }

        public static bool TryWorldToCanvasLocal(
            Camera worldCamera,
            RectTransform canvasRect,
            Canvas canvas,
            Vector3 worldPosition,
            out Vector2 localPoint,
            out bool inFront)
        {
            localPoint = default;
            inFront = false;
            if (canvasRect == null)
                return false;

            if (!TryWorldToScreen(worldCamera, worldPosition, out var screenPoint, out inFront))
                return false;

            Camera eventCam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCam = canvas.worldCamera != null ? canvas.worldCamera : worldCamera;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                eventCam,
                out localPoint);
        }

        public static bool IsInsidePixelRect(Camera worldCamera, Vector2 screenPoint, float marginPx = 0f)
        {
            if (worldCamera == null)
                return false;

            var rect = worldCamera.pixelRect;
            return screenPoint.x >= rect.xMin - marginPx
                   && screenPoint.x <= rect.xMax + marginPx
                   && screenPoint.y >= rect.yMin - marginPx
                   && screenPoint.y <= rect.yMax + marginPx;
        }
    }
}
