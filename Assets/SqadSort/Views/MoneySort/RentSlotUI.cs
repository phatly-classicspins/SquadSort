using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class RentSlotOffer
    {
        public float DurationSeconds { get; }
        public int CoinPrice { get; }
        public int CurrentMoney { get; }
        public int FreeUsesRemaining { get; }
        public Action ConfirmFree { get; }
        public Action ConfirmCoin { get; }
        public Action Close { get; }

        public RentSlotOffer(
            float durationSeconds,
            int coinPrice,
            int currentMoney,
            int freeUsesRemaining,
            Action confirmFree,
            Action confirmCoin,
            Action close)
        {
            DurationSeconds = durationSeconds;
            CoinPrice = coinPrice;
            CurrentMoney = currentMoney;
            FreeUsesRemaining = freeUsesRemaining;
            ConfirmFree = confirmFree;
            ConfirmCoin = confirmCoin;
            Close = close;
        }
    }

    public sealed class RentSlotUI : UIBase
    {
        private bool _moneySortInitialized;
        [SerializeField] private TextMeshProUGUI _durationText;
        [SerializeField] private TextMeshProUGUI _coinPriceText;
        [SerializeField] private TextMeshProUGUI _freeText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _coinButton;
        [SerializeField] private Button _freeButton;
        [SerializeField] private Button _closeButton;

        private RentSlotOffer _offer;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            _coinButton.onClick.AddListener(OnCoinClicked);
            _freeButton.onClick.AddListener(OnFreeClicked);
            _closeButton.onClick.AddListener(OnCloseClicked);
        }

        public override void SetData(params object[] data)
        {
            _offer = data.Length > 0 ? data[0] as RentSlotOffer : null;
            if (_offer == null)
                return;

            _durationText.text = $"{Mathf.RoundToInt(_offer.DurationSeconds)} Sec";
            _coinPriceText.text = _offer.CoinPrice.ToString("N0");
            _freeText.text = "Free";
            _freeButton.gameObject.SetActive(true);
            RewardedAdButton.Bind(_freeButton, AdsPlacement.RentSlot, _offer.ConfirmFree != null);
            _coinButton.gameObject.SetActive(true);
            _coinButton.interactable =
                _offer.CurrentMoney >= _offer.CoinPrice;
            _messageText.text = _coinButton.interactable
                ? "Get Rent Slots"
                : $"Need {_offer.CoinPrice - _offer.CurrentMoney:N0} more coins";
        }

        public override void Hide()
        {
            _offer = null;
            base.Hide();
        }

        private void OnCoinClicked() => _offer?.ConfirmCoin?.Invoke();
        private void OnFreeClicked() => _offer?.ConfirmFree?.Invoke();
        private void OnCloseClicked() => _offer?.Close?.Invoke();

        private void OnDestroy()
        {
            if (_coinButton != null) _coinButton.onClick.RemoveListener(OnCoinClicked);
            if (_freeButton != null) _freeButton.onClick.RemoveListener(OnFreeClicked);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(OnCloseClicked);
        }
    }
}
