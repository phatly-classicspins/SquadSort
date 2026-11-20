using System;
using TMPro;
using UnityEngine;

namespace Game.Views
{
    /// <summary>
    /// Home (mock-ups <c>docs/mockups/home-v01.png</c>, <c>home-coins-v02.png</c> — CR-012 C1 coin pill): the live-text logo (the commercial name is not
    /// final, D-012), a fan of the six card colours and the Play button. Renders given labels; relays Play.
    /// </summary>
    public sealed class HomeView : MonoBehaviour
    {
        [SerializeField] private HomeUI _moneySortHome;
        private HomeUI SourceHome {get{_moneySortHome.Init(null);return _moneySortHome;}}
        [SerializeField] private Sprite[] _cardFace = new Sprite[8];
        [SerializeField] private Sprite _ground;
        [SerializeField] private Sprite _buttonPrimary;
        [SerializeField] private Sprite _pill, _coin;
        [SerializeField] private Sprite _heart;   // CR-012 E
        [SerializeField] private Sprite _footerBar, _footerSelected, _tabShop, _tabHome, _tabLock;   // Money Sort art (art/vendor)
        [SerializeField] private TMP_FontAsset _msFont;          // Lilita One (Money Sort), the footer labels
        [SerializeField] private Material _msLabel;              // its brown-outline material
        // D-069 Money Sort HomeUI "OfferIcons": the Remove Ads and Starter Pack buttons, their label material (navy outline)
        [SerializeField] private Sprite _offerRemoveAds, _offerStarter;
        [SerializeField] private Material _msOfferLabel;
        // D-070 Money Sort HomeUI "GarageGiftButton": the Gold Brick badge, its countdown material (navy outline 050D47)
        [SerializeField] private Sprite _giftBadge;
        [SerializeField] private Material _msTimerLabel;
        [SerializeField] private TMP_FontAsset _font;

        public event Action PlayPressed, SettingsPressed;
        /// <summary>A footer tab was tapped (the locked tab never raises it).</summary>
        public event Action<HomeTab> TabPressed;
        public event Action StarterPressed, RemoveAdsPressed, GiftPressed;   // D-069, D-070

        private RectTransform _root;
        private TextMeshProUGUI _title, _tagline, _play, _note, _coins, _lives, _livesNext;
        private HomeFooter _footer;
        private RectTransform _removeAdsIcon, _starterIcon;
        private RectTransform _giftIcon;
        private TextMeshProUGUI _starterLabel, _starterTimer, _giftTimer;


        private float Tall => _root != null ? Mathf.Max(0f, _root.rect.height - 1920f) : 0f;

        private void EnsureBuilt()
        {
            if (_root != null) return;
            _root = UiKit.LegacyRoot("Root", transform);
            UiKit.Fill("Ground", _root, Color.white, false).sprite = _ground;
            _coins = UiKit.CoinPill(_root, _pill, _coin, _font, string.Empty);
            (_lives, _livesNext) = UiKit.LivesPill(_root, _pill, _heart, _font);   // CR-012 E (home-lives-v04)
            float tall = Tall;
            _title = UiKit.Text("Title", _root, _font, string.Empty, DesignTokens.TypeLogo, DesignTokens.Secondary, 0f, 330f + tall / 6f, 1080f, 200f);
            _tagline = UiKit.Text("Tagline", _root, _font, string.Empty, 42f, DesignTokens.InkSoft, 0f, 530f + tall / 6f, 1080f, 60f);
            // mock-up: cards rotate about a point 520 px below their top edge, 12° apart
            for (int i = 0; i < 6; i++)
            {
                var arm = UiKit.Node($"Arm{i}", _root, 540f, 760f + tall / 3f + 520f, 0f, 0f);
                arm.localRotation = Quaternion.Euler(0f, 0f, -(i - 2.5f) * 12f);
                UiKit.Image($"Card{i}", arm, _cardFace[i], -90f, -520f, 180f, 260f);
            }
            var play = UiKit.Image("Play", _root, _buttonPrimary, 540f - 320f, 1360f + tall * 0.6f, 640f, 204f, sliced: true);
            _play = UiKit.Text("Label", play.transform, _font, string.Empty, DesignTokens.TypeDisplay, DesignTokens.OnColor, 0f, 0f, 640f, 190f);
            UiKit.Button(play, () => PlayPressed?.Invoke());
            _note = UiKit.Text("Note", _root, _font, string.Empty, 34f, DesignTokens.InkSoft, 0f, 1590f + tall * 0.6f, 1080f, 50f);
            _footer = HomeFooter.Build(_root, new HomeFooter.Art(_footerBar, _footerSelected, _tabShop, _tabHome, _tabLock, _msFont, _msLabel),
                tab => TabPressed?.Invoke(tab));
            BuildOffers();
        }


