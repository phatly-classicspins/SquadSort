using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using Game.Application;
using Game.Gen;
using Game.Views;

namespace Game.Presentation
{
    /// <summary>
    /// The Remove Ads offer (features/shop.md §4, D-067) — Money Sort's RemoveAdsUI opened directly (07-11-2026). Buy goes through
    /// <see cref="ShopPurchases"/> and closes with true once bought; ✕ / Back close with false (not while a purchase is in flight).
    /// When it shows (after an interstitial, from level 15) is the caller's. D-071: tracking reports the popup as the store.
    /// </summary>
    public sealed class RemoveAdsPopup
    {
        private readonly PopupUi _popups;
        private readonly ShopPurchases _shop;

        public RemoveAdsPopup(PopupUi popups, ShopPurchases shop) { _popups = popups; _shop = shop; }

        public UniTask<bool> ShowAsync(CancellationToken ct)
        {
            const string placement = TrackingNames.RemoveAdsPopup;
            var pack = ShopCatalog.Find(ShopCatalog.RemoveAdsId);
            return _popups.ShowAsync<IapOfferPopupUI, bool>(AssetKeys.UI.RemoveAdsUI, () => false, (ui, h) =>
            {
                string coins = _shop.Coins(pack);
                ui.SetData(new IapOfferPopupPresentation(new ShopOfferView(null, null, _shop.Price(pack), MoneySortShopOfferGroup.RemoveAds,
                    new MoneySortShopReward { coins = MoneySortUiValue.Number(coins), removeAds = true }, null, true,
                    () => Buy(h, pack, placement, ct).Forget()), null, null, null, () => { if (!h.Busy) h.Close(false); }));
                ui.SetLabel("_coinsText", coins);
                _shop.StoreEntered(placement, "Remove Ads", placement, new[] { pack });
            }, ct);
        }

        private async UniTaskVoid Buy(PopupHandle<bool> h, ShopPack pack, string placement, CancellationToken ct)
        {
            if (h.Busy) return;
            h.Busy = true;
            try { if (await _shop.BuyAsync(pack, placement, ct)) h.Close(true); }
            catch (OperationCanceledException) { }
            finally { h.Busy = false; }
        }
    }
}
