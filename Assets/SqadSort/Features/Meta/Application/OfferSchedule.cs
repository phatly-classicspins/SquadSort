using System;

namespace Game.Application
{
    /// <summary>The offer popup a level complete screen (or an app start) shows (D-069, D-070): at most one.</summary>
    public enum LevelCompleteOffer { None, Starter, RemoveAds, GoldBrick }

    /// <summary>
    /// D-069 (task Asana "Update the new Popup logic & icon", Phat 02-11: on the level complete screen, as Money Sort): when the IAP
    /// offer popups show — Money Sort's <c>TryShowLevelCompleteOffer</c> / <c>TryShowSessionStartOffer</c> and the GDD Block Sort
    /// popup list (6.3, 6.4, 7.2).
    /// <list type="bullet">
    /// <item>Level complete: at most one, Starter Pack &gt; Remove Ads. The Starter Pack's first popup is the level complete screen
    /// of <see cref="EconomyTuning.StarterPackUnlockLevel"/> or later and opens its window; after that it shows at each app start
    /// while the window lasts, never on a level complete again.</item>
    /// <item>Remove Ads: only on a level complete that followed an interstitial, from <see cref="EconomyTuning.RemoveAdsUnlockLevel"/>.
    /// Show 1 at the first such; shows 2–4 every <see cref="EarlyInterstitials"/> interstitials and <see cref="EarlyWaitMinutes"/>
    /// minutes apart (or in a new session); from show 5 every <see cref="LateInterstitials"/>, at most <see cref="LateMaxPerDay"/> per
    /// 24 hours. Interstitials are counted only once the first popup showed.</item>
    /// </list>
    /// The counters live in the save; whether Remove Ads showed this session is this instance's (Root: one per app run).
    /// </summary>
    public sealed class OfferSchedule
    {
        public const int EarlyInterstitials = 4, EarlyWaitMinutes = 20, LateInterstitials = 8, LateMaxPerDay = 2;
        private const long SecondsPerDay = 86400;

        private readonly IProgressStore _store;
        private readonly EconomyTuning _tuning;
        private bool _removeAdsShownThisSession;

        public OfferSchedule(IProgressStore store, EconomyTuning tuning) { _store = store; _tuning = tuning; }

        /// <summary>The level complete screen of <paramref name="levelJustWon"/>: which offer to show now, recorded as shown (the
        /// Starter Pack window opens; the Remove Ads cycle restarts; a new Gold Brick window opens). <paramref name="removeAdsOwned"/>:
        /// the entitlement.</summary>
        public LevelCompleteOffer AtLevelComplete(int levelJustWon, bool afterInterstitial, bool removeAdsOwned, DateTimeOffset now)
        {
            var p = _store.Progress;
            long t = now.ToUnixTimeSeconds();
            if (p.StarterOfferFromUtc == 0 && !p.PacksBought.Contains(ShopCatalog.StarterId) && levelJustWon >= _tuning.StarterPackUnlockLevel
                && Save(m => m.StarterOfferFromUtc = t))
                return LevelCompleteOffer.Starter;
            if (afterInterstitial && !removeAdsOwned && levelJustWon >= _tuning.RemoveAdsUnlockLevel && TryRemoveAds(t)) return LevelCompleteOffer.RemoveAds;
            return TryGoldBrick(levelJustWon, t) ? LevelCompleteOffer.GoldBrick : LevelCompleteOffer.None;
        }

        /// <summary>App start (GDD 7.2 "new session": Starter Pack &gt; Gold Brick Package!): the offer to show on Home, if any.
        /// <paramref name="levelsCleared"/>: the highest level won so far.</summary>
        public LevelCompleteOffer AtSessionStart(LevelProgressService progress, int levelsCleared, DateTimeOffset now)
        {
            if (progress.StarterOfferLeft(now) > TimeSpan.Zero) return LevelCompleteOffer.Starter;
            return TryGoldBrick(levelsCleared, now.ToUnixTimeSeconds()) ? LevelCompleteOffer.GoldBrick : LevelCompleteOffer.None;
        }

        private bool TryRemoveAds(long t)
        {
            var p = _store.Progress;
            if (p.RemoveAdsDayStartUtc > 0 && t - p.RemoveAdsDayStartUtc >= SecondsPerDay) { p.RemoveAdsDayStartUtc = 0; p.RemoveAdsShownInDay = 0; }
            if (!RemoveAdsDue(t)) return false;
            bool late = p.RemoveAdsPopupShown + 1 > 4;
            if (!Save(m =>
                {
                    m.RemoveAdsPopupShown++;
                    m.RemoveAdsInterstitials = 0;
                    m.RemoveAdsLastShownUtc = t;
                    if (late)
                    {
                        if (m.RemoveAdsDayStartUtc <= 0) m.RemoveAdsDayStartUtc = t;
                        m.RemoveAdsShownInDay++;
                    }
                })) return false;
            _removeAdsShownThisSession = true;
            return true;
        }

