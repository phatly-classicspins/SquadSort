using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    // Idle attention loop for a buy button: a light streak sweeps across the button face,
    // then the price label gives a small bounce. The button's own scale is left alone so
    // ButtonScaleAnimation keeps the press feedback.
    public sealed class PriceButtonShine : MonoBehaviour
    {
        [Header("Shine")]
        [SerializeField] private Sprite _shineSprite;
        [SerializeField] private Color _shineColor = new(1f, 1f, 1f, 0.75f);
        [SerializeField, Min(1f)] private float _shineWidth = 46f;
        [SerializeField] private float _shineAngle = -20f;
        [SerializeField, Min(0.05f)] private float _sweepDuration = 0.55f;
        [Tooltip("Shrinks the clip area from the button edges (x: left/right, y: top/bottom).")]
        [SerializeField] private Vector2 _maskInset = new(14f, 10f);

        [Header("Label bounce")]
        [Tooltip("Price label to bounce after each sweep; optional.")]
        [SerializeField] private RectTransform _label;
        [SerializeField] private float _bounceStrength = 0.12f;
        [SerializeField, Min(0.05f)] private float _bounceDuration = 0.35f;

        [Header("Timing")]
        [SerializeField] private Vector2 _intervalRange = new(2.2f, 3.2f);

        private RectTransform _shine;
        private Sequence _loop;
        private Vector3 _labelScale = Vector3.one;

        private void Awake()
        {
            if (_label != null)
                _labelScale = _label.localScale;
            if (_shineSprite != null)
                _shine = CreateShine();
        }

        // Clipped by a RectMask2D (no stencil, keeps batching); the inset and soft edges keep
        // the streak off the rounded corners. Inserted as the first child so it draws under the price text.
        private RectTransform CreateShine()
        {
            var maskGo = new GameObject("ShineMask", typeof(RectTransform), typeof(RectMask2D));
            var maskRect = (RectTransform)maskGo.transform;
            maskRect.SetParent(transform, false);
            maskRect.SetAsFirstSibling();
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = new Vector2(_maskInset.x, _maskInset.y);
            maskRect.offsetMax = new Vector2(-_maskInset.x, -_maskInset.y);
            maskGo.GetComponent<RectMask2D>().softness = new Vector2Int(Mathf.RoundToInt(_maskInset.x), 0);

            var shineGo = new GameObject("Shine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var shine = (RectTransform)shineGo.transform;
            shine.SetParent(maskRect, false);
            shine.anchorMin = shine.anchorMax = shine.pivot = new Vector2(0.5f, 0.5f);
            shine.localEulerAngles = new Vector3(0f, 0f, _shineAngle);
            var image = shineGo.GetComponent<Image>();
            image.sprite = _shineSprite;
            image.color = _shineColor;
            image.raycastTarget = false;
            shine.gameObject.SetActive(false);
            return shine;
        }

        private void OnEnable()
        {
            _loop?.Kill();
            // Random first delay so a grid of buttons does not flash in sync.
            Play(Random.Range(0.3f, _intervalRange.y));
        }

        private void Play(float delay)
        {
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject).AppendInterval(delay);
            // Sold-out buttons stay quiet but keep the loop alive in case they come back on sale.
            var button = GetComponent<Button>();
            if (button != null && !button.interactable)
            {
                _loop = sequence.OnComplete(() => Play(_intervalRange.x));
                return;
            }
            if (_shine != null)
            {
                var size = ((RectTransform)transform).rect.size;
                var travel = (size.x + _shineWidth) * 0.5f + size.y;
                _shine.sizeDelta = new Vector2(_shineWidth, size.y * 2f);
                sequence
                    .AppendCallback(() =>
                    {
                        _shine.anchoredPosition = new Vector2(-travel, 0f);
                        _shine.gameObject.SetActive(true);
                    })
                    .Append(DOTween.To(
                            () => _shine.anchoredPosition.x,
                            x => _shine.anchoredPosition = new Vector2(x, 0f),
                            travel,
                            _sweepDuration)
                        .SetEase(Ease.InOutSine))
                    .AppendCallback(() => _shine.gameObject.SetActive(false));
            }
            if (_label != null)
                sequence.Append(_label.DOPunchScale(_labelScale * _bounceStrength, _bounceDuration, 6, 0.6f));
            _loop = sequence.OnComplete(() => Play(Random.Range(_intervalRange.x, _intervalRange.y)));
        }

        private void OnDisable()
        {
            _loop?.Kill();
            _loop = null;
            if (_shine != null)
                _shine.gameObject.SetActive(false);
            if (_label != null)
                _label.localScale = _labelScale;
        }
    }
}
