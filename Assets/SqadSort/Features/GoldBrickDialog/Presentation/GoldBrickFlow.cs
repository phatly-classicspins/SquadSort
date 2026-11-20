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
    /// <summary>D-070: <c>Watch</c> = "Get Reward" (the caller plays the rewarded ad and grants the gift); <c>Decline</c> = No thanks,
    /// ✕, Back, or the window running out.</summary>
    public enum GoldBrickChoice { Decline, Watch }

    /// <summary>
    /// D-070 (Money Sort WatchGarageGiftAd): the Gold Brick Package! — Money Sort's GarageGiftUI opened directly (07-11-2026) — through
    /// to the gift. The popup counts down once a second and closes by itself when the window ends. "Get Reward" plays the rewarded ad;
    /// only an ad that rewarded grants (G19) — 200 coins + 1 Hand, the cooldown restarting — else the popup comes back while the window
    /// lasts. Declined, the gift waits behind the Home badge until the window ends. Root: opened on a level complete screen and on Home.
    /// </summary>
    public sealed class GoldBrickFlow
    {
        private readonly PopupUi _popups;
        private readonly NoAdsPopup _noAds;
        private readonly IAdsService _ads;
        private readonly OfferSchedule _offers;
        private readonly LevelProgressService _progress;
        private readonly AdPacing _adPacing;
        private readonly IGameTracking _tracking;
        private readonly GameFeedback _feedback;
        private readonly EconomyTuning _tuning;
        private readonly IClock _clock;
        private readonly ILocalizationService _loc;

        public GoldBrickFlow(PopupUi popups, NoAdsPopup noAds, IAdsService ads, OfferSchedule offers, LevelProgressService progress,
            AdPacing adPacing, IGameTracking tracking, GameFeedback feedback, EconomyTuning tuning, IClock clock, ILocalizationService loc)
        {
            _popups = popups; _noAds = noAds; _ads = ads; _offers = offers; _progress = progress; _adPacing = adPacing; _tracking = tracking;
            _feedback = feedback; _tuning = tuning; _clock = clock; _loc = loc;
        }

        private UniTask<GoldBrickChoice> ShowUIAsync(CancellationToken ct) =>
            _popups.ShowAsync<RewardedAdPromptUI, GoldBrickChoice>(AssetKeys.UI.GarageGiftUI, () => GoldBrickChoice.Decline, (ui, h) =>
            {
                ui.SetData(new RewardedAdPromptOffer(() => h.Close(GoldBrickChoice.Watch), () => h.Close(GoldBrickChoice.Decline),
                    rewardLabels: new[] { _loc.Get(LocKeys.ShopBooster, OfferSchedule.GoldBrickBoosters), _loc.Get(LocKeys.HudCoins, _tuning.GoldBrickCoins) }));
                ui.SetLabel("@PromptCard/Description", _loc.Get(LocKeys.GoldbrickDesc));
                ui.SetButtonLabel("_noThanksButton", _loc.Get(LocKeys.GoldbrickNo));
                ui.SetButtonLabel("_watchAdButton", _loc.Get(LocKeys.GoldbrickGet));
                void Timer()
                {
                    var left = _offers.GoldBrickLeft(_clock.UtcNow);
                    if (left <= TimeSpan.Zero) { h.Close(GoldBrickChoice.Decline); return; }
                    ui.SetLabel("_timerText", _loc.Get(LocKeys.GoldbrickTimer, (int)left.TotalMinutes, left.Seconds));
                }
                Timer();
                PopupUi.EverySecond(h.Token, Timer).Forget();
            }, ct);

        /// <summary>True when the gift was claimed.</summary>
        public async UniTask<bool> ShowAsync(CancellationToken ct)
        {
            while (_offers.GoldBrickLeft(_clock.UtcNow) > TimeSpan.Zero)
            {
                _tracking.RvPopupShown(TrackingNames.GoldBrickRv, 0, await _ads.IsReadyAsync(AdPlacements.RewardedGoldBrick));   // D-071
                var choice = await ShowUIAsync(ct);
                _tracking.UiTapped(choice == GoldBrickChoice.Watch ? TrackingNames.WatchAd : TrackingNames.Close, TrackingNames.GoldBrickRv + "_popup");
                if (choice == GoldBrickChoice.Decline) return false;
                if (!await _ads.IsReadyAsync(AdPlacements.RewardedGoldBrick))   // no ad yet: say so (Money Sort's ErrorUI), then the popup again
                {
                    ct.ThrowIfCancellationRequested();
                    await _noAds.ShowAsync(ct);
                    continue;
                }
                ct.ThrowIfCancellationRequested();
                // QA RV-07: the window may run out while the ad plays; what counts is that Get Reward was pressed inside it
                bool startedInWindow = _offers.GoldBrickLeft(_clock.UtcNow) > TimeSpan.Zero;
                if (!startedInWindow) return false;
                _tracking.AdStarted("RV", TrackingNames.GoldBrickRv);
                var shown = await _ads.ShowAsync(AdPlacements.RewardedGoldBrick, ct);
                if (shown != AdResult.Rewarded)
                {
                    _tracking.AdFailed("RV", TrackingNames.GoldBrickRv);
                    if (shown == AdResult.Failed) await _noAds.ShowAsync(ct);
                    continue;
                }
                _tracking.AdCompleted("RV", TrackingNames.GoldBrickRv, "{\"" + TrackingNames.Coins + "\":" + _tuning.GoldBrickCoins + ",\"" +
                    TrackingNames.Booster(Booster.Undo) + "\":" + OfferSchedule.GoldBrickBoosters + "}");
                if (!_offers.ClaimGoldBrick(_progress, _clock.UtcNow, adStartedInWindow: true)) return false;
                _feedback.Coins();
                return true;
            }
            return false;
        }
    }
}
