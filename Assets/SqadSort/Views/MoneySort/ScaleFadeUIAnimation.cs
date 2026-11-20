using DG.Tweening;
using UnityEngine;

namespace Game.Views
{
    public sealed class ScaleFadeUIAnimation : UIAnimation
    {
        [Header("Targets")]
        [SerializeField] private RectTransform target;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Scale")]
        [SerializeField] private Vector3 hiddenScale = new(0.82f, 0.82f, 1f);
        [SerializeField] private Vector3 shownScale = Vector3.one;

        [Header("Alpha")]
        [SerializeField, Range(0f, 1f)] private float hiddenAlpha;
        [SerializeField, Range(0f, 1f)] private float shownAlpha = 1f;

        private void Awake() => EnsureTargets();

        private void EnsureTargets()
        {
            if (target == null)
                target = transform as RectTransform;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        }

        protected override Tween CreateShowTween()
        {
            EnsureTargets();
            return DOTween.Sequence()
                .Join(target.DOScale(shownScale, showDuration).SetEase(showEase))
                .Join(DOTween.To(
                    () => canvasGroup.alpha,
                    value => canvasGroup.alpha = value,
                    shownAlpha,
                    showDuration));
        }

        protected override Tween CreateHideTween()
        {
            EnsureTargets();
            return DOTween.Sequence()
                .Join(target.DOScale(hiddenScale, hideDuration).SetEase(hideEase))
                .Join(DOTween.To(
                    () => canvasGroup.alpha,
                    value => canvasGroup.alpha = value,
                    hiddenAlpha,
                    hideDuration));
        }

        protected override void SetShownState()
        {
            EnsureTargets();
            target.localScale = shownScale;
            canvasGroup.alpha = shownAlpha;
        }

        protected override void SetHiddenState()
        {
            EnsureTargets();
            target.localScale = hiddenScale;
            canvasGroup.alpha = hiddenAlpha;
        }

        protected override void SetInteraction(bool enabled)
        {
            EnsureTargets();
            canvasGroup.interactable = enabled;
            canvasGroup.blocksRaycasts = enabled;
        }
    }
}
