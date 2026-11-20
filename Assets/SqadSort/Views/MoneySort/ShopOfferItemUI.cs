using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    // One purchasable card. Every reference is optional so the art pass can drop
    // the summary text and wire the per-reward labels and badges instead.
    public sealed class ShopOfferItemUI : MonoBehaviour
    {
        public void BindCardSlotLabels(ShopCardVisual card){SetText(_titleText,card.Title);SetText(_priceText,card.Price);SetText(_coinsText,card.Coins);SetText(_infiniteLivesText,card.Lives);if(card.Booster!=null){SetText(_shuffleText,card.Booster);SetText(_hammerText,card.Booster);SetText(_freeMoveText,card.Booster);}if(card.Timer!=null)SetText(_timerText,card.Timer);}
        [Tooltip("ShopConfigSO product id this card sells.")]
        [SerializeField] private string _productId;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _priceText;
        [Tooltip("Plain-text list of every reward; used by the placeholder layout.")]
        [SerializeField] private TextMeshProUGUI _summaryText;
        [SerializeField] private Image _icon;
        [SerializeField] private Button _buyButton;

        [Header("Per-reward labels (optional)")]
        [SerializeField] private TextMeshProUGUI _coinsText;
        [Tooltip("{0} is replaced by the formatted coin amount, e.g. \"Plus {0} coins\".")]
        [SerializeField] private string _coinsFormat = "{0}";
        [SerializeField] private TextMeshProUGUI _shuffleText;
        [SerializeField] private TextMeshProUGUI _hammerText;
        [SerializeField] private TextMeshProUGUI _freeMoveText;
        [SerializeField] private TextMeshProUGUI _infiniteLivesText;
        [Tooltip("{0} is replaced by the booster count.")]
        [SerializeField] private string _boosterFormat = "x{0}";
        [SerializeField] private GameObject _removeAdsBadge;
        [SerializeField] private GameObject _purchasedBadge;
        [Tooltip("Countdown for time-limited offers (hh:mm:ss); the card hides itself when it ends.")]
        [SerializeField] private TextMeshProUGUI _timerText;

        private ShopOfferView _view;
        private int _shownSeconds = -1;

        public string ProductId => _productId;

        private void Awake()
        {
            if (_buyButton != null)
                _buyButton.onClick.AddListener(OnBuy);
        }

        public void Bind(ShopOfferView view)
        {
            if(_buyButton!=null){_buyButton.onClick.RemoveListener(OnBuy);_buyButton.onClick.AddListener(OnBuy);}
            _view = view;
            if (view == null)
                return;
            var reward = view.Reward;
            SetText(_titleText, view.Title);
            SetText(_priceText, view.Price);
            SetText(_summaryText, BuildSummary(view));
            // Keeps the art placed in the prefab unless the config overrides it.
            if (_icon != null && view.Icon != null)
                _icon.sprite = view.Icon;
            SetCount(_coinsText, reward.coins, string.Format(
                string.IsNullOrEmpty(_coinsFormat) ? "{0}" : _coinsFormat, reward.coins.ToString("N0")));
            var boosterFormat = string.IsNullOrEmpty(_boosterFormat) ? "{0}" : _boosterFormat;
            SetCount(_shuffleText, reward.shuffle, string.Format(boosterFormat, reward.shuffle));
            SetCount(_hammerText, reward.hammer, string.Format(boosterFormat, reward.hammer));
            SetCount(_freeMoveText, reward.freeMove, string.Format(boosterFormat, reward.freeMove));
            SetCount(_infiniteLivesText, reward.infiniteLivesMinutes, FormatDuration(reward.infiniteLivesMinutes));
            if (_removeAdsBadge != null)
                _removeAdsBadge.SetActive(reward.removeAds);
            if (_purchasedBadge != null)
                _purchasedBadge.SetActive(!view.Purchasable);
            if (_buyButton != null)
                _buyButton.interactable = view.Purchasable;
            _shownSeconds = -1;
            UpdateTimer();
        }

        private void Update() => UpdateTimer();

        private void UpdateTimer()
        {
            // Cards without their own countdown (e.g. inside an offer popup) leave expiry to the owner.
            if (_view?.RemainingSeconds == null || _timerText == null)
                return;
            var seconds = Mathf.Max(0, _view.RemainingSeconds());
            if (seconds == _shownSeconds)
                return;
            _shownSeconds = seconds;
            _timerText.text = IapOfferPopupUI.FormatClock(seconds);
            if (seconds <= 0)
                gameObject.SetActive(false);
        }

        public static string BuildSummary(ShopOfferView view)
        {
            var reward = view.Reward;
            var builder = new StringBuilder();
            void Append(string part)
            {
                if (builder.Length > 0)
                    builder.Append("  •  ");
                builder.Append(part);
            }
            if (reward.coins > 0) Append($"{reward.coins:N0} coins");
            if (reward.shuffle > 0) Append($"Shuffle x{reward.shuffle}");
            if (reward.hammer > 0) Append($"Hammer x{reward.hammer}");
            if (reward.freeMove > 0) Append($"Free Move x{reward.freeMove}");
            if (reward.infiniteLivesMinutes > 0) Append($"Infinite lives {FormatDuration(reward.infiniteLivesMinutes)}");
            if (reward.removeAds) Append("No Ads + play offline");
            if (!view.Purchasable) Append("Purchased");
            return builder.ToString();
        }

        public static string FormatDuration(int minutes) =>
            minutes >= 60 && minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes}m";

        private void OnBuy()
        {
            if (_view != null && _view.Purchasable)
                _view.Buy?.Invoke();
        }

        private static void SetText(TextMeshProUGUI label, string value)
        {
            if (label != null)
                label.text = value ?? string.Empty;
        }

        private static void SetCount(TextMeshProUGUI label, int amount, string value)
        {
            if (label == null)
                return;
            label.gameObject.SetActive(amount > 0);
            label.text = value;
        }

        private void OnDestroy()
        {
            if (_buyButton != null)
                _buyButton.onClick.RemoveListener(OnBuy);
        }
    }
}
