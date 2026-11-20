using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    // Idle twinkle for shop cards: a few star sprites pop in at random spots over the card,
    // spin and fade out, then reappear elsewhere. Stars are created at runtime above the
    // card art (last children of this transform) and never block clicks.
    public sealed class ShopCardSparkles : MonoBehaviour
    {
        [Header("Area")]
        [Tooltip("Stars spawn inside this rect; defaults to this transform.")]
        [SerializeField] private RectTransform _area;
        [Tooltip("Inset from the area edges, in area pixels (negative spills outside).")]
        [SerializeField] private Vector2 _padding = new(12f, 12f);

        [Header("Stars")]
        [SerializeField] private Sprite _sprite;
        [SerializeField] private Color _color = new(1f, 0.97f, 0.8f, 1f);
        [SerializeField, Min(1)] private int _count = 3;
        [SerializeField] private Vector2 _sizeRange = new(42f, 66f);

        [Header("Timing")]
        [SerializeField, Min(0.05f)] private float _twinkleDuration = 0.7f;
        [SerializeField] private Vector2 _delayRange = new(0.4f, 1.8f);

        private readonly List<RectTransform> _stars = new();
        private readonly List<Sequence> _tweens = new();

        private void Awake()
        {
            if (_area == null)
                _area = transform as RectTransform;
            if (_sprite == null)
                return;
            for (var i = 0; i < _count; i++)
                _stars.Add(CreateStar(i));
        }

        private RectTransform CreateStar(int index)
        {
            var go = new GameObject($"Sparkle{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.SetAsLastSibling();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var image = go.GetComponent<Image>();
            image.sprite = _sprite;
            image.color = _color;
            image.raycastTarget = false;
            rect.localScale = Vector3.zero;
            return rect;
        }

        private void OnEnable()
        {
            for (var i = 0; i < _stars.Count; i++)
                Twinkle(i, Random.Range(0f, _delayRange.y));
        }

        private void Twinkle(int index, float delay)
        {
            var star = _stars[index];
            var image = star.GetComponent<Image>();
            var size = Random.Range(_sizeRange.x, _sizeRange.y);
            star.sizeDelta = new Vector2(size, size);
            star.localPosition = RandomPoint();
            star.localScale = Vector3.zero;
            star.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 45f));
            image.color = _color;

            var half = _twinkleDuration * 0.5f;
            var sequence = DOTween.Sequence()
                .SetUpdate(true)
                .SetLink(star.gameObject)
                .AppendInterval(delay)
                .Append(star.DOScale(1f, half).SetEase(Ease.OutBack))
                .Join(star.DOLocalRotate(new Vector3(0f, 0f, 90f), _twinkleDuration, RotateMode.LocalAxisAdd).SetEase(Ease.Linear))
                .Append(star.DOScale(0f, half).SetEase(Ease.InQuad))
                .Join(DOTween.To(() => image.color.a, a => image.color = WithAlpha(image.color, a), 0f, half))
                .OnComplete(() => Twinkle(index, Random.Range(_delayRange.x, _delayRange.y)));
            SetTween(index, sequence);
        }

        // Picks a point inside the area and converts it to this transform's space, where the stars live.
        private Vector3 RandomPoint()
        {
            var rect = _area.rect;
            var x = Random.Range(rect.xMin + _padding.x, rect.xMax - _padding.x);
            var y = Random.Range(rect.yMin + _padding.y, rect.yMax - _padding.y);
            var point = transform.InverseTransformPoint(_area.TransformPoint(new Vector3(x, y, 0f)));
            point.z = 0f;
            return point;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private void SetTween(int index, Sequence sequence)
        {
            while (_tweens.Count <= index)
                _tweens.Add(null);
            _tweens[index]?.Kill();
            _tweens[index] = sequence;
        }

        private void OnDisable()
        {
            foreach (var tween in _tweens)
                tween?.Kill();
            _tweens.Clear();
            foreach (var star in _stars)
            {
                if (star != null)
                    star.localScale = Vector3.zero;
            }
        }
    }
}