        // ── Gold Brick Package! (D-070, GDD Block Sort 7.3, Money Sort GarageGift) ───────────────────────────────────────────────
        // Cooldown ⇒ waiting (from the unlock level, once the cooldown is over) ⇒ shown: a window of GoldBrickActiveMinutes opens with
        // the popup; claimed (a rewarded ad) ⇒ the cooldown restarts now; not claimed ⇒ the Home badge keeps it until the window ends,
        // then the cooldown runs from the window's end. One popup per window: after that it reopens only from the badge.

        private bool TryGoldBrick(int level, long t)
        {
            var p = _store.Progress;
            if (p.GoldBrickActiveEndsUtc > t) return !p.GoldBrickShown && Save(m => m.GoldBrickShown = true);
            if (level < _tuning.GoldBrickUnlockLevel || t < p.GoldBrickNextAvailableUtc) return false;
            long ends = t + _tuning.GoldBrickActiveMinutes * 60L;
            return Save(m =>
            {
                m.GoldBrickActiveEndsUtc = ends;
                m.GoldBrickNextAvailableUtc = ends + _tuning.GoldBrickCooldownHours * 3600L;
                m.GoldBrickShown = true;
            });
        }

        /// <summary>Time left to claim the current gift; zero when none is open.</summary>
        public TimeSpan GoldBrickLeft(DateTimeOffset now) =>
            TimeSpan.FromSeconds(Math.Max(0, _store.Progress.GoldBrickActiveEndsUtc - now.ToUnixTimeSeconds()));

        /// <summary>The gift's rewarded ad completed (G19): coins + Hand boosters granted in one save, the cooldown restarts now. False —
        /// nothing granted — when no gift is open or saving failed.</summary>
        /// <param name="adStartedInWindow">The player pressed Get Reward while the window was open: the gift is theirs even if the
        /// window ran out (or was reported expired) while the ad played (QA RV-07, G19 — a watched ad always pays).</param>
        public bool ClaimGoldBrick(LevelProgressService progress, DateTimeOffset now, bool adStartedInWindow = false)
        {
            if (!adStartedInWindow && GoldBrickLeft(now) <= TimeSpan.Zero) return false;
            if (!progress.GrantGift(_tuning.GoldBrickCoins, Booster.Undo, GoldBrickBoosters)) return false;
            long t = now.ToUnixTimeSeconds();
            Save(m => { m.GoldBrickActiveEndsUtc = 0; m.GoldBrickNextAvailableUtc = t + _tuning.GoldBrickCooldownHours * 3600L; });
            return true;
        }

        /// <summary>D-071 (Money Sort CheckGarageGiftExpired): a window that ran out unclaimed, reported once — true and whether its popup
        /// had been on screen; the window is then cleared (the cooldown already runs from its end).</summary>
        public bool TakeExpiredGoldBrick(DateTimeOffset now, out bool wasShown)
        {
            var p = _store.Progress;
            wasShown = p.GoldBrickShown;
            if (p.GoldBrickActiveEndsUtc <= 0 || now.ToUnixTimeSeconds() < p.GoldBrickActiveEndsUtc) return false;
            return Save(m => { m.GoldBrickActiveEndsUtc = 0; m.GoldBrickShown = false; });
        }

        /// <summary>Boosters in the gift (GDD: "200 + 1"; Money Sort gives Free Move, CardSlot's nearest is Hand).</summary>
        public const int GoldBrickBoosters = 1;

        /// <summary>An interstitial played; it counts toward the next Remove Ads popup once the first one showed.</summary>
        public void InterstitialShown()
        {
            if (_store.Progress.RemoveAdsPopupShown <= 0) return;
            Save(m => m.RemoveAdsInterstitials++);
        }

        /// <summary>App start: the Starter Pack popup shows while its window is open and it is not bought.</summary>
        public bool StarterAtSessionStart(LevelProgressService progress, DateTimeOffset now) => progress.StarterOfferLeft(now) > TimeSpan.Zero;

        /// <summary>The Remove Ads icon on Home: after the first popup, until bought.</summary>
        public bool RemoveAdsIconVisible(bool removeAdsOwned) => !removeAdsOwned && _store.Progress.RemoveAdsPopupShown > 0;

        private bool RemoveAdsDue(long t)
        {
            var p = _store.Progress;
            if (p.RemoveAdsPopupShown <= 0) return true;
            if (p.RemoveAdsPopupShown < 4)
                return p.RemoveAdsInterstitials >= EarlyInterstitials &&
                       (!_removeAdsShownThisSession || t - p.RemoveAdsLastShownUtc >= EarlyWaitMinutes * 60L);
            return p.RemoveAdsInterstitials >= LateInterstitials && p.RemoveAdsShownInDay < LateMaxPerDay;
        }

        // change and persist, or restore (G18)
        private bool Save(Action<ProgressModel> change)
        {
            var before = _store.Progress.Clone();
            try { change(_store.Progress); _store.Save(); return true; }
            catch (Exception) { _store.Progress.CopyFrom(before); return false; }
        }
    }
}