        // Money Sort HomeUI "OfferIcons": a column from (16, 190) of the 720 × 1280 screen, 136 × 134 buttons 16 apart, Remove Ads above
        // the Starter Pack (its label and countdown under it); a hidden one leaves its place to the next. × ShopArtView.Scale.
        private const float Ms = ShopArtView.Scale, OfferX = 16f, OfferTop = 190f, OfferW = 136f, OfferH = 134f, OfferGap = 16f;

        private void BuildOffers()
        {
            _removeAdsIcon = OfferIcon("RemoveAdsButton", _offerRemoveAds, () => RemoveAdsPressed?.Invoke());
            _starterIcon = OfferIcon("StarterPackButton", _offerStarter, () => StarterPressed?.Invoke());
            _starterLabel = OfferText(_starterIcon, -97f, 160f, 30f, 28f, DesignTokens.OnColor);
            _starterTimer = OfferText(_starterIcon, -137f, 140f, 31f, 18.36f, DesignTokens.MsTimer);
            // "GarageGiftButton": 136 × 133 at (-72, -256.5) from the top-right corner, its countdown 91.3 below the centre
            _giftIcon = OfferIcon("GarageGiftButton", _giftBadge, () => GiftPressed?.Invoke());
            UiKit.Place(_giftIcon, (720f - 72f - OfferW / 2f) * Ms, (256.5f - 133f / 2f) * Ms, OfferW * Ms, 133f * Ms);
            _giftTimer = OfferText(_giftIcon, -91.3f, 90f, 46f, 27.55f, DesignTokens.OnColor);
            if (_msTimerLabel != null) _giftTimer.fontSharedMaterial = _msTimerLabel;
            _giftIcon.gameObject.SetActive(false);
            _removeAdsIcon.gameObject.SetActive(false); _starterIcon.gameObject.SetActive(false);
        }

        private RectTransform OfferIcon(string name, Sprite sprite, Action pressed)
        {
            var img = UiKit.Image(name, _root, sprite, OfferX * Ms, OfferTop * Ms, OfferW * Ms, OfferH * Ms);
            img.preserveAspect = true;
            UiKit.Button(img, pressed);
            return img.rectTransform;
        }

        // a centred Lilita One line (dy Money Sort px from the icon's centre), white with the navy outline
        private TextMeshProUGUI OfferText(RectTransform icon, float dy, float w, float h, float size, Color color)
        {
            var t = UiKit.Text("Text", icon, _msFont, string.Empty, size * Ms, color, (OfferW / 2f - w / 2f) * Ms, (OfferH / 2f - dy - h / 2f) * Ms, w * Ms, h * Ms);
            t.fontStyle = FontStyles.Normal;
            if (_msOfferLabel != null) t.fontSharedMaterial = _msOfferLabel;
            return t;
        }

        /// <summary>D-070: the Gold Brick badge with its countdown; null hides it.</summary>
        public void SetGift(string timer)
        {
            if(_moneySortHome!=null){var ui=SourceHome;ui.SetGarageGift(timer==null?0:1);ui.SetLabel("_garageGiftTimerText",timer);return;}

            EnsureBuilt();
            _giftIcon.gameObject.SetActive(timer != null);
            if (timer != null) UiKit.SetText(_giftTimer, timer);
        }

