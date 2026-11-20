using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Domain;

namespace Game.Application
{
    /// <summary>Wallet resources of the SKU (GDD v2.0 §7): coins are the only soft currency.</summary>
    public static class SqadSortResources
    {
        public static readonly ResourceKey Coin = new ResourceKey("coin");
    }

    // The three boosters are Game.Application.Booster (LevelSession.cs): Undo, Shuffle, AddContainer — the bar order on screen.

    /// <summary>The elements of the SqadSort GDD §2.2b (11: Hidden, 12: House), in unlock order.</summary>
    public enum ElementId { Hidden = 0, House = 1 }

    /// <summary>Level progress, FTUE and ad pacing (GDD §5, v1.2). Saved through the framework's
    /// <c>IUserData</c> envelope (loaded by <c>JsonConvert.PopulateObject</c> into this instance).</summary>
    /// <remarks>
    /// <b>Changing the shape.</b> A new field with a safe default needs no migrator: an older save simply lacks it and keeps
    /// the default (the framework's WalletModel precedent; <c>SaveSchema.CurrentVersion</c> stays 1). Add a fixture to
    /// <c>SkuHeadlessTests/Meta/SaveFixtureTests</c> when a default is not obviously right. Renaming, removing or re-meaning a
    /// field DOES need a migrator — that bumps the framework's <c>SaveSchema.CurrentVersion</c>, a spine change routed to
    /// the framework repo (rule #2: append a migrator + fixture, never edit a shipped one). The fixtures go red first.
    /// </remarks>
    public sealed class ProgressModel : IUserModel
    {
        public string Key => "sqadsort.progress";
        public int CurrentLevel = 1;
        /// <summary>Highest level won (CR-002); a win at or below it is a replay.</summary>
        public int HighestCleared;
        /// <summary>Starts of the current level since its last win (CR-002, analytics <c>attempt_no</c>).</summary>
        public int Attempts;
        public List<string> FtueCompleted = new List<string>();
        public int WinsSinceInterstitial;
        /// <summary>CR-012 C1: the starting coins were granted (once per install).</summary>
        public bool StartCoinsGranted;
        /// <summary>CR-012 C2: boosters owned (features/boosters.md v2) and the ones unlocked so far (gift given).</summary>
        public int BoosterUndo, BoosterShuffle, BoosterAddContainer;
        public List<string> BoostersUnlocked = new List<string>();
        /// <summary>CR-012 D: elements introduced so far (popup shown, or caught up by an older save).</summary>
        public List<string> ElementsUnlocked = new List<string>();
        /// <summary>CR-012 E (features/lives.md): lives left, and when the next one started counting (Unix seconds UTC, 0 = full, not counting).
        /// A save from before E has neither and reads as full.</summary>
        public int Lives = 5;
        public long LivesRefillFromUtc;
        /// <summary>D-056: the rating prompt was offered (once per install; saved before it shows).</summary>
        public bool RatingPromptShown;
        /// <summary>D-061 (analytics bookkeeping): the attempt open right now — level, launch, taps; 0 = none. One still set at
        /// launch is an attempt the app was killed in, reported then as <c>level_abandoned</c>.</summary>
        public int TrackedAttemptLevel, TrackedAttemptLaunch, TrackedAttemptCounter;
        /// <summary>D-061: the install's first loading flow was reported (once).</summary>
        public bool FirstLoadingTracked;
        /// <summary>D-097 (Money Sort's user properties): the install's analytics user id (a GUID, made once; "" = not yet) and the
        /// UTC day it was made (yyyymmdd, 0 = not yet) that <c>retained_days</c> counts from. An older save has neither: the first
        /// session after the update counts as day 0, as Money Sort does for a player who updated into the feature.</summary>
        public string AnalyticsUserId = "";
        public int AnalyticsInstallDayUtc;
        /// <summary>D-064 Skip Level: losses in a row on <see cref="SkipFailLevel"/> (a win, a skip or a loss elsewhere restarts it), and
        /// the skips used on <see cref="SkipDay"/> (local date as yyyymmdd; another day ⇒ none used yet).</summary>
        public int SkipFailLevel, SkipFailCount, SkipDay, SkipsToday;
        public int RestartBoosterOfferedLevel;
        public int RetryBoosterDay, RetryBoostersToday;
        /// <summary>D-067 Shop (features/shop.md): one-time packs bought (ids of <c>ShopCatalog</c>), unlimited lives until (Unix seconds
        /// UTC, 0 = none), and when the Starter Pack window opened (0 = not yet).</summary>
        public List<string> PacksBought = new List<string>();
        public long UnlimitedLivesUntilUtc, StarterOfferFromUtc;
        /// <summary>D-069 Remove Ads popup schedule (OfferSchedule): popups shown so far, interstitials since the last one (counted
        /// only after the first), when it last showed, and the 24-hour window of shows 5+ (Unix seconds UTC, 0 = none).</summary>
        public int RemoveAdsPopupShown, RemoveAdsInterstitials, RemoveAdsShownInDay;
        public long RemoveAdsLastShownUtc, RemoveAdsDayStartUtc;
        /// <summary>D-070 Gold Brick Package!: the current window ends (0 = none or claimed), the next gift may start (Unix seconds
        /// UTC), and whether the popup was on screen in this window.</summary>
        public long GoldBrickActiveEndsUtc, GoldBrickNextAvailableUtc;
        public bool GoldBrickShown;
        /// <summary>D-138: level play attempts in a row that showed the low-slot bubble and opened no RV Slot.</summary>
        public int RvNudgeIgnoreStreak;

