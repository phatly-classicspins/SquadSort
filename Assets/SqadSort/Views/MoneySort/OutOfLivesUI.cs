using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class OutOfLivesPresentation
    {
        public readonly int CoinPrice;
        public readonly int CoinLives;
        public readonly int AdLives;
        public readonly Action BuyLives;
        public readonly Action WatchAd;
        public readonly Action Close;

        public OutOfLivesPresentation(
            int coinPrice,
            int coinLives,
            int adLives,
            Action buyLives,
            Action watchAd,
            Action close)
        {
            CoinPrice = Mathf.Max(0, coinPrice);
            CoinLives = Mathf.Max(1, coinLives);
            AdLives = Mathf.Max(1, adLives);
            BuyLives = buyLives;
            WatchAd = watchAd;
            Close = close;
        }
    }

    public sealed class OutOfLivesUI : UIBase
    {
        private bool _moneySortInitialized;
        [SerializeField] private Button _buyButton;
        [SerializeField] private Button _freeLifeButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _coinPriceText;
        [SerializeField] private TMP_Text _coinLivesText;
        [SerializeField] private TMP_Text _adLivesText;
        private OutOfLivesPresentation _presentation;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            _buyButton.onClick.AddListener(OnBuy);
            _freeLifeButton.onClick.AddListener(OnWatchAd);
            _closeButton.onClick.AddListener(OnClose);
        }

        public override void SetData(params object[] data)
        {
            _presentation = data.Length > 0 ? data[0] as OutOfLivesPresentation : null;
            if (_presentation == null)
                return;
            if (_coinPriceText != null)
                _coinPriceText.text = _presentation.CoinPrice.ToString("N0");
            if (_coinLivesText != null)
                _coinLivesText.text = $"x{_presentation.CoinLives}";
            if (_adLivesText != null)
                _adLivesText.text = $"x{_presentation.AdLives}";
            RewardedAdButton.Bind(_freeLifeButton, AdsPlacement.OutOfLivesRv, _presentation.WatchAd != null);
        }

        private void OnBuy() => _presentation?.BuyLives?.Invoke();
        private void OnWatchAd() => _presentation?.WatchAd?.Invoke();
        private void OnClose() => _presentation?.Close?.Invoke();

        private void OnDestroy()
        {
            _buyButton?.onClick.RemoveListener(OnBuy);
            _freeLifeButton?.onClick.RemoveListener(OnWatchAd);
            _closeButton?.onClick.RemoveListener(OnClose);
        }
    }
}
