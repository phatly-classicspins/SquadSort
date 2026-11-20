using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.Views
{
    // Shop page: Remove Ads on top, then Special Packs, then coin packs. Every card is laid out
    // in the prefab and names its product id; this only binds store data and hides cards whose
    // product is not on sale (e.g. Remove Ads after it was bought).
    public sealed class ShopUI : UIBase
    {
        public void SetFooterLabels(string shop, string home) => _footer?.SetLabels(shop, home);

        public void BindCardSlot(IReadOnlyList<ShopSectionVisual> sections,Action<string> buy,Action close,Action restore,bool overlay)
        {
            var offers=new List<ShopOfferView>();
            foreach(var section in sections)foreach(var card in section.Cards){var b=MoneySortUiValue.Number(card.Booster);var group=card.Kind==ShopCardKind.RemoveAds?MoneySortShopOfferGroup.RemoveAds:card.Kind==ShopCardKind.Starter?MoneySortShopOfferGroup.Starter:card.Kind==ShopCardKind.Gold?MoneySortShopOfferGroup.CoinPack:MoneySortShopOfferGroup.SpecialPack;var id=card.Id;offers.Add(new ShopOfferView(id,card.Title,card.Price,group,new MoneySortShopReward{coins=MoneySortUiValue.Number(card.Coins),shuffle=b,hammer=b,freeMove=b,infiniteLivesMinutes=MoneySortUiValue.Number(card.Lives),removeAds=card.Kind==ShopCardKind.RemoveAds},null,true,()=>buy?.Invoke(id)));}
            SetData(new ShopPresentation(offers,close,restore,overlay));
            foreach(var section in sections)foreach(var card in section.Cards)foreach(var item in _items)if(item!=null&&item.ProductId==card.Id)item.BindCardSlotLabels(card);
        }

        [SerializeField] private Button _settingsButton;
        private Action _settings;
        public void SetSettings(Action settings) => _settings = settings;
        private bool _moneySortInitialized;
        [Serializable]
        private sealed class Section
        {
            public MoneySortShopOfferGroup group;
            [Tooltip("Hidden when no card of this group is on sale.")]
            public GameObject header;
        }

        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private ShopOfferItemUI[] _items = Array.Empty<ShopOfferItemUI>();
        [SerializeField] private Section[] _sections = Array.Empty<Section>();
        [SerializeField] private Button _closeButton;
        [SerializeField] private HomeFooterUI _footer;
        [SerializeField] private Button _restoreButton;
        [Header("Overlay (opened from gameplay)")]
        [Tooltip("Stretched down over the footer area when the Shop opens as an overlay.")]
        [SerializeField] private RectTransform _panel;
        [Tooltip("Shown only as the Home tab, e.g. the selected footer tab.")]
        [SerializeField] private GameObject[] _homeTabOnly = Array.Empty<GameObject>();
        [Tooltip("Shown only as an overlay: backdrop, resource bar and close button.")]
        [SerializeField] private GameObject[] _overlayOnly = Array.Empty<GameObject>();
        [SerializeField] private ResourceGroupUI _overlayResources;

        private readonly HashSet<MoneySortShopOfferGroup> _visibleGroups = new();
        private ShopPresentation _presentation;
        private float _panelBottom;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(() => _settings?.Invoke());
            if (_footer != null)
                _footer.Init(tab => { if (tab == MoneySortHomeTab.Main) OnClose(); });
            if (_panel != null)
                _panelBottom = _panel.offsetMin.y;
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnClose);
            if (_restoreButton != null)
                _restoreButton.onClick.AddListener(OnRestore);
        }

        public override void SetData(params object[] data)
        {
            var wasShown = _presentation != null && gameObject.activeSelf;
            _presentation = data.Length > 0 ? data[0] as ShopPresentation : null;
            Refresh();
            if (!wasShown && _scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 1f;
        }

        public override void Hide()
        {
            base.Hide();
            _presentation = null;
        }

        private Tween _pageFade;
        private bool _hostFooter;

        /// <summary>The Home tab page: Home's footer, drawn above this page, serves both tabs — this page's own footer stays hidden.</summary>
        public void UseHostFooter()
        {
            _hostFooter = true;
            if (_footer != null) _footer.gameObject.SetActive(false);
        }

        /// <summary>Home ⇄ Shop footer tab: fades the page (everything but its own footer) in or out over Home;
        /// <paramref name="done"/> runs when the fade ends.</summary>
        public void FadePage(bool shown, Action done = null)
        {
            _pageFade?.Kill();
            var groups = new List<CanvasGroup>();
            foreach (Transform child in transform)
            {
                if (_footer != null && child == _footer.transform) continue;
                var group = child.GetComponent<CanvasGroup>();
                if (group == null) group = child.gameObject.AddComponent<CanvasGroup>();   // Unity null: never ??
                group.blocksRaycasts = shown;
                groups.Add(group);
            }
            float alpha = shown ? 0f : 1f;
            foreach (var group in groups) group.alpha = alpha;
            _pageFade = DOTween.To(() => alpha, v => { alpha = v; foreach (var group in groups) if (group != null) group.alpha = v; },
                    shown ? 1f : 0f, DesignTokens.Motion.TabFade)
                .SetEase(shown ? Ease.OutQuad : Ease.InQuad).SetUpdate(true)
                .OnComplete(() => { _pageFade = null; done?.Invoke(); });
        }

        private void Refresh()
        {
            if (_presentation == null)
                return;
            var overlay = _presentation.Overlay;
            if (_footer != null)
            {
                _footer.gameObject.SetActive(!overlay && !_hostFooter);
                _footer.SetSelected(MoneySortHomeTab.Shop);
            }
            foreach (var item in _homeTabOnly)
                item?.SetActive(!overlay);
            foreach (var item in _overlayOnly)
                item?.SetActive(overlay);
            if (_panel != null)
                _panel.offsetMin = new Vector2(_panel.offsetMin.x, overlay ? 0f : _panelBottom);
            _visibleGroups.Clear();
            foreach (var item in _items)
            {
                if (item == null)
                    continue;
                var offer = Find(item.ProductId);
                item.gameObject.SetActive(offer != null);
                if (offer == null)
                    continue;
                item.Bind(offer);
                _visibleGroups.Add(offer.Group);
            }
            foreach (var section in _sections)
            {
                if (section?.header != null)
                    section.header.SetActive(_visibleGroups.Contains(section.group));
            }
        }

        // The overlay's own resource bar; as the Home tab the Shop sits under Home's bar.
        public void SetMoney(int amount) => _overlayResources?.SetMoney(amount);

        public void SetLives(int current, int maximum, int refillSeconds, int infiniteSeconds) =>
            _overlayResources?.SetLives(current, maximum, refillSeconds, infiniteSeconds);

        private ShopOfferView Find(string productId)
        {
            foreach (var offer in _presentation.Offers)
            {
                if (offer.Id == productId)
                    return offer;
            }
            return null;
        }

        private void OnClose() => _presentation?.Close?.Invoke();
        private void OnRestore() => _presentation?.Restore?.Invoke();

        private void OnDestroy()
        {
            _pageFade?.Kill();
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnClose);
            if (_restoreButton != null)
                _restoreButton.onClick.RemoveListener(OnRestore);
        }
    }
}
