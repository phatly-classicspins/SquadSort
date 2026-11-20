using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    /// <summary>
    /// A full-rect scrim with one rounded hole, drawn as a single mesh: <see cref="Graphic.color"/> outside the hole,
    /// <see cref="SpotColor"/> inside it (the lit target of a tutorial). The hole's corners are truly round — four shade
    /// rectangles round a rounded highlight would leave lit corners. Never a raycast target: the player plays through it.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpotlightScrim : MaskableGraphic, ICanvasRaycastFilter
    {
        private const int ArcSegments = 8;

        [SerializeField] private Color _spotColor = new Color(1f, 1f, 1f, 0.4f);
        [SerializeField, Min(0f)] private float _radius = 22f;
        private Rect _hole, _secondHole;

        public Color SpotColor { get => _spotColor; set { _spotColor = value; SetVerticesDirty(); } }

        /// <summary>The hole, in this graphic's local space; an empty rect draws the plain scrim.</summary>
        public void SetHole(Rect hole)
        {
            _hole = hole;
            _secondHole = default;
            SetVerticesDirty();
        }

        public void SetHoles(Rect first, Rect second)
        {
            _hole = first;
            _secondHole = second;
            SetVerticesDirty();
        }

        private bool _blockOutside;
        public void BlockOutsideHole(bool block) { _blockOutside = block; raycastTarget = block; }
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!_blockOutside) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var point)) return true;
            return !_hole.Contains(point) && !_secondHole.Contains(point);
        }

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }

        private const float Bleed = 4000f;   // canvas units past each edge of the rect

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            // Bleed far past the rect, so the dim never stops short of a screen edge whatever size the world-space popup host's
            // rect has against the visible world (it reads 720x1280 in a 720x1280 Game view while the rig is 1080x1920).
            var visible = rectTransform.rect;
            var outer = Rect.MinMaxRect(visible.xMin - Bleed, visible.yMin - Bleed, visible.xMax + Bleed, visible.yMax + Bleed);
            if (_secondHole.width > 0f && _secondHole.height > 0f)
            {
                // Two separate rounded spots without doubling the dim between them.
                if (_hole.yMax <= _secondHole.yMin || _secondHole.yMax <= _hole.yMin)
                {
                    var low = _hole.center.y < _secondHole.center.y ? _hole : _secondHole;
                    var high = _hole.center.y < _secondHole.center.y ? _secondHole : _hole;
                    float split = (low.yMax + high.yMin) / 2f;
                    DrawHole(vh, Rect.MinMaxRect(outer.xMin, outer.yMin, outer.xMax, split), low);
                    DrawHole(vh, Rect.MinMaxRect(outer.xMin, split, outer.xMax, outer.yMax), high);
                    return;
                }
                // side by side (two tubes on one row): the same split, left and right
                if (_hole.xMax <= _secondHole.xMin || _secondHole.xMax <= _hole.xMin)
                {
                    var left = _hole.center.x < _secondHole.center.x ? _hole : _secondHole;
                    var right = _hole.center.x < _secondHole.center.x ? _secondHole : _hole;
                    float split = (left.xMax + right.xMin) / 2f;
                    DrawHole(vh, Rect.MinMaxRect(outer.xMin, outer.yMin, split, outer.yMax), left);
                    DrawHole(vh, Rect.MinMaxRect(split, outer.yMin, outer.xMax, outer.yMax), right);
                    return;
                }
            }
            DrawHole(vh, outer, _hole);
        }

        private void DrawHole(VertexHelper vh, Rect outer, Rect requested)
        {
            int start = vh.currentVertCount;
            Color32 scrim = color, spot = _spotColor;
            if (requested.width <= 0f || requested.height <= 0f)
            {
                Quad(vh, outer.min, new Vector2(outer.xMin, outer.yMax), outer.max, new Vector2(outer.xMax, outer.yMin), scrim);
                return;
            }

            var hole = Rect.MinMaxRect(Mathf.Max(requested.xMin, outer.xMin), Mathf.Max(requested.yMin, outer.yMin),
                Mathf.Min(requested.xMax, outer.xMax), Mathf.Min(requested.yMax, outer.yMax));
            float r = Mathf.Min(_radius, hole.width / 2f, hole.height / 2f);
            // the hole's outline, counter-clockwise: bottom-right, top-right, top-left, bottom-left corner arcs
            var centres = new[]
            {
                new Vector2(hole.xMax - r, hole.yMin + r), new Vector2(hole.xMax - r, hole.yMax - r),
                new Vector2(hole.xMin + r, hole.yMax - r), new Vector2(hole.xMin + r, hole.yMin + r),
            };
            var corners = new[]
            {
                new Vector2(outer.xMax, outer.yMin), outer.max, new Vector2(outer.xMin, outer.yMax), outer.min,
            };
            int perArc = ArcSegments + 1;
            for (int k = 0; k < 4; k++)
                for (int s = 0; s < perArc; s++)
                {
                    float a = (-90f + 90f * k + 90f * s / ArcSegments) * Mathf.Deg2Rad;
                    vh.AddVert(centres[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, scrim, Vector4.zero);
                }
            int outerStart = vh.currentVertCount;
            foreach (var c in corners) vh.AddVert(c, scrim, Vector4.zero);
            for (int k = 0; k < 4; k++)
            {
                int first = start + k * perArc, last = first + ArcSegments, next = start + ((k + 1) % 4) * perArc;
                int nextCorner = (k + 1) % 4;
                for (int s = first; s < last; s++) vh.AddTriangle(s, s + 1, outerStart + k);       // between the arc and its corner
                vh.AddTriangle(last, next, outerStart + nextCorner);                            // the straight edge to the next arc
                vh.AddTriangle(last, outerStart + nextCorner, outerStart + k);
            }

            // the lit inside: a fan from the hole's centre over the same outline
            int centre = vh.currentVertCount;
            vh.AddVert(hole.center, spot, Vector4.zero);
            int ringStart = vh.currentVertCount;
            for (int k = 0; k < 4; k++)
                for (int s = 0; s < perArc; s++)
                {
                    float a = (-90f + 90f * k + 90f * s / ArcSegments) * Mathf.Deg2Rad;
                    vh.AddVert(centres[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, spot, Vector4.zero);
                }
            int ring = 4 * perArc;
            for (int i = 0; i < ring; i++) vh.AddTriangle(centre, ringStart + i, ringStart + (i + 1) % ring);
        }

        private static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector4.zero); vh.AddVert(b, col, Vector4.zero);
            vh.AddVert(c, col, Vector4.zero); vh.AddVert(d, col, Vector4.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
