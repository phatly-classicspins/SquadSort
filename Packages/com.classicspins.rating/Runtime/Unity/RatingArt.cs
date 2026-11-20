using UnityEngine;

namespace ClassicSpins.Rating
{
    /// <summary>
    /// The default popup's shapes, drawn into textures at first use (white, anti-aliased, tinted by the style), so the
    /// package needs no art: a rounded box (9-sliced) and a five-point star.
    /// </summary>
    internal static class RatingArt
    {
        private static Sprite _round, _star, _circle;

        /// <summary>A white rounded rectangle, 9-sliced with a 40 px corner.</summary>
        public static Sprite Round => _round != null ? _round : _round = RoundedBox(128, 40f);

        /// <summary>A white circle (for the close button).</summary>
        public static Sprite Circle => _circle != null ? _circle : _circle = RoundedBox(128, 64f, sliced: false);

        /// <summary>A white five-point star with softly rounded tips.</summary>
        public static Sprite Star => _star != null ? _star : _star = MakeStar(160);

        private static Sprite RoundedBox(int size, float radius, bool sliced = true)
        {
            var tex = NewTexture(size, "RatingRound");
            var px = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // signed distance to a rounded square filling the texture
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                byte a = (byte)(Mathf.Clamp01(0.5f - outside) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Sprite MakeStar(int size)
        {
            var tex = NewTexture(size, "RatingStar");
            var pts = new Vector2[10];
            float c = size / 2f, outer = size * 0.48f, inner = outer * 0.47f;
            for (int i = 0; i < 10; i++)
            {
                float r = i % 2 == 0 ? outer : inner;
                float ang = Mathf.PI / 2f + i * Mathf.PI / 5f;   // first tip straight up
                pts[i] = new Vector2(c + r * Mathf.Cos(ang), c + r * Mathf.Sin(ang) - size * 0.03f);
            }
            const int ss = 4;   // 4×4 supersampling
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                    if (Inside(pts, new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss))) hits++;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / (ss * ss)));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        private static Texture2D NewTexture(int size, string name) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
    }
}
