using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    // Starter Pack and Remove Ads popups. Same card as the No Internet popup:
    // header title, icon, reward text, an optional countdown and one green buy button.
    public sealed class IapOfferPopupUI : UIBase
    {
        private bool _moneySortInitialized;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _descriptionText;
        [SerializeField] private TextMeshProUGUI _timerText;
        [Tooltip("Shows the countdown as hh:mm:ss instead of \"71h 59m\".")]
        [SerializeField] private bool _clockTimer;
        [Tooltip("Optional per-reward labels (coins, boosters, lives) bound from the offer.")]
        [SerializeField] private ShopOfferItemUI _rewards;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Button _buyButton;
        [SerializeField] private TextMeshProUGUI _priceText;
        [SerializeField] private CanvasGroup _buyGroup;
        [SerializeField] private Button _closeButton;

        private IapOfferPopupPresentation _presentation;
        private Sprite _defaultIcon;
        private float _nextTimerRefreshAt;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            if (_iconImage != null)
                _defaultIcon = _iconImage.sprite;
            if (_buyButton != null)
                _buyButton.onClick.AddListener(OnBuy);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnClose);
        }

        public override void SetData(params object[] data)
        {
            _presentation = data.Length > 0 ? data[0] as IapOfferPopupPresentation : null;
            var offer = _presentation?.Offer;
            if (offer == null)
                return;
            if (_titleText != null)
                _titleText.text = _presentation.Title;
            if (_descriptionText != null)
                _descriptionText.text = _presentation.Description;
            _rewards?.Bind(offer);
            if (_iconImage != null)
                _iconImage.sprite = offer.Icon != null ? offer.Icon : _defaultIcon;
            if (_priceText != null)
                _priceText.text = offer.Price;
            if (_buyButton != null)
                _buyButton.interactable = offer.Purchasable;
            if (_buyGroup != null)
                _buyGroup.alpha = offer.Purchasable ? 1f : 0.5f;
            if (_timerText != null)
                _timerText.gameObject.SetActive(_presentation.RemainingSeconds != null);
            RefreshTimer();
        }

        private void Update()
        {
            // Unscaled: the popup can appear while gameplay time is paused.
            if (Time.unscaledTime < _nextTimerRefreshAt)
                return;
            RefreshTimer();
        }

        private void RefreshTimer()
        {
            _nextTimerRefreshAt = Time.unscaledTime + 1f;
            if (_presentation?.RemainingSeconds == null)
                return;
            var remaining = _presentation.RemainingSeconds();
            if (_timerText != null)
                _timerText.text = _clockTimer ? FormatClock(remaining) : FormatRemaining(remaining);
            if (remaining <= 0)
                OnClose();
        }

        public static string FormatRemaining(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            var hours = seconds / 3600;
            return hours > 0
                ? $"{hours}h {seconds / 60 % 60:00}m"
                : $"{seconds / 60:00}:{seconds % 60:00}";
        }

        public static string FormatClock(int seconds)
        {
            seconds = Mathf.Max(0, seconds);
            return $"{seconds / 3600:00}:{seconds / 60 % 60:00}:{seconds % 60:00}";
        }

        private void OnBuy()
        {
            var offer = _presentation?.Offer;
            if (offer != null && offer.Purchasable)
                offer.Buy?.Invoke();
        }

        private void OnClose() => _presentation?.Close?.Invoke();

        private void OnDestroy()
        {
            if (_buyButton != null)
                _buyButton.onClick.RemoveListener(OnBuy);
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnClose);
        }
    }
}
