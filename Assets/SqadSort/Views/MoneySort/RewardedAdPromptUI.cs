using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class RewardedAdPromptOffer
    {
        public Action WatchAd { get; }
        public Action Decline { get; }
        // Optional: a countdown that closes the popup (as a decline) when it reaches zero.
        public Func<int> RemainingSeconds { get; }
        // Optional: one label per reward slot of the popup, e.g. "x1" and "200".
        public string[] RewardLabels { get; }
        public string AdPlacement { get; }
        // Optional: run instead of Decline when the countdown closes the popup (no button was tapped).
        public Action Expire { get; }

        public RewardedAdPromptOffer(Action watchAd, Action decline,
            Func<int> remainingSeconds = null, string[] rewardLabels = null, string adPlacement = null,
            Action expire = null)
        {
            AdPlacement = adPlacement;
            Expire = expire;
            WatchAd = watchAd;
            Decline = decline;
            RemainingSeconds = remainingSeconds;
            RewardLabels = rewardLabels;
        }
    }

    // Rewarded ad offers ("Skip Level?", "Need a Help?", the Garage Gift): the ad button takes
    // the offer, while "No Thanks" and the close button both decline it.
    public sealed class RewardedAdPromptUI : UIBase
    {
        private bool _moneySortInitialized;
        [SerializeField] private Button _watchAdButton;
        [SerializeField] private Button _noThanksButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TextMeshProUGUI _timerText;
        [SerializeField] private TextMeshProUGUI[] _rewardTexts;
        // The ad badge sits beside the watch button, so it is hidden with the button's content.
        [SerializeField] private GameObject _adIcon;

        private RewardedAdPromptOffer _offer;
        private float _nextTimerRefreshAt;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            if (_watchAdButton != null)
                _watchAdButton.onClick.AddListener(OnWatchAd);
            if (_noThanksButton != null)
                _noThanksButton.onClick.AddListener(OnDecline);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnDecline);
        }

        public override void SetData(params object[] data)
        {
            _offer = data.Length > 0 ? data[0] as RewardedAdPromptOffer : null;
            if (_rewardTexts != null && _offer?.RewardLabels != null)
            {
                for (var i = 0; i < _rewardTexts.Length && i < _offer.RewardLabels.Length; i++)
                {
                    if (_rewardTexts[i] != null)
                        _rewardTexts[i].text = _offer.RewardLabels[i];
                }
            }
            if (_offer != null)
                RewardedAdButton.Bind(_watchAdButton, _offer.AdPlacement, _offer.WatchAd != null, _adIcon);
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
            if (_offer?.RemainingSeconds == null)
                return;
            var remaining = Mathf.Max(0, _offer.RemainingSeconds());
            if (_timerText != null)
                _timerText.text = $"{remaining / 60}:{remaining % 60:00}";
            if (remaining > 0)
                return;
            // Cleared first so the hide animation's frames do not decline again.
            var decline = _offer.Expire ?? _offer.Decline;
            _offer = null;
            decline?.Invoke();
        }

        private void OnWatchAd() => _offer?.WatchAd?.Invoke();
        private void OnDecline() => _offer?.Decline?.Invoke();

        private void OnDestroy()
        {
            if (_watchAdButton != null)
                _watchAdButton.onClick.RemoveListener(OnWatchAd);
            if (_noThanksButton != null)
                _noThanksButton.onClick.RemoveListener(OnDecline);
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnDecline);
        }
    }
}