        /// <summary>D-069: the offer icons — Remove Ads when <paramref name="removeAds"/>, the Starter Pack with its label and countdown
        /// when <paramref name="starterTimer"/> is not null.</summary>
        public void SetOffers(bool removeAds, string starterLabel, string starterTimer)
        {
            if(_moneySortHome!=null){var ui=SourceHome;ui.SetOfferIcons(starterTimer==null?0:1,removeAds);ui.SetLabel("_starterPackTimerText",starterTimer);return;}

            EnsureBuilt();
            _removeAdsIcon.gameObject.SetActive(removeAds);
            bool starter = starterTimer != null;
            _starterIcon.gameObject.SetActive(starter);
            if (!starter) return;
            UiKit.SetText(_starterLabel, starterLabel);
            UiKit.SetText(_starterTimer, starterTimer);
            UiKit.Place(_starterIcon, OfferX * Ms, (OfferTop + (removeAds ? OfferH + OfferGap : 0f)) * Ms, OfferW * Ms, OfferH * Ms);
        }

        /// <summary>The footer: which tab is lit, and the two tab labels (localized).</summary>
        public void SetCoins(string coins)
        {
            if (_moneySortHome != null) { SourceHome.SetLabel("_moneyText", coins); return; }
            EnsureBuilt();
            _coins.SetText(coins ?? string.Empty);
        }
        /// <summary>The Shop tab page goes under Home's footer (one shared footer); see <see cref="HomeUI.MountPage"/>.</summary>
        public void MountPage(Transform page)
        {
            if (_moneySortHome != null) SourceHome.MountPage(page);
        }

        /// <summary>Lights a footer tab only; Home's content stays as it is.</summary>
        public void SetFooterTab(HomeTab tab)
        {
            if (_moneySortHome != null) SourceHome.SetFooterTab(tab == HomeTab.Shop ? MoneySortHomeTab.Shop : MoneySortHomeTab.Main);
        }

        public void SetFooterVisible(bool visible)
        {
            if (_moneySortHome != null) { SourceHome.SetFooterVisible(visible); return; }
            _root?.Find("Footer")?.gameObject.SetActive(visible);
        }

        public void SetFooter(HomeTab selected, string shopLabel, string homeLabel)
        {
            if(_moneySortHome!=null){SourceHome.SetFooterLabels(shopLabel,homeLabel);SourceHome.SetSelectedTab(selected==HomeTab.Shop?MoneySortHomeTab.Shop:MoneySortHomeTab.Main);return;}

            EnsureBuilt();
            _footer.Set(selected, shopLabel, homeLabel);
        }

        /// <param name="note">Line under Play (e.g. "More levels coming soon"); null hides it.</param>
        public void SetHardPlay(bool hard) => _moneySortHome?.SetHardPlay(hard);

        public void Show(string title, string tagline, string playLabel, string note, string coins)
        {
            if(_moneySortHome!=null){var ui=SourceHome;ui.SetData(new HomePresentation(MoneySortUiValue.Number(playLabel),MoneySortUiValue.Number(coins),0,5,0,()=>PlayPressed?.Invoke(),()=>SettingsPressed?.Invoke(),()=>TabPressed?.Invoke(HomeTab.Shop),()=>TabPressed?.Invoke(HomeTab.Home),null,0,()=>StarterPressed?.Invoke(),()=>RemoveAdsPressed?.Invoke(),()=>GiftPressed?.Invoke(),()=>TabPressed?.Invoke(HomeTab.Shop)));ui.SetLabel("_moneyText",coins);return;}

            EnsureBuilt();
            _coins.SetText(coins ?? string.Empty);
            _title.SetText(title ?? string.Empty);
            _tagline.SetText(tagline ?? string.Empty);
            _play.SetText(playLabel ?? string.Empty);
            _note.gameObject.SetActive(note != null);
            _note.SetText(note ?? string.Empty);
        }

        /// <summary>CR-012 E: the lives pill — the count on the heart and "Full" or the time to the next life (both localized).</summary>
        public void SetLives(string count, string next)
        {
            if(_moneySortHome!=null){var ui=SourceHome;ui.SetLabel("_livesText",count);ui.SetLabel("_lifeTimerText",next);return;}

            EnsureBuilt();
            _lives.SetText(count ?? string.Empty);
            _livesNext.SetText(next ?? string.Empty);
        }
    }
}
