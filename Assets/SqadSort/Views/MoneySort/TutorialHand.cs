using UnityEngine;

namespace Game.Views
{
    /// <summary>
    /// Places the tutorial pointing hand so the fingertip lands on a point
    /// of a spot. Shared by the element / booster tutorials and the level 1 FTUE. The hand is <c>size</c> of the screen's width; it
    /// hangs down-right of the point, and turns 90° to come from above when that would leave the screen (the booster bar).
    /// </summary>
    public static class TutorialHand
    {
        // the skeleton's own measures at scale 1 (TutorialHand.json, idle frame): the fingertip and the image's rect
        private static readonly Vector2 TipUnit = new Vector2(-315.5f, 300.5f);
        private static readonly Rect BoundsUnit = new Rect(-383.5f, -322.5f, 651f, 653f);

        /// <param name="hand">The hand, a child of the space <paramref name="bounds"/> and <paramref name="tip"/> are in.</param>
        /// <returns>The hand's rect in that space (a hint panel keeps clear of it).</returns>
        public static Rect Place(RectTransform hand, Rect bounds, Vector2 tip, float size, float? angle = null)
        {
            // PointHand is a downward static sprite; retain the old angle convention for callers.
            bool sprite = hand.GetComponent<UnityEngine.UI.Image>() != null;
            var unitBounds = sprite ? new Rect(-55f, -78.5f, 106f, 148f) : BoundsUnit;
            var unitTip = sprite ? new Vector2(7f, -63f) : TipUnit;
            float scale = bounds.width * size / unitBounds.width;
            var baseTurn = Quaternion.Euler(0f, 0f, sprite ? -135f : 0f);
            float below = 0f;
            foreach (var corner in new[] { unitBounds.min, unitBounds.max,
                new Vector2(unitBounds.xMin, unitBounds.yMax), new Vector2(unitBounds.xMax, unitBounds.yMin) })
                below = Mathf.Max(below, -(baseTurn * ((Vector3)(corner - unitTip) * scale)).y);
            var turn = Quaternion.Euler(0f, 0f, (angle ?? (tip.y - below < bounds.yMin ? 90f : 0f)) - (sprite ? 135f : 0f));
            Vector2 Turned(Vector2 v) => turn * (v * scale);
            Vector2 origin = tip - Turned(unitTip);
            hand.localScale = Vector3.one * scale;
            hand.localRotation = turn;
            hand.localPosition = origin;
            hand.gameObject.SetActive(true);
            Vector2 a = origin + Turned(unitBounds.min), b = origin + Turned(unitBounds.max);
            Vector2 c = origin + Turned(new Vector2(unitBounds.xMin, unitBounds.yMax)), d = origin + Turned(new Vector2(unitBounds.xMax, unitBounds.yMin));
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x, c.x, d.x), Mathf.Min(a.y, b.y, c.y, d.y), Mathf.Max(a.x, b.x, c.x, d.x), Mathf.Max(a.y, b.y, c.y, d.y));
        }
    }
}
