using UnityEngine;
using UnityEngine.EventSystems;

namespace Cascade.Core
{
    public enum UITooltipArrowEdge
    {
        Top,
        Bottom
    }

    /// <summary>Screen-space source geometry captured before opening a tooltip.</summary>
    public readonly struct UITooltipAnchor
    {
        public UITooltipAnchor(Rect screenRect, Vector2 screenPosition)
        {
            ScreenRect = screenRect;
            ScreenPosition = screenPosition;
        }

        public Rect ScreenRect { get; }
        public Vector2 ScreenPosition { get; }

        public static UITooltipAnchor FromPointer(PointerEventData eventData, RectTransform source)
        {
            var position = eventData != null ? eventData.position : Vector2.zero;
            return FromClick(new UIPointerClickData(position, source));
        }

        public static UITooltipAnchor FromClick(UIPointerClickData click)
        {
            var position = click.ScreenPosition;
            var source = click.Source;
            if (source == null)
                return new UITooltipAnchor(new Rect(position, Vector2.zero), position);

            var canvas = source.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var corners = new Vector3[4];
            source.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var max = min;
            for (var i = 1; i < corners.Length; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, corners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return new UITooltipAnchor(Rect.MinMaxRect(min.x, min.y, max.x, max.y), position);
        }
    }

    public readonly struct UITooltipPlacementOptions
    {
        public UITooltipPlacementOptions(float margin, float gap, float arrowInset)
        {
            Margin = Mathf.Max(0f, margin);
            Gap = Mathf.Max(0f, gap);
            ArrowInset = Mathf.Max(0f, arrowInset);
        }

        public float Margin { get; }
        public float Gap { get; }
        public float ArrowInset { get; }

        public static UITooltipPlacementOptions Default =>
            new UITooltipPlacementOptions(16f, 12f, 28f);
    }

    public readonly struct UITooltipPlacementResult
    {
        public UITooltipPlacementResult(Rect bubbleRect, UITooltipArrowEdge arrowEdge, float arrowOffset)
        {
            BubbleRect = bubbleRect;
            ArrowEdge = arrowEdge;
            ArrowOffset = arrowOffset;
        }

        public Rect BubbleRect { get; }
        public UITooltipArrowEdge ArrowEdge { get; }
        /// <summary>Arrow center measured from the bubble's left edge.</summary>
        public float ArrowOffset { get; }
    }

    /// <summary>Pure, screen-space tooltip placement and arrow alignment.</summary>
    public static class UITooltipPlacement
    {
        public static UITooltipPlacementResult Place(
            Rect viewport,
            UITooltipAnchor anchor,
            Vector2 requestedSize,
            UITooltipPlacementOptions options)
        {
            var available = Shrink(viewport, options.Margin);
            var width = Mathf.Min(Mathf.Max(1f, requestedSize.x), available.width);
            var height = Mathf.Min(Mathf.Max(1f, requestedSize.y), available.height);
            var anchorCenter = anchor.ScreenRect.size.sqrMagnitude > 0f
                ? anchor.ScreenRect.center
                : anchor.ScreenPosition;

            var aboveBottom = anchor.ScreenRect.yMax + options.Gap;
            var belowBottom = anchor.ScreenRect.yMin - options.Gap - height;
            var fitsAbove = aboveBottom + height <= available.yMax;
            var fitsBelow = belowBottom >= available.yMin;
            var aboveSpace = available.yMax - anchor.ScreenRect.yMax - options.Gap;
            var belowSpace = anchor.ScreenRect.yMin - available.yMin - options.Gap;
            var placeAbove = fitsAbove || (!fitsBelow && aboveSpace >= belowSpace);

            var x = Mathf.Clamp(anchorCenter.x - width * 0.5f, available.xMin, available.xMax - width);
            var y = placeAbove ? aboveBottom : belowBottom;
            y = Mathf.Clamp(y, available.yMin, available.yMax - height);

            var arrowInset = Mathf.Min(options.ArrowInset, width * 0.5f);
            var arrowOffset = Mathf.Clamp(anchorCenter.x - x, arrowInset, width - arrowInset);
            var arrowEdge = placeAbove ? UITooltipArrowEdge.Bottom : UITooltipArrowEdge.Top;
            return new UITooltipPlacementResult(new Rect(x, y, width, height), arrowEdge, arrowOffset);
        }

        private static Rect Shrink(Rect rect, float margin)
        {
            var maxMargin = Mathf.Min(margin, Mathf.Min(rect.width, rect.height) * 0.5f);
            return Rect.MinMaxRect(
                rect.xMin + maxMargin,
                rect.yMin + maxMargin,
                rect.xMax - maxMargin,
                rect.yMax - maxMargin);
        }
    }
}
