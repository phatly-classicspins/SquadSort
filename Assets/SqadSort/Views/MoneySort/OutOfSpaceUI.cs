using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class OutOfSpaceOffer
    {
        public int CoinPrice { get; }
        public int CurrentMoney { get; }
        public Action BuyWithCoins { get; }
        public Action WatchAd { get; }
        public Action Close { get; }
        public bool PlayIntro { get; }

        public OutOfSpaceOffer(int coinPrice, int currentMoney,
            Action buyWithCoins, Action watchAd, Action close, bool playIntro = true)
        {
            CoinPrice = coinPrice;
            CurrentMoney = currentMoney;
            BuyWithCoins = buyWithCoins;
            WatchAd = watchAd;
            Close = close;
            PlayIntro = playIntro;
        }
    }

    public sealed class OutOfSpaceUI : UIBase
    {
        private bool _moneySortInitialized;
        [SerializeField] private TextMeshProUGUI coinPriceText;
        [SerializeField] private Button coinButton;
        [SerializeField] private Button adButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private BoosterUnlockPreviewVideo previewVideo;
        [SerializeField] private RectTransform introBanner;
        [SerializeField] private RectTransform popupCard;
        [SerializeField] private CanvasGroup popupCanvasGroup;
        [SerializeField] private Image backdrop;
        [SerializeField, Min(0f)] private float bannerEnterDuration = 0.55f;
        [SerializeField, Min(0f)] private float bannerHoldDuration = 0.65f;
        [SerializeField, Min(0f)] private float bannerExitDuration = 0.3f;

        private OutOfSpaceOffer offer;
        private Sequence introSequence;
        private Vector2 bannerTargetPosition;
        private float backdropAlpha;
        private bool appearanceCached;

        private void Awake()
        {
            CacheAppearance();
        }

        private void CacheAppearance()
        {
            if (appearanceCached) return;
            bannerTargetPosition = introBanner.anchoredPosition;
            backdropAlpha = backdrop.color.a;
            appearanceCached = true;
        }

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            coinButton.onClick.AddListener(OnCoinClicked);
            adButton.onClick.AddListener(OnAdClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        public override void SetData(params object[] data)
        {
            offer = data.Length > 0 ? data[0] as OutOfSpaceOffer : null;
            if (offer == null) return;

            coinPriceText.text = offer.CoinPrice.ToString("N0");
            // Short on coins the button stays tappable: it opens the Shop (GDD 6.2).
            coinButton.interactable = offer.BuyWithCoins != null;
            RewardedAdButton.Bind(adButton, AdsPlacement.OutOfSpace, offer.WatchAd != null);
            // CardSlot shows this offer modeless (no framework dim, so the banner drops over the bright board): the source's own
            // full-screen backdrop is the dim and the input gate, whatever an instance override left it as
            backdrop.enabled = true;
            backdrop.raycastTarget = true;
            if (offer.PlayIntro)
            {
                // the dialog is on screen before Show() runs the intro: start dark-free, card hidden, so nothing flashes first
                CacheAppearance();
                SetBackdropAlpha(0f);
                popupCanvasGroup.alpha = 0f;
                popupCard.gameObject.SetActive(false);
            }
        }

        public override void Show()
        {
            CacheAppearance();
            base.Show();
            introSequence?.Kill();
            introSequence = null;
            if (offer?.PlayIntro != true)
            {
                ShowPopupImmediately();
                return;
            }

            popupCard.gameObject.SetActive(false);
            introBanner.gameObject.SetActive(true);
            popupCanvasGroup.alpha = 0f;
            SetBackdropAlpha(0f);

            var parent = (RectTransform)introBanner.parent;
            var offscreenY = parent.rect.height * 0.5f + introBanner.rect.height + 40f;
            introBanner.anchoredPosition = new Vector2(bannerTargetPosition.x, offscreenY);

            introSequence = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(
                    () => introBanner.anchoredPosition,
                    position => introBanner.anchoredPosition = position,
                    bannerTargetPosition,
                    bannerEnterDuration).SetEase(Ease.OutBack))
                .AppendInterval(bannerHoldDuration)
                .Append(DOTween.To(
                    () => introBanner.anchoredPosition.y,
                    y =>
                    {
                        var position = introBanner.anchoredPosition;
                        position.y = y;
                        introBanner.anchoredPosition = position;
                    },
                    offscreenY,
                    bannerExitDuration).SetEase(Ease.InBack))
                .AppendCallback(() =>
                {
                    introBanner.gameObject.SetActive(false);
                    popupCard.gameObject.SetActive(true);
                    popupCard.localScale = Vector3.one * 0.88f;
                    previewVideo?.Play(MoneySortBoosterType.Shuffle);
                })
                .Append(DOTween.To(() => backdrop.color.a, SetBackdropAlpha, backdropAlpha, 0.25f))
                .Join(DOTween.To(
                    () => popupCanvasGroup.alpha,
                    alpha => popupCanvasGroup.alpha = alpha,
                    1f,
                    0.25f))
                .Join(popupCard.DOScale(1f, 0.3f).SetEase(Ease.OutBack))
                .OnComplete(() => introSequence = null);
        }

        public override void Hide()
        {
            StopIntro();
            previewVideo?.Stop();
            offer = null;
            base.Hide();
        }

        private void ShowPopupImmediately()
        {
            introBanner.gameObject.SetActive(false);
            introBanner.anchoredPosition = bannerTargetPosition;
            popupCard.gameObject.SetActive(true);
            popupCard.localScale = Vector3.one;
            popupCanvasGroup.alpha = 1f;
            SetBackdropAlpha(backdropAlpha);
            previewVideo?.Play(MoneySortBoosterType.Shuffle);
        }

        private void StopIntro()
        {
            introSequence?.Kill();
            introSequence = null;
        }

        private void SetBackdropAlpha(float alpha)
        {
            var color = backdrop.color;
            color.a = alpha;
            backdrop.color = color;
        }

        private void OnDisable()
        {
            StopIntro();
            previewVideo?.Stop();
        }

        /// <summary>Whether a rewarded ad is loaded for the Free button: not loaded ⇒ grey "No ads available"; loaded ⇒ the
        /// button as authored. The caller re-reads the fill while the popup is up (QA RV-03).</summary>
        public void SetAdAvailable(bool available)
        {
            if (adButton != null && adButton.TryGetComponent(out RewardedAdButton rewarded)) rewarded.SetAvailability(available);
        }

        private void OnCoinClicked() => offer?.BuyWithCoins?.Invoke();
        private void OnAdClicked() => offer?.WatchAd?.Invoke();
        private void OnCloseClicked() => offer?.Close?.Invoke();

        private void OnDestroy()
        {
            StopIntro();
            if (coinButton != null) coinButton.onClick.RemoveListener(OnCoinClicked);
            if (adButton != null) adButton.onClick.RemoveListener(OnAdClicked);
            if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
        }
    }
}
