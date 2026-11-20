using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Views
{
    public sealed class PurchaseResultPresentation
    {
        // Null for a failed purchase.
        public readonly MoneySortShopReward Reward;
        public readonly Action Close;

        public PurchaseResultPresentation(MoneySortShopReward reward, Action close)
        {
            Reward = reward;
            Close = close;
        }
    }

    // "REWARDS!" popup after a successful purchase, and the "Purchase Failed" popup. The reward
    // popup shows one of three layouts: Remove Ads, coins only, or a bundle (coins, lives, boosters).
    public sealed class PurchaseResultUI : UIBase
    {
        private bool _moneySortInitialized;
        [Tooltip("Continue and the X both close the popup.")]
        [SerializeField] private Button[] _closeButtons = Array.Empty<Button>();

        [Header("Reward layouts (reward popup only)")]
        [SerializeField] private GameObject _removeAdsContent;
        [SerializeField] private GameObject _coinsContent;
        [SerializeField] private TextMeshProUGUI _coinsText;
        [SerializeField] private GameObject _bundleContent;
        [SerializeField] private ShopOfferItemUI _bundleRewards;

        private PurchaseResultPresentation _presentation;

        public override void Init(object manager)
        {
            if (_moneySortInitialized) return;
            _moneySortInitialized = true;
            base.Init(manager);
            foreach (var button in _closeButtons)
                button?.onClick.AddListener(OnClose);
        }

        public override void SetData(params object[] data)
        {
            _presentation = data.Length > 0 ? data[0] as PurchaseResultPresentation : null;
            var reward = _presentation?.Reward;
            if (reward == null)
                return;
            bool bundle = reward.removeAds || reward.shuffle > 0 || reward.hammer > 0 ||
                          reward.freeMove > 0 || reward.infiniteLivesMinutes > 0;
            SetActive(_removeAdsContent, reward.removeAds);
            SetActive(_coinsContent, !bundle);
            SetActive(_bundleContent, bundle);
            if (_coinsText != null)
                _coinsText.text = reward.coins.ToString("N0");
            if (!bundle || _bundleRewards == null) return;

            _bundleRewards.Bind(new ShopOfferView(null, null, null, default, reward, null, true, null));
            var content = _bundleContent.transform;
            foreach (var name in new[] { "Heart", "Infinity", "InfiniteLives" })
                content.Find(name).gameObject.SetActive(reward.infiniteLivesMinutes > 0);
            foreach (var name in new[] { "CoinsIcon", "Coins" })
            {
                var rect = (RectTransform)content.Find(name);
                rect.anchoredPosition = new Vector2(reward.removeAds ? 108f : -92f, rect.anchoredPosition.y);
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private void OnClose() => _presentation?.Close?.Invoke();

        private void OnDestroy()
        {
            foreach (var button in _closeButtons)
                button?.onClick.RemoveListener(OnClose);
        }
    }
}
