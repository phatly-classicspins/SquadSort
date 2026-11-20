using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class HomePresentation
    {
        public readonly int Level;
        public readonly int Money;
        public readonly int Lives;
        public readonly int MaximumLives;
        public readonly int LifeRefillSeconds;
        public readonly Action Play;
        public readonly Action Settings;
        public readonly Action OpenShop;
        public readonly Action CloseShop;
        public readonly Action OpenLockedTab;
        public readonly int InfiniteLivesSeconds;
        public readonly Action OpenStarterPack;
        public readonly Action OpenRemoveAds;
        public readonly Action OpenGarageGift;
        // The coin bar; falls back to OpenShop.
        public readonly Action OpenShopFromCoins;

        public HomePresentation(
            int level,
            int money,
            int lives,
            int maximumLives,
            int lifeRefillSeconds,
            Action play,
            Action settings,
            Action openShop = null,
            Action closeShop = null,
            Action openLockedTab = null,
            int infiniteLivesSeconds = 0,
            Action openStarterPack = null,
            Action openRemoveAds = null,
            Action openGarageGift = null,
            Action openShopFromCoins = null)
        {
            OpenShopFromCoins = openShopFromCoins;
            OpenStarterPack = openStarterPack;
            OpenRemoveAds = openRemoveAds;
            OpenGarageGift = openGarageGift;
            Level = level;
            Money = money;
            Lives = lives;
            MaximumLives = maximumLives;
            LifeRefillSeconds = lifeRefillSeconds;
            Play = play;
            Settings = settings;
            OpenShop = openShop;
            CloseShop = closeShop;
            OpenLockedTab = openLockedTab;
            InfiniteLivesSeconds = infiniteLivesSeconds;
        }
    }

    public sealed class HomeUI : UIBase
    {
        private bool _moneySortInitialized;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _moneyText;
        [SerializeField] private TextMeshProUGUI _livesText;
        [SerializeField] private TextMeshProUGUI _lifeTimerText;
        [SerializeField] private ResourceGroupUI _resourceGroup;
        [SerializeField] private Button _playButton;
        [SerializeField] private Sprite _hardPlaySprite;

        public void SetHardPlay(bool hard)
        {
            if (_playButton.targetGraphic is Image image)
                image.overrideSprite = hard ? _hardPlaySprite : null;
        }
        [SerializeField] private Button _settingsButton;
        [Tooltip("Coin bar with the \"+\" badge: opens the Shop tab.")]
        [SerializeField] private Button _moneyButton;
        [Tooltip("Lives bar, drawn above the out of lives popup.")]
        [SerializeField] private GameObject _livesHud;
        [SerializeField] private HomeLevelNodeUI[] _mapLevelNodes;
        [SerializeField] private HomeFooterUI _footer;
        [Tooltip("Shown only on the Main tab; the shop page would otherwise show the map through its gaps.")]
        [SerializeField] private GameObject[] _mainTabContent;
        [Header("Offer Icons")]
        [SerializeField] private Button _starterPackButton;
        [SerializeField] private TextMeshProUGUI _starterPackTimerText;
        [SerializeField] private Button _removeAdsButton;
        [Tooltip("Garage Gift badge, shown while a dismissed gift can still be claimed.")]
        [SerializeField] private Button _garageGiftButton;
        [SerializeField] private TextMeshProUGUI _garageGiftTimerText;
        private HomePresentation _presentation;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            _playButton.onClick.AddListener(OnPlay);
            _settingsButton.onClick.AddListener(OnSettings);
            _moneyButton?.onClick.AddListener(OnMoney);
            if (_starterPackButton != null)
            {
                _starterPackButton.onClick.AddListener(OnStarterPack);
                _starterPackButton.gameObject.SetActive(false);
            }
            if (_removeAdsButton != null)
            {
                _removeAdsButton.onClick.AddListener(OnRemoveAds);
                _removeAdsButton.gameObject.SetActive(false);
            }
            if (_garageGiftButton != null)
            {
                _garageGiftButton.onClick.AddListener(OnGarageGift);
                _garageGiftButton.gameObject.SetActive(false);
            }
            if (_footer != null)
            {
                _footer.Init(OnFooterTab);
                // Reserved for a feature that is not designed yet.
                _footer.SetLocked(MoneySortHomeTab.Locked, true);
                _footer.SetSelected(MoneySortHomeTab.Main);
            }
        }

        public override void SetData(params object[] data)
        {
            _presentation = data.Length > 0 ? data[0] as HomePresentation : null;
            if (_presentation == null) return;
            var currentLevel = Mathf.Max(1, _presentation.Level);
            _levelText.text = $"Level {currentLevel}";
            if (_mapLevelNodes != null)
            {
                for (var i = 0; i < _mapLevelNodes.Length; i++)
                {
                    if (_mapLevelNodes[i] != null)
                        _mapLevelNodes[i].SetData(
                            currentLevel + i,
                            i == 0 ? HomeLevelNodeState.Current : HomeLevelNodeState.Upcoming);
                }
            }
            if (_resourceGroup != null)
                _resourceGroup.SetMoney(_presentation.Money);
            else
                _moneyText.text = _presentation.Money.ToString("N0");
            SetLives(_presentation.Lives, _presentation.MaximumLives, _presentation.LifeRefillSeconds,
                _presentation.InfiniteLivesSeconds);
        }

        public void SetLives(int lives, int maximumLives, int refillSeconds, int infiniteSeconds = 0)
        {
            if (_resourceGroup != null)
            {
                _resourceGroup.SetLives(lives, maximumLives, refillSeconds, infiniteSeconds);
                return;
            }
            if (_livesText == null)
                return;
            var remaining = Mathf.Max(0, refillSeconds);
            _livesText.text = Mathf.Max(0, lives).ToString();
            if (_lifeTimerText != null)
                _lifeTimerText.text = infiniteSeconds > 0
                    ? ResourceGroupUI.FormatInfiniteTimer(infiniteSeconds)
                    : lives >= maximumLives
                        ? "Max"
                        : $"{remaining / 60:00}:{remaining % 60:00}";
        }

        private void OnPlay() => _presentation?.Play?.Invoke();
        private void OnSettings() => _presentation?.Settings?.Invoke();
        private void OnMoney() => (_presentation?.OpenShopFromCoins ?? _presentation?.OpenShop)?.Invoke();

        // Draws the coin bar above a popup's dark backdrop (out of lives) so it can still open the Shop.
        public void SetMoneyAbovePopups(bool above)
        {
            if (_moneyButton != null)
                CanvasLift.Set(_moneyButton.gameObject, above);
        }

        // Draws the lives bar above the out of lives popup next to the coin bar.
        public void SetLivesAbovePopups(bool above) => CanvasLift.Set(_livesHud, above);
        private void OnStarterPack() => _presentation?.OpenStarterPack?.Invoke();
        private void OnRemoveAds() => _presentation?.OpenRemoveAds?.Invoke();
        private void OnGarageGift() => _presentation?.OpenGarageGift?.Invoke();

        // Zero seconds hides the badge (no gift waiting to be claimed).
        public void SetGarageGift(int remainingSeconds)
        {
            if (_garageGiftButton != null)
                _garageGiftButton.gameObject.SetActive(remainingSeconds > 0);
            if (_garageGiftTimerText != null && remainingSeconds > 0)
                _garageGiftTimerText.text = $"{remainingSeconds / 60}:{remainingSeconds % 60:00}";
        }

        // Zero starter seconds hides the Starter Pack icon (not unlocked, bought or expired).
        public void SetOfferIcons(int starterPackSeconds, bool removeAdsVisible)
        {
            if (_starterPackButton != null)
                _starterPackButton.gameObject.SetActive(starterPackSeconds > 0);
            if (_starterPackTimerText != null && starterPackSeconds > 0)
                _starterPackTimerText.text = IapOfferPopupUI.FormatClock(starterPackSeconds);
            if (_removeAdsButton != null)
                _removeAdsButton.gameObject.SetActive(removeAdsVisible);
        }

        public void SetMoney(int amount)
        {
            if (_resourceGroup != null)
                _resourceGroup.SetMoney(amount);
            else if (_moneyText != null)
                _moneyText.text = Mathf.Max(0, amount).ToString("N0");
        }

        public void SetFooterVisible(bool visible) { if (_footer != null) _footer.gameObject.SetActive(visible); }

        /// <summary>A footer tab's page (the Shop): drawn over Home's content but under Home's footer, so one footer serves both
        /// tabs and never moves.</summary>
        public void MountPage(Transform page)
        {
            page.SetParent(transform, false);
            page.SetSiblingIndex(_footer != null ? _footer.transform.GetSiblingIndex() : transform.childCount - 1);
            _mountedPage = page;
        }

        // A mounted page (the Shop tab) brings its own resource bar; Home's bar under it showed through beside it on tall
        // screens (Chipsaway feedback 11-11, "Bug header UI shop"), so it is hidden while the page is up.
        private Transform _mountedPage;
        private void LateUpdate()
        {
            if (_resourceGroup == null) return;
            bool pageUp = _mountedPage != null && _mountedPage.gameObject.activeInHierarchy;
            if (_resourceGroup.gameObject.activeSelf == pageUp) _resourceGroup.gameObject.SetActive(!pageUp);
        }

        /// <summary>Lights a footer tab without touching Home's content (the page switch shows or hides that).</summary>
        public void SetFooterTab(MoneySortHomeTab tab) => _footer?.SetSelected(tab);
        public void SetFooterLabels(string shop, string home) => _footer?.SetLabels(shop, home);

        public void SetSelectedTab(MoneySortHomeTab tab)
        {
            if (_footer != null)
                _footer.SetSelected(tab);
            if (_mainTabContent == null)
                return;
            foreach (var content in _mainTabContent)
            {
                if (content != null)
                    content.SetActive(tab == MoneySortHomeTab.Main);
            }
        }

        private void OnFooterTab(MoneySortHomeTab tab)
        {
            switch (tab)
            {
                case MoneySortHomeTab.Shop:
                    _presentation?.OpenShop?.Invoke();
                    break;
                case MoneySortHomeTab.Main:
                    _presentation?.CloseShop?.Invoke();
                    break;
                case MoneySortHomeTab.Locked:
                    _presentation?.OpenLockedTab?.Invoke();
                    break;
            }
        }

        private void OnDestroy()
        {
            _playButton?.onClick.RemoveListener(OnPlay);
            _settingsButton?.onClick.RemoveListener(OnSettings);
            _moneyButton?.onClick.RemoveListener(OnMoney);
            _starterPackButton?.onClick.RemoveListener(OnStarterPack);
            _removeAdsButton?.onClick.RemoveListener(OnRemoveAds);
            _garageGiftButton?.onClick.RemoveListener(OnGarageGift);
        }
    }

}
