using System;
using System.Collections.Generic;
using System.Threading;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Presentation;
using Cysharp.Threading.Tasks;
using Game.Application;
using Game.Gen;
using Game.Views;

namespace Game.Presentation
{
    /// <summary>
    /// D-067 (features/shop.md §2, Money Sort ShopService): one purchase from tap to popup. The store sells the framework product of
    /// the same id (<c>Resources/Config/products.json</c>); on success the pack is granted and saved (<see cref="LevelProgressService.GrantPurchase"/>)
    /// before Rewards! shows; a failure shows Purchase failed and changes nothing; the player cancelling shows nothing. Also turns a
    /// pack into the labels every Shop dialog draws. Root scope: the Store opens over Home and Gameplay.
    /// </summary>
    public sealed class ShopPurchases
    {
        private readonly IPurchaseService _purchases;
        private readonly LevelProgressService _progress;
        private readonly IEntitlementService _entitlements;
        private readonly IClock _clock;
        private readonly ILocalizationService _loc;
        private readonly IGameTracking _tracking;   // D-071
        private readonly ILog _log;

        public ShopPurchases(IPurchaseService purchases, LevelProgressService progress, IEntitlementService entitlements,
            IClock clock, ILocalizationService loc, IGameTracking tracking, ILog log, IAssetService assets, IRenderLayerRegistry layers)
        {
            _assets = assets; _layers = layers; _purchases = purchases; _progress = progress; _entitlements = entitlements; _clock = clock; _loc = loc; _tracking = tracking; _log = log;
        }

        public bool RemoveAdsOwned => _entitlements.Has(OnlineGate.RemoveAds);

        /// <summary>Buy <paramref name="pack"/>; true once granted and the Rewards! popup was closed.</summary>
        public async UniTask<bool> BuyAsync(ShopPack pack, string placement, CancellationToken ct)
        {
            ProductInfo product = null;
            foreach (var p in _purchases.Catalog) if (p.Id == pack.StoreId) { product = p; break; }   // D-133: products.json holds the store ids (GDD 6.1)
            string name = TrackingName(pack);
            _tracking.IapStarted(pack, name, placement);   // D-071
            var result = product == null ? PurchaseResult.Failed : await _purchases.BuyAsync(product, ct);
            _log.Info($"[Shop] {pack.Id} from {placement}: {result}");
            if (result == PurchaseResult.Success)
            {
                // the store has charged: a failed save is logged, never hidden behind "failed" (the store would not charge again)
                if (!_progress.GrantPurchase(pack, _clock.UtcNow, placement)) _log.Error($"[Shop] {pack.Id} paid but the save failed.");
                _tracking.IapCompleted(pack, name, placement);
                await ShowPurchaseResultAsync(pack, ct);
                return true;
            }
            if (result == PurchaseResult.Failed || result == PurchaseResult.Cancelled)
                _tracking.IapFailed(pack, name, placement, result == PurchaseResult.Cancelled ? "cancelled" : "failed");
            if (result == PurchaseResult.Failed)
                await ShowPurchaseResultAsync(null, ct);
            return false;   // Cancelled, Pending, Deferred: nothing to show
        }

        private readonly IAssetService _assets;
        private readonly IRenderLayerRegistry _layers;
        private UniTaskCompletionSource _overlayClose;
        private bool _overlayBuying;

        public bool CloseUI()
        {
            if (_overlayClose == null) return false;
            if (!_overlayBuying) _overlayClose.TrySetResult();
            return true;
        }

