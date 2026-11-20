using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class BoosterPurchaseOffer
    {
        public MoneySortBoosterType Type { get; }
        public string Title { get; }
        public string Description { get; }
        public int CoinPrice { get; }
        public int RewardAmount { get; }
        public int AdRewardAmount { get; }
        public int CurrentMoney { get; }
        public Sprite Icon { get; }
        public Action ConfirmCoin { get; }
        public Action ConfirmAd { get; }
        public Action Close { get; }
        public string AdPlacement { get; }

        public BoosterPurchaseOffer(
            MoneySortBoosterType type,
            string title,
            string description,
            int coinPrice,
            int rewardAmount,
            int currentMoney,
            Sprite icon,
            Action confirmCoin,
            Action confirmAd,
            Action close,
            int adRewardAmount = 1,
            string adPlacement = null)
        {
            AdPlacement = adPlacement;
            Type = type;
            Title = title;
            Description = description;
            CoinPrice = coinPrice;
            RewardAmount = rewardAmount;
            AdRewardAmount = Mathf.Max(1, adRewardAmount);
            CurrentMoney = currentMoney;
            Icon = icon;
            ConfirmCoin = confirmCoin;
            ConfirmAd = confirmAd;
            Close = close;
        }
    }

    public sealed class BoosterPurchaseUI : UIBase
    {
        [SerializeField] private BoosterConfigSO _icons;
        public Sprite Icon(MoneySortBoosterType type)=>_icons != null ? _icons.GetIcon(type) : _previewVideo?.Icon(type)??_boosterIcon.sprite;
        private bool _moneySortInitialized;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private Image _boosterIcon;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _coinPriceText;
        [SerializeField] private TextMeshProUGUI _coinRewardText;
        [SerializeField] private TextMeshProUGUI _adRewardText;
        [SerializeField] private TextMeshProUGUI _messageText;
        [SerializeField] private Button _coinButton;
        [SerializeField] private Button _adButton;
        [SerializeField] private Sprite _noAdsButtonSprite;
        private Sprite _availableAdSprite;
        [SerializeField] private Image _coinBoosterIcon;
        [SerializeField] private Image _adBoosterIcon;
        [SerializeField] private Button _closeButton;
        [SerializeField] private BoosterUnlockPreviewVideo _previewVideo;

        private BoosterPurchaseOffer _offer;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            _availableAdSprite = _adButton.image.sprite;
            _coinButton.onClick.AddListener(OnCoinClicked);
            _adButton.onClick.AddListener(OnAdClicked);
            _closeButton.onClick.AddListener(OnCloseClicked);
        }

        public override void SetData(params object[] data)
        {
            _offer = data.Length > 0 ? data[0] as BoosterPurchaseOffer : null;
            if (_offer == null)
                return;

            _titleText.text = _offer.Title;
            _descriptionText.text = _offer.Description;
            _coinPriceText.text = _offer.CoinPrice.ToString("N0");
            _coinRewardText.text = $"X{_offer.RewardAmount}";
            _adRewardText.text = $"X{_offer.AdRewardAmount}";
            _boosterIcon.sprite = _offer.Icon;
            _boosterIcon.enabled = _offer.Icon != null;
            SetIcon(_coinBoosterIcon, _offer.Icon);
            SetIcon(_adBoosterIcon, _offer.Icon);
            // Short on coins the button stays tappable: it opens the Shop (GDD 6.2).
            _coinButton.interactable = true;
            if (_noAdsButtonSprite != null)
                _adButton.image.sprite = _offer.ConfirmAd != null ? _availableAdSprite : _noAdsButtonSprite;
            RewardedAdButton.Bind(_adButton, _offer.AdPlacement, _offer.ConfirmAd != null);
            _messageText.text = _offer.CurrentMoney >= _offer.CoinPrice
                ? string.Empty
                : $"NEED {_offer.CoinPrice - _offer.CurrentMoney:N0} MORE COINS";
        }

        private static void SetIcon(Image image, Sprite sprite)
        {
            if (image == null)
                return;
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
        }

        public override void Show()
        {
            base.Show();
            if (_offer != null && _previewVideo != null && _previewVideo.gameObject.activeInHierarchy)
                _previewVideo.Play(_offer.Type);
        }

        public override void Hide()
        {
            _previewVideo?.Stop();
            _offer = null;
            base.Hide();
        }

        private void OnCoinClicked() => _offer?.ConfirmCoin?.Invoke();
        private void OnAdClicked() => _offer?.ConfirmAd?.Invoke();
        private void OnCloseClicked() => _offer?.Close?.Invoke();

        private void OnDestroy()
        {
            if (_coinButton != null) _coinButton.onClick.RemoveListener(OnCoinClicked);
            if (_adButton != null) _adButton.onClick.RemoveListener(OnAdClicked);
            if (_closeButton != null) _closeButton.onClick.RemoveListener(OnCloseClicked);
        }
    }
}
