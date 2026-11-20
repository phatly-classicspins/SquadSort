using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    internal static class BoosterRewardFlyAnimation
    {
        public const float DefaultStartSize = 220f;
        public const float DefaultHoldDuration = 0.35f;
        public const float DefaultFlyDuration = 0.65f;
        public const float DefaultEndScale = 0.42f;

        public static Sequence Play(
            Sprite icon,
            Transform animationLayer,
            RectTransform target,
            float startSize,
            float holdDuration,
            float flyDuration,
            float endScale,
            Action onLanded)
        {
            if (icon == null || animationLayer == null || target == null)
            {
                onLanded?.Invoke();
                return null;
            }

            var iconObject = new GameObject(
                "Flying Booster Icon",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            var flyingIcon = iconObject.GetComponent<Image>();
            flyingIcon.sprite = icon;
            flyingIcon.raycastTarget = false;
            flyingIcon.preserveAspect = true;

            var rect = flyingIcon.rectTransform;
            rect.SetParent(animationLayer, false);
            rect.anchorMin = Vector2.one * 0.5f;
            rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.one * startSize;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();

            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(iconObject);
            sequence.AppendInterval(holdDuration);
            sequence.Insert(
                holdDuration,
                rect.DOMove(target.position, flyDuration).SetEase(Ease.InOutQuad));
            sequence.Insert(
                holdDuration,
                rect.DOScale(endScale, flyDuration).SetEase(Ease.InQuad));
            sequence.OnComplete(() =>
            {
                target.DOKill();
                target.localScale = Vector3.one;
                target.DOPunchScale(Vector3.one * 0.12f, 0.22f, 5, 0.45f)
                    .SetUpdate(true);
                onLanded?.Invoke();
            });
            sequence.OnKill(() =>
            {
                if (iconObject != null)
                    UnityEngine.Object.Destroy(iconObject);
            });
            return sequence;
        }
    }
}
