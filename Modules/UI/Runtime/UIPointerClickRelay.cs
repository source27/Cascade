using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cascade.Modules.UI
{
    /// <summary>Reports pointer position and source RectTransform to page-owned UI logic.</summary>
    public sealed class UIPointerClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public event Action<UIPointerClickData> Clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null)
                return;
            Clicked?.Invoke(new UIPointerClickData(eventData.position, transform as RectTransform));
        }
    }

    public readonly struct UIPointerClickData
    {
        public UIPointerClickData(Vector2 screenPosition, RectTransform source)
        {
            ScreenPosition = screenPosition;
            Source = source;
        }

        public Vector2 ScreenPosition { get; }
        public RectTransform Source { get; }
    }
}
