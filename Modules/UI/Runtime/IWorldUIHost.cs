using System;
using UnityEngine;

namespace Cascade.Modules.UI
{
    public interface IWorldUIHandle : IDisposable
    {
        RectTransform Widget { get; }
        bool IsValid { get; }
        void SetVisible(bool visible);
        void SetScreenOffset(Vector2 offsetPx);
        void SetWorldAnchor(Transform anchor);
    }

    public struct WorldUiTrackOptions
    {
        public Transform WorldAnchor;
        public Func<Vector3> WorldPositionProvider;
        public Vector2 ScreenOffsetPx;
        public bool HideWhenOffscreen;
        public bool HideWhenBehindCamera;

        public static WorldUiTrackOptions DefaultFeetBar(Transform anchor, Vector2 screenOffsetPx)
        {
            return new WorldUiTrackOptions
            {
                WorldAnchor = anchor,
                ScreenOffsetPx = screenOffsetPx,
                HideWhenOffscreen = true,
                HideWhenBehindCamera = true
            };
        }
    }

    public interface IWorldUIHost
    {
        RectTransform Root { get; }
        Camera WorldCamera { get; set; }

        IWorldUIHandle Track(RectTransform widget, in WorldUiTrackOptions options);
        IWorldUIHandle Track(GameObject widgetInstance, in WorldUiTrackOptions options);
        void Untrack(IWorldUIHandle handle);
        void Clear();

        /// <summary>Test/editor hook: run one projection pass immediately.</summary>
        void TickNow();
    }
}