        /// <summary>The authored ShopUI covers the caller until X/Back. No ShopDialog participates.</summary>
        public async UniTask OpenOverlayAsync(string placement, CancellationToken ct)
        {
            if (_overlayClose != null) return;
            var close = _overlayClose = new UniTaskCompletionSource();
            UnityEngine.GameObject prefab = null, instance = null;
            ShopUI ui = null;
            using var ticking = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                prefab = await _assets.LoadAsync(AssetKeys.UI.ShopUI, ct);
                instance = UnityEngine.Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                ui = instance.GetComponent<ShopUI>();
                ui.SetLayout(0f, 1f);
                ui.Init(null);
                Draw();
                ui.Show();
                StoreEntered(TrackingNames.ShopStore, "Shop", placement, _progress.ShopPacks(_clock.UtcNow, RemoveAdsOwned));
                Tick().Forget();
                await close.Task.AttachExternalCancellation(ct);
                ticking.Cancel();
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            finally
            {
                ticking.Cancel();
                if (ui != null) ui.Animation?.Stop();
                if (instance != null) UnityEngine.Object.Destroy(instance);
                ui = null;
                if (prefab != null) _assets.Release(prefab);
                _overlayClose = null;
                _overlayBuying = false;
            }

            void Draw()
            {
                if (ui == null) return;
                ui.BindCardSlot(Sections(), id => Run(async token =>
                {
                    var pack = ShopCatalog.Find(id);
                    if (pack != null) await BuyAsync(pack, placement, token);
                }), () => CloseUI(), () => Run(RestoreAsync), overlay: true);
                ui.SetMoney((int)_progress.Coins);
                var (lives, next) = LivesPill();
                ui.SetLabel("_livesText", lives);
                ui.SetLabel("_lifeTimerText", next);
                ui.SetButtonLabel("_restoreButton", _loc.Get(LocKeys.ShopRestore));
            }
            void Run(Func<CancellationToken, UniTask> work)
            {
                if (_overlayBuying || ticking.IsCancellationRequested) return;
                _overlayBuying = true;
                Purchase().Forget();
                async UniTask Purchase()
                {
                    try { await work(ticking.Token); }
                    catch (OperationCanceledException) { }
                    catch (Exception e) { _log.Warn("[Shop UI purchase] " + e.Message); }
                    finally { _overlayBuying = false; if (ui != null) Draw(); }
                }
            }
            async UniTask Tick()
            {
                bool starter = _progress.StarterOfferLeft(_clock.UtcNow) > TimeSpan.Zero;
                while (!ticking.IsCancellationRequested)
                {
                    if (await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true,
                        cancellationToken: ticking.Token).SuppressCancellationThrow()) return;
                    if (ui == null || _overlayBuying) continue;
                    bool now = _progress.StarterOfferLeft(_clock.UtcNow) > TimeSpan.Zero;
                    if (now != starter) { starter = now; Draw(); }
                    else if (now) ui.SetLabel("_timerText", StarterCountdown());
                    var (lives, next) = LivesPill();
                    ui.SetLabel("_livesText", lives);
                    ui.SetLabel("_lifeTimerText", next);
                }
            }
        }


        public async UniTask RestoreAsync(CancellationToken ct)
        {
            var result = await _purchases.RestoreAsync(ct);
            _log.Info($"[Shop] restore: {result}");
            // D-071: Remove Ads is the one non-consumable a restore gives back
            var removeAds = ShopCatalog.Find(ShopCatalog.RemoveAdsId);
            if (result != RestoreResult.Success) return;
            if (RemoveAdsOwned) _tracking.IapRestored(removeAds, TrackingName(removeAds));
            await ShowRestorePurchaseAsync(ct);
        }

