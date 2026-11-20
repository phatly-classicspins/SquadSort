using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The screen scene's <c>WorldRoot</c> node, handed to the screen controller by its scope so the
    /// controller never searches scenes by name. Renderer-drawn world content is parented here (rule #16).
    /// </summary>
    public sealed class ScreenWorldRoot
    {
        public ScreenWorldRoot(Transform transform) => Transform = transform;

        public Transform Transform { get; }
    }
}
