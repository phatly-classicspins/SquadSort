using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    // Views share one canvas and stack by sibling order; lifting a child into its own sorted
    // canvas draws it (and keeps it tappable) above later views such as a popup's dark backdrop.
    public static class CanvasLift
    {
        public static void Set(GameObject target, bool lifted)
        {
            if (target == null)
                return;
            var canvas = target.GetComponent<Canvas>();
            if (canvas == null)
            {
                if (!lifted)
                    return;
                canvas = target.AddComponent<Canvas>();
                target.AddComponent<GraphicRaycaster>();
            }
            if (canvas.overrideSorting == lifted)
                return;
            var root = canvas.rootCanvas;
            canvas.overrideSorting = lifted;
            canvas.sortingLayerID = root.sortingLayerID;
            canvas.sortingOrder = root.sortingOrder + 1;
        }
    }
}
