using Cascade.Modules.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cascade.Modules.UI.Editor
{
    /// <summary>
    /// Newly added decorative uGUI graphics default raycastTarget off. Interactive
    /// controls restore raycasts on the Graphic that owns their hit area.
    /// </summary>
    [InitializeOnLoad]
    public static class ComponentAddListener
    {
        static ComponentAddListener()
        {
            ObjectFactory.componentWasAdded += OnComponentWasAdded;
        }

        private static void OnComponentWasAdded(Component component)
        {
            if (component is MaskableGraphic graphic)
            {
                graphic.raycastTarget =
                    graphic.GetComponent<Selectable>() != null ||
                    graphic.GetComponent<IPointerClickHandler>() != null ||
                    graphic.GetComponent<EmptyRaycast>() !=null;
                return;
            }

            if (component is Selectable || component is IPointerClickHandler)
            {
                var graphics = component.GetComponents<MaskableGraphic>();
                for (var i = 0; i < graphics.Length; i++)
                    graphics[i].raycastTarget = true;
            }
        }
    }
}
