using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    /// <summary>
    /// The Undo booster's full-screen "◀◀ REW" video-tape rewind (Squad Sort GDD §1.5, §3.4): a cool tint, scanlines rolling
    /// down, the REW tag blinking in the corner and a row of chevrons sweeping right to left across the middle. It answers
    /// how the rewind looks; the screen says how long it holds. It removes itself once it has faded out.
    /// </summary>
    public sealed class RewindOverlayView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _scanlines;     // tiled, one tile taller than the screen; rolls by one tile
        [SerializeField] private float _scanlineTile = 8f;
        [SerializeField] private float _scanlineSpeed = 160f;  // units per second
        [SerializeField] private GameObject _tag;              // the corner "◀◀ REW"
        [SerializeField] private float _blinkPeriod = 0.5f;
        [SerializeField] private Graphic[] _chevrons;          // right to left: the lit one walks left
        [SerializeField] private float _sweepPeriod = 0.36f;
        [SerializeField, Range(0f, 1f)] private float _chevronDim = 0.25f;
        [SerializeField] private float _fadeIn = 0.12f;
        [SerializeField] private float _fadeOut = 0.22f;

        private float[] _chevronAlpha;   // each chevron's prefab alpha: the sweep scales it, never replaces it
        private float _hold;
        private float _elapsed;
        private bool _playing;

        /// <summary>Fade in, hold for <paramref name="hold"/> seconds, fade out and remove itself.</summary>
        public void Play(float hold)
        {
            _hold = Mathf.Max(0f, hold);
            _elapsed = 0f;
            _playing = true;
            _group.alpha = 0f;
            if (_chevronAlpha == null && _chevrons != null)
            {
                _chevronAlpha = new float[_chevrons.Length];
                for (int i = 0; i < _chevrons.Length; i++) _chevronAlpha[i] = _chevrons[i].color.a;
            }
            Draw();
        }

        private void Update()
        {
            if (!_playing) return;
            _elapsed += Time.unscaledDeltaTime;
            Draw();
            if (_elapsed >= _fadeIn + _hold + _fadeOut) Destroy(gameObject);
        }

        private void Draw()
        {
            float t = _elapsed;
            float fadeIn = _fadeIn > 0f ? t / _fadeIn : 1f;
            float fadeOut = _fadeOut > 0f ? (_fadeIn + _hold + _fadeOut - t) / _fadeOut : 1f;
            _group.alpha = Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut));

            if (_scanlines != null && _scanlineTile > 0f)
            {
                Vector2 p = _scanlines.anchoredPosition;
                p.y = -Mathf.Repeat(t * _scanlineSpeed, _scanlineTile);
                _scanlines.anchoredPosition = p;
            }

            if (_tag != null && _blinkPeriod > 0f) _tag.SetActive(Mathf.Repeat(t, _blinkPeriod * 2f) < _blinkPeriod * 1.4f);

            if (_chevrons == null || _chevrons.Length == 0 || _sweepPeriod <= 0f) return;
            float head = Mathf.Repeat(t / _sweepPeriod, 1f) * _chevrons.Length;
            for (int i = 0; i < _chevrons.Length; i++)
            {
                float lit = Mathf.Clamp01(1f - Mathf.Abs(head - i - 0.5f));
                Color c = _chevrons[i].color;
                c.a = _chevronAlpha[i] * Mathf.Lerp(_chevronDim, 1f, lit);
                _chevrons[i].color = c;
            }
        }
    }
}
