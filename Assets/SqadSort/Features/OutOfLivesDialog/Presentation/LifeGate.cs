using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Presentation;
using Game.Application;
using Game.Gen;
using Game.Views;

namespace Game.Presentation
{
    /// <summary><c>Ad</c> = the player chose the rewarded ad (the caller shows it and grants the life); <c>Coins</c> = the coin refill
    /// (the caller pays and grants, D-062); <c>Refilled</c> = a life came back while the dialog was up; <c>Close</c> = ✕ or Back.</summary>
    public enum OutOfLivesChoice { Close, Ad, Refilled, Coins }

    /// <summary>
    /// CR-012 E / Money Sort: Out of Lives on Home — after the last life went on leaving a level, or Play with none. With a life it
    /// passes at once; without, Out of Lives shows until a refill (coins, or a rewarded ad — G19: granted only on completion), a life
    /// arriving, or ✕. It never spends a life: leaving an attempt does (GameplayScreen.LeaveAttempt).
    /// </summary>
    public sealed class LifeGate
    {
        private UniTaskCompletionSource<OutOfLivesChoice> _activeChoice;
        public void CloseUI() => _activeChoice?.TrySetResult(OutOfLivesChoice.Close);
        private readonly NoAdsPopup _noAds;
        private readonly IAssetService _assets;
        private readonly IRenderLayerRegistry _layers;
        private readonly IAdsService _ads;
        private readonly ILocalizationService _loc;
        private readonly LevelProgressService _progress;
        private readonly IClock _clock;
        private readonly AdPacing _adPacing;
        private readonly IGameTracking _tracking;   // D-061
        private readonly EconomyTuning _tuning;     // D-062: the refill amount shown on the coin button
        private readonly ShopPurchases _shop;       // D-067: short of coins, the Store opens over Out of Lives

        public LifeGate(NoAdsPopup noAds, IAdsService ads, ILocalizationService loc, LevelProgressService progress, IClock clock,
            AdPacing adPacing, IGameTracking tracking, EconomyTuning tuning, ShopPurchases shop, IAssetService assets, IRenderLayerRegistry layers)
        {
            _noAds = noAds; _ads = ads; _loc = loc; _progress = progress; _clock = clock; _adPacing = adPacing; _tracking = tracking; _tuning = tuning; _shop = shop; _assets = assets; _layers = layers;
        }

        // The authored MoneySort prefab supplies layout, animation, scrim and input surface.
        private async UniTask<OutOfLivesChoice> ShowUIAsync(bool adReady, CancellationToken ct)
        {
            var prefab = await _assets.LoadAsync(AssetKeys.UI.OutOfLivesUI, ct);
            UnityEngine.GameObject instance = null;
            OutOfLivesUI ui = null;
            using var ticking = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                ui = instance.GetComponent<OutOfLivesUI>();
                ui.Init(null);
                var choice = _activeChoice = new UniTaskCompletionSource<OutOfLivesChoice>();
                ui.SetData(new OutOfLivesPresentation(_progress.LivesRefillCoinCost,
                    _tuning.LivesRefillCoinAmount, _tuning.LivesRefillAdAmount,
                    () => choice.TrySetResult(OutOfLivesChoice.Coins),
                    () => choice.TrySetResult(OutOfLivesChoice.Ad),
                    () => choice.TrySetResult(OutOfLivesChoice.Close)));
                ui.SetLabel("@LifePopupCard/Header/Title", _loc.Get(LocKeys.LivesOutTitle));
                ui.SetLabel("@LifePopupCard/Description", _loc.Get(LocKeys.LivesOutHint));
                ui.SetLabel("@LifePopupCard/HeartImage/Text (TMP)", _loc.Get(LocKeys.LivesCount, _progress.Lives(_clock.UtcNow)));
                ui.SetLabel("_coinPriceText", _loc.Get(LocKeys.BoosterPrice, _progress.LivesRefillCoinCost));
                ui.SetLabel("_coinLivesText", _loc.Get(LocKeys.LivesOutAmount, _tuning.LivesRefillCoinAmount));
                ui.SetLabel("_adLivesText", _loc.Get(LocKeys.LivesOutAmount, _tuning.LivesRefillAdAmount));
                ui.SetAd("_freeLifeButton", adReady);
                ui.Show();
                WaitForLife(choice, ticking.Token).Forget();
                WatchAd(ui, adReady, ticking.Token).Forget();
                var result = await choice.Task.AttachExternalCancellation(ct);
                ticking.Cancel();
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(ct);
                return result;
            }
            finally
            {
                ticking.Cancel();
                _activeChoice = null;
                if (ui != null) ui.Animation?.Stop();
                if (instance != null) UnityEngine.Object.Destroy(instance);
                _assets.Release(prefab);
            }
        }

