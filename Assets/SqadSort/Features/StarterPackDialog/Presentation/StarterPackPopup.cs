using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using Game.Gen;
using Game.Views;

namespace Game.Presentation
{
    /// <summary>
    /// The Starter Pack offer (features/shop.md §3, D-067) — Money Sort's StarterBundleUI opened directly (07-11-2026). Counts down to
    /// the end of its window once a second and closes by itself when it ends; Buy goes through <see cref="ShopPurchases"/> and closes
    /// with true once bought; ✕ / Back close with false (not while a purchase is in flight). D-071: what opened it is only its trigger;
    /// tracking reports the popup as the store.
    /// </summary>
    public sealed class StarterPackPopup
    {
        private readonly PopupUi _popups;
        private readonly ShopPurchases _shop;
        private readonly LevelProgressService _progress;
        private readonly IClock _clock;
        private readonly ILocalizationService _loc;

        public StarterPackPopup(PopupUi popups, ShopPurchases shop, LevelProgressService progress, IClock clock, ILocalizationService loc)
        {
            _popups = popups; _shop = shop; _progress = progress; _clock = clock; _loc = loc;
        }

        public UniTask<bool> ShowAsync(CancellationToken ct)
        {
            const string placement = TrackingNames.StarterPopup;
            var pack = ShopCatalog.Find(ShopCatalog.StarterId);
            return _popups.ShowAsync<IapOfferPopupUI, bool>(AssetKeys.UI.StarterBundleUI, () => false, (ui, h) =>
            {
                string title = _loc.Get(LocKeys.PackStarterPack), price = _shop.Price(pack), lives = _shop.Lives(pack);
                int booster = MoneySortUiValue.Number(_shop.Booster(pack));
                ui.SetData(new IapOfferPopupPresentation(new ShopOfferView(null, title, price, MoneySortShopOfferGroup.Starter,
                    new MoneySortShopReward
                    {
                        coins = MoneySortUiValue.Number(_shop.Coins(pack)), shuffle = booster, hammer = booster, freeMove = booster,
                        infiniteLivesMinutes = MoneySortUiValue.Number(lives)
                    }, null, true, () => Buy(h, pack, placement, ct).Forget()),
                    title, _loc.Get(LocKeys.OfferLimited), null, () => { if (!h.Busy) h.Close(false); }));
                ui.SetLabel("_infiniteLivesText", lives);
                void Countdown()
                {
                    var left = _progress.StarterOfferLeft(_clock.UtcNow);
                    if (left <= TimeSpan.Zero && !h.Busy) { h.Close(false); return; }
                    ui.SetLabel("_timerText", _loc.Get(LocKeys.OfferTimer, (int)left.TotalHours, left.Minutes, left.Seconds));
                    ui.ShowLabel("_timerText", true);
                }
                Countdown();
                PopupUi.EverySecond(h.Token, Countdown).Forget();
                _shop.StoreEntered(placement, "Starter Pack", placement, new[] { pack });
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
