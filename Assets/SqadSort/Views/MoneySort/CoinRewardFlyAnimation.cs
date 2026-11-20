using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    // Coins pop out of the pressed button and stream up into the coin bar one after another,
    // as in the reference ending video (about 20 coins, done within a second).
    internal static class CoinRewardFlyAnimation
    {
        public const int MaxCoins = 20;
        private const float BurstDuration = 0.15f;
        private const float BurstRadius = 120f;
        private const float FlyDuration = 0.5f;
        private const float Stagger = 0.03f;
        private const float CoinSize = 64f;
        // Short pause after the last coin lands so the counted up balance is seen before the popup closes.
        private const float HoldAfterLanding = 0.35f;

        // onCoinLanded gets (coins landed so far, coins flying). onFinished runs once,
        // also when the animation is killed early, so callers can always continue.
        public static Sequence Play(
            Sprite icon,
            RectTransform source,
            Transform animationLayer,
            RectTransform target,
            int amount,
            Action<int, int> onCoinLanded,
            Action onFinished)
        {
            if (icon == null || source == null || animationLayer == null ||
                target == null || amount <= 0)
            {
                onFinished?.Invoke();
                return null;
            }

            var root = new GameObject("Flying Coins", typeof(RectTransform));
            var rootRect = (RectTransform)root.transform;
            rootRect.SetParent(animationLayer, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.SetAsLastSibling();

            // Coins shrink to the target icon size, so they land as the coin bar icon itself.
            var endScale = target.rect.width > 0f ? Mathf.Clamp(target.rect.width / CoinSize, 0.3f, 1f) : 0.6f;
            var count = Mathf.Clamp(amount, 1, MaxCoins);
            var landed = 0;
            var sequence = DOTween.Sequence().SetUpdate(true).SetLink(root);
            for (var index = 0; index < count; index++)
            {
                var coinObject = new GameObject(
                    "Coin",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                var coin = coinObject.GetComponent<Image>();
                coin.sprite = icon;
                coin.raycastTarget = false;
                coin.preserveAspect = true;

                var rect = coin.rectTransform;
                rect.SetParent(rootRect, false);
                rect.anchorMin = Vector2.one * 0.5f;
                rect.anchorMax = Vector2.one * 0.5f;
                rect.pivot = Vector2.one * 0.5f;
                rect.sizeDelta = Vector2.one * CoinSize;
                rect.position = source.position;
                rect.localScale = Vector3.zero;

                var burst = rect.localPosition + (Vector3)(UnityEngine.Random.insideUnitCircle * BurstRadius);
                var flyStart = BurstDuration + index * Stagger;
                sequence.Insert(0f, rect.DOScale(1f, BurstDuration).SetEase(Ease.OutBack));
                sequence.Insert(0f, rect.DOLocalMove(burst, BurstDuration).SetEase(Ease.OutCubic));
                // Sideways first, up later: each coin curves into the bar instead of a straight line.
                sequence.Insert(flyStart, rect.DOMoveX(target.position.x, FlyDuration).SetEase(Ease.OutSine));
                sequence.Insert(flyStart, rect.DOMoveY(target.position.y, FlyDuration).SetEase(Ease.InQuad));
                sequence.Insert(flyStart, rect.DOScale(endScale, FlyDuration).SetEase(Ease.InQuad));
                sequence.InsertCallback(flyStart + FlyDuration, () =>
                {
                    coinObject.SetActive(false);
                    landed++;
                    target.DOKill(true);
                    target.localScale = Vector3.one;
                    target.DOPunchScale(Vector3.one * 0.12f, 0.2f, 5, 0.45f).SetUpdate(true);
                    onCoinLanded?.Invoke(landed, count);
                });
            }

            sequence.AppendInterval(HoldAfterLanding);
            sequence.OnKill(() =>
            {
                if (root != null)
                    UnityEngine.Object.Destroy(root);
                onFinished?.Invoke();
            });
            return sequence;
        }
    }
}