        // QA LV-06: the fill read before opening is often a unit still reloading after the last RV. The Free button follows it
        // while the popup is up — grey "No ads available", lit as soon as an ad loads (as Play on and Level Up, D-139)
        private const double AdRecheckSeconds = 0.5;

        private async UniTask WatchAd(OutOfLivesUI ui, bool adReady, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (await UniTask.Delay(TimeSpan.FromSeconds(AdRecheckSeconds), ignoreTimeScale: true,
                    cancellationToken: ct).SuppressCancellationThrow()) return;
                bool ready = await _ads.IsReadyAsync(AdPlacements.RewardedLife);
                if (ct.IsCancellationRequested || ready == adReady || ui == null) continue;
                adReady = ready;
                ui.SetAd("_freeLifeButton", ready);
            }
        }

        private async UniTask WaitForLife(UniTaskCompletionSource<OutOfLivesChoice> choice, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (_progress.Lives(_clock.UtcNow) > 0) { choice.TrySetResult(OutOfLivesChoice.Refilled); return; }
                if (await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true,
                    cancellationToken: ct).SuppressCancellationThrow()) return;
            }
        }
        /// <param name="trigger">What asked for the attempt, for <c>out_of_lives_shown</c> (<see cref="TrackingNames.TapPlay"/> …).</param>
        public async UniTask<bool> EnsureAsync(string trigger, CancellationToken ct)
        {
            const string Popup = TrackingNames.OutOfLivesPopup;
            while (_progress.Lives(_clock.UtcNow) <= 0)
            {
                bool adReady = await _ads.IsReadyAsync(AdPlacements.RewardedLife);
                ct.ThrowIfCancellationRequested();   // IsReadyAsync takes no token
                int price = _progress.LivesRefillCoinCost;
                bool affordable = _progress.CanAffordLivesRefill;
                _tracking.OutOfLivesShown(trigger, (long)_progress.NextLifeIn(_clock.UtcNow).TotalSeconds, price, affordable, adReady);
                var result = await ShowUIAsync(adReady, ct);
                if (result == OutOfLivesChoice.Close) { _tracking.UiTapped(TrackingNames.Close, Popup); return false; }
                if (result == OutOfLivesChoice.Coins)
                {
                    _tracking.UiTapped(TrackingNames.BuyCoins, Popup);
                    if (!_progress.CanAffordLivesRefill) { await _shop.OpenOverlayAsync(TrackingNames.OutOfLivesRefill, ct); continue; }   // D-067
                    _progress.TryRefillLivesWithCoins(_clock.UtcNow);   // short or not saved: nothing changed, the loop offers again
                    continue;
                }
                if (result == OutOfLivesChoice.Ad)
                {
                    _tracking.UiTapped(TrackingNames.WatchAd, Popup);
                    _tracking.AdStarted("RV", TrackingNames.OutOfLivesRv);
                    var shown = await _ads.ShowAsync(AdPlacements.RewardedLife, ct);
                    if (shown == AdResult.Rewarded)
                    {
                        _tracking.AdCompleted("RV", TrackingNames.OutOfLivesRv, "{\"" + TrackingNames.Lives + "\":1}");
                        _progress.GrantLifeFromAd(_clock.UtcNow);
                    }
                    else
                    {
                        _tracking.AdFailed("RV", TrackingNames.OutOfLivesRv);
                        // Money Sort's ErrorUI: no ad could play (a skipped ad says nothing); then the offer again
                        if (shown == AdResult.Failed) await _noAds.ShowAsync(ct);
                    }
                }
                // Refilled, or the ad gave one: the loop checks again (a cancelled ad brings the dialog back)
            }
            return true;
        }
    }
}