        private async UniTask ShowPurchaseResultAsync(ShopPack pack, CancellationToken ct)
        {
            var failed = pack == null;
            var key = failed ? AssetKeys.UI.PurchaseFailedUI : AssetKeys.UI.PurchaseRewardUI;
            var prefab = await _assets.LoadAsync(key, ct);
            UnityEngine.GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                var ui = instance.GetComponent<PurchaseResultUI>();
                var close = new UniTaskCompletionSource();
                ui.Init(null);
                ui.SetData(new PurchaseResultPresentation(failed ? null : new MoneySortShopReward
                {
                    coins = (int)Math.Min(pack.Coins, int.MaxValue),
                    removeAds = pack.Id == ShopCatalog.RemoveAdsId,
                    infiniteLivesMinutes = pack.UnlimitedLivesHours * 60,
                    hammer = pack.BoosterEach,
                    shuffle = pack.BoosterEach,
                    freeMove = pack.BoosterEach
                }, () => close.TrySetResult()));
                ui.SetLabel("@OfferCard/Title", _loc.Get(failed ? LocKeys.PurchaseFailedTitle : LocKeys.PurchaseRewards));
                if (failed) ui.SetLabel("@OfferCard/Message", _loc.Get(LocKeys.PurchaseFailedBody));
                ui.SetLabel("@OfferCard/ContinueButton/Label", _loc.Get(LocKeys.PurchaseContinue));
                ui.Show();
                await close.Task.AttachExternalCancellation(ct);
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.Destroy(instance);
                _assets.Release(prefab);
            }
        }

        private async UniTask ShowRestorePurchaseAsync(CancellationToken ct)
        {
            var prefab = await _assets.LoadAsync(AssetKeys.UI.RestorePurchaseUI, ct);
            UnityEngine.GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                var ui = instance.GetComponent<RestorePurchaseUI>();
                var close = new UniTaskCompletionSource();
                ui.Init(null);
                ui.Bind(_loc.Get(LocKeys.PurchaseRestoreTitle), _loc.Get(LocKeys.PurchaseRestoreBody),
                    _loc.Get(LocKeys.PurchaseRestoreOk), () => close.TrySetResult());
                ui.Show();
                await close.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                if (instance != null) UnityEngine.Object.Destroy(instance);
                _assets.Release(prefab);
            }
        }

        /// <summary>D-071 store_entered for the Store (<paramref name="storeId"/> "shop") or an offer popup (its own id), with the packs it shows.</summary>
        public void StoreEntered(string storeId, string storeName, string placement, IEnumerable<ShopPack> shown)
        {
            var ids = new List<string>();
            foreach (var pack in shown) ids.Add(pack.StoreId);
            _tracking.StoreEntered(storeId, storeName, placement, ids);
        }

        /// <summary>The product name the tracking plan reports (Money Sort: the catalog title) — the pack name, or "1,000 Gold".</summary>
        public string TrackingName(ShopPack pack) => Name(pack) ?? pack.Coins.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture) + " Gold";

        /// <summary>The price label: the store's localized price, or the GDD price while the store is the Editor's fake (it answers
        /// "$0.99" for everything) or has not reported one.</summary>
        public string Price(ShopPack pack)
        {
            if (!(_purchases is FakePurchaseService))
                foreach (var p in _purchases.Catalog)
                    if (p.Id == pack.StoreId && !string.IsNullOrEmpty(p.LocalizedPrice)) return p.LocalizedPrice;
            return ShopCatalog.FallbackPrice(pack);
        }

        public string Coins(ShopPack pack) => pack.Coins > 0 ? _loc.Get(LocKeys.HudCoins, pack.Coins) : null;
        public string Booster(ShopPack pack) => pack.BoosterEach > 0 ? _loc.Get(LocKeys.ShopBooster, pack.BoosterEach) : null;
        public string Lives(ShopPack pack) => pack.UnlimitedLivesHours > 0 ? _loc.Get(LocKeys.ShopLives, pack.UnlimitedLivesHours) : null;

        public string Name(ShopPack pack) => pack.Id switch
        {
            ShopCatalog.RemoveAdsId => _loc.Get(LocKeys.PackRemoveAds),
            ShopCatalog.StarterId => _loc.Get(LocKeys.PackStarterPack),
            "pack_small" => _loc.Get(LocKeys.PackPackSmall),
            "pack_medium" => _loc.Get(LocKeys.PackPackMedium),
            "pack_ultra" => _loc.Get(LocKeys.PackPackUltra),
            _ => null,
        };

        /// <summary>The Money Sort picture of a pack (a sprite of the shop atlas).</summary>
        public static string Icon(ShopPack pack) => pack.Id switch
        {
            ShopCatalog.RemoveAdsId => "shop_icon_remove_ads_tv",
            "pack_medium" => "shop_pack_pile_10000",
            "pack_ultra" => "shop_pack_pile_25000",
            "coins_1K" => "shop_gold_icon_1000",
            "coins_5K" => "shop_gold_icon_5000",
            "coins_10K" => "shop_gold_icon_10000",
            "coins_25K" => "shop_gold_icon_25000",
            "coins_50K" => "shop_gold_icon_50000",
            "coins_100K" => "shop_gold_icon_100000",
            _ => "shop_pack_pile_5000",   // Starter, Small
        };

        /// <summary>The Store's sections for what is on sale now (§2): Remove Ads, Starter, Special Pack, Gold Pack; an empty one is left out.</summary>
        public IReadOnlyList<ShopSectionVisual> Sections()
        {
            var bySection = new Dictionary<ShopSection, List<ShopCardVisual>>();
            foreach (var pack in _progress.ShopPacks(_clock.UtcNow, RemoveAdsOwned))
            {
                if (!bySection.TryGetValue(pack.Section, out var list)) bySection[pack.Section] = list = new List<ShopCardVisual>();
                list.Add(Card(pack));
            }
            var sections = new List<ShopSectionVisual>();
            void Add(ShopSection s, string header, ShopCardKind kind)
            {
                if (bySection.TryGetValue(s, out var cards)) sections.Add(new ShopSectionVisual(header, kind, cards));
            }
            Add(ShopSection.RemoveAds, null, ShopCardKind.RemoveAds);
            Add(ShopSection.Limited, null, ShopCardKind.Starter);
            Add(ShopSection.Special, _loc.Get(LocKeys.ShopSectionSpecial), ShopCardKind.Pack);
            Add(ShopSection.Gold, _loc.Get(LocKeys.ShopSectionGold), ShopCardKind.Gold);
            return sections;
        }

        public string[] RemoveAdsBenefits(ShopPack pack) => new[]
        {
            _loc.Get(LocKeys.ShopNoadsBenefit1), _loc.Get(LocKeys.ShopNoadsBenefit2),
            pack.Coins > 0 ? _loc.Get(LocKeys.ShopNoadsCoins, pack.Coins) : _loc.Get(LocKeys.ShopNoadsBenefit3),
        };

        private ShopCardVisual Card(ShopPack pack)
        {
            var kind = pack.Section == ShopSection.RemoveAds ? ShopCardKind.RemoveAds : pack.Section == ShopSection.Gold ? ShopCardKind.Gold
                : pack.Section == ShopSection.Limited ? ShopCardKind.Starter : ShopCardKind.Pack;
            string badge = pack.Tag == ShopTag.Hot ? "shop_badge_hot" : pack.Tag == ShopTag.Off ? "shop_badge_60off" : null;
            string badgeText = pack.Tag == ShopTag.BestValue ? _loc.Get(LocKeys.ShopTagBest) : null;
            bool starter = kind == ShopCardKind.Starter;
            return new ShopCardVisual(pack.Id, kind, Name(pack), Icon(pack), Coins(pack), Booster(pack), Lives(pack), Price(pack), badge, badgeText,
                kind == ShopCardKind.RemoveAds ? RemoveAdsBenefits(pack) : null, kind == ShopCardKind.RemoveAds ? _loc.Get(LocKeys.ShopNoadsLabel) : null,
                starter ? StarterCountdown() : null, starter ? _loc.Get(LocKeys.OfferLimitedCard) : null);
        }

        /// <summary>Time left in the Starter Pack window, "hh:mm:ss".</summary>
        public string StarterCountdown()
        {
            var left = _progress.StarterOfferLeft(_clock.UtcNow);
            return _loc.Get(LocKeys.OfferTimer, (int)left.TotalHours, left.Minutes, left.Seconds);
        }

        /// <summary>The lives pill as Home shows it: the count, then "Full" or the time to the next life; ∞ and its time while
        /// unlimited lives run (GDD §1.3).</summary>
        public (string count, string next) LivesPill()
        {
            var now = _clock.UtcNow;
            var unlimited = _progress.UnlimitedLivesLeft(now);
            if (unlimited > System.TimeSpan.Zero) return (_loc.Get(LocKeys.LivesUnlimited), MainScreen.UnlimitedTimer(_loc, unlimited));
            var left = _progress.NextLifeIn(now);
            return (_loc.Get(LocKeys.LivesCount, _progress.Lives(now)),
                left <= System.TimeSpan.Zero ? _loc.Get(LocKeys.LivesFull) : _loc.Get(LocKeys.LivesTimer, (int)left.TotalMinutes, left.Seconds));
        }
    }
}
