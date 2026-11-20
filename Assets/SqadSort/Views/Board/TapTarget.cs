using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Views
{
    /// <summary>
    /// A flat, camera-square tap area on the 3D board (a tube tray, a plaza tile). The GamePlay layer
    /// raycasts in 2D, so its <see cref="BoxCollider2D"/> is kept world-aligned. Relays the tap; it knows
    /// nothing about what was tapped.
    /// </summary>
    public sealed class TapTarget : MonoBehaviour, IPointerClickHandler
    {
        public event Action Tapped;

        /// <summary>Add a tap area of <paramref name="screenSize"/> world units under <paramref name="parent"/>.</summary>
        public static TapTarget Create(string name, Transform parent, Vector3 localPosition, Vector2 screenSize)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.rotation = Quaternion.identity;
            go.AddComponent<BoxCollider2D>().size = screenSize;
            return go.AddComponent<TapTarget>();
        }

        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke();
    }
}