        public ProgressModel Clone() => new ProgressModel
        {
            CurrentLevel = CurrentLevel, HighestCleared = HighestCleared, Attempts = Attempts,
            FtueCompleted = new List<string>(FtueCompleted),
            WinsSinceInterstitial = WinsSinceInterstitial, StartCoinsGranted = StartCoinsGranted,
            BoosterUndo = BoosterUndo, BoosterShuffle = BoosterShuffle, BoosterAddContainer = BoosterAddContainer,
            BoostersUnlocked = new List<string>(BoostersUnlocked),
            ElementsUnlocked = new List<string>(ElementsUnlocked),
            Lives = Lives, LivesRefillFromUtc = LivesRefillFromUtc,
            RatingPromptShown = RatingPromptShown,
            TrackedAttemptLevel = TrackedAttemptLevel, TrackedAttemptLaunch = TrackedAttemptLaunch, TrackedAttemptCounter = TrackedAttemptCounter,
            FirstLoadingTracked = FirstLoadingTracked,
            AnalyticsUserId = AnalyticsUserId, AnalyticsInstallDayUtc = AnalyticsInstallDayUtc,
            RestartBoosterOfferedLevel = RestartBoosterOfferedLevel,
            RetryBoosterDay = RetryBoosterDay, RetryBoostersToday = RetryBoostersToday,
            SkipFailLevel = SkipFailLevel, SkipFailCount = SkipFailCount, SkipDay = SkipDay, SkipsToday = SkipsToday,
            PacksBought = new List<string>(PacksBought), UnlimitedLivesUntilUtc = UnlimitedLivesUntilUtc, StarterOfferFromUtc = StarterOfferFromUtc,
            RemoveAdsPopupShown = RemoveAdsPopupShown, RemoveAdsInterstitials = RemoveAdsInterstitials, RemoveAdsShownInDay = RemoveAdsShownInDay,
            RemoveAdsLastShownUtc = RemoveAdsLastShownUtc, RemoveAdsDayStartUtc = RemoveAdsDayStartUtc,
            GoldBrickActiveEndsUtc = GoldBrickActiveEndsUtc, GoldBrickNextAvailableUtc = GoldBrickNextAvailableUtc, GoldBrickShown = GoldBrickShown,
            RvNudgeIgnoreStreak = RvNudgeIgnoreStreak,
        };

        public void CopyFrom(ProgressModel o)
        {
            CurrentLevel = o.CurrentLevel; HighestCleared = o.HighestCleared; Attempts = o.Attempts;
            FtueCompleted = new List<string>(o.FtueCompleted);
            WinsSinceInterstitial = o.WinsSinceInterstitial; StartCoinsGranted = o.StartCoinsGranted;
            BoosterUndo = o.BoosterUndo; BoosterShuffle = o.BoosterShuffle; BoosterAddContainer = o.BoosterAddContainer;
            BoostersUnlocked = new List<string>(o.BoostersUnlocked);
            ElementsUnlocked = new List<string>(o.ElementsUnlocked);
            Lives = o.Lives; LivesRefillFromUtc = o.LivesRefillFromUtc;
            RatingPromptShown = o.RatingPromptShown;
            TrackedAttemptLevel = o.TrackedAttemptLevel; TrackedAttemptLaunch = o.TrackedAttemptLaunch; TrackedAttemptCounter = o.TrackedAttemptCounter;
            FirstLoadingTracked = o.FirstLoadingTracked;
            AnalyticsUserId = o.AnalyticsUserId; AnalyticsInstallDayUtc = o.AnalyticsInstallDayUtc;
            RestartBoosterOfferedLevel = o.RestartBoosterOfferedLevel;
            RetryBoosterDay = o.RetryBoosterDay; RetryBoostersToday = o.RetryBoostersToday;
            SkipFailLevel = o.SkipFailLevel; SkipFailCount = o.SkipFailCount; SkipDay = o.SkipDay; SkipsToday = o.SkipsToday;
            PacksBought = new List<string>(o.PacksBought); UnlimitedLivesUntilUtc = o.UnlimitedLivesUntilUtc; StarterOfferFromUtc = o.StarterOfferFromUtc;
            RemoveAdsPopupShown = o.RemoveAdsPopupShown; RemoveAdsInterstitials = o.RemoveAdsInterstitials; RemoveAdsShownInDay = o.RemoveAdsShownInDay;
            RemoveAdsLastShownUtc = o.RemoveAdsLastShownUtc; RemoveAdsDayStartUtc = o.RemoveAdsDayStartUtc;
            GoldBrickActiveEndsUtc = o.GoldBrickActiveEndsUtc; GoldBrickNextAvailableUtc = o.GoldBrickNextAvailableUtc; GoldBrickShown = o.GoldBrickShown;
            RvNudgeIgnoreStreak = o.RvNudgeIgnoreStreak;
        }
    }

    /// <summary>Player settings (features/pause-settings.md).</summary>
    public sealed class SettingsModel : IUserModel
    {
        public string Key => "sqadsort.settings";
        public bool Sound = true;
        public bool Music = true;
        public bool Haptics = true;
    }

    /// <summary>
    /// The narrow save port the meta services use. The Infrastructure adapter sits on the framework's
    /// <c>IUserData</c>; tests use an in-memory store that can be told to fail (G18).
    /// </summary>
    public interface IProgressStore
    {
        ProgressModel Progress { get; }
        SettingsModel Settings { get; }
        /// <summary>Persist. May throw on a storage failure; callers roll back in memory.</summary>
        void Save();
    }
}
