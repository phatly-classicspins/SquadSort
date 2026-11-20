using System;

namespace Game.Application
{
    /// <summary>Interstitial pacing (features/ads.md). Pure rule plus the save bookkeeping. Session-long (Root), so the
    /// time of the last ad survives a trip Home and back.</summary>
    public sealed class AdPacing
    {
        private readonly IProgressStore _store;
        private readonly EconomyTuning _tuning;
        private DateTimeOffset? _lastInterstitialUtc;   // the last interstitial shown this session; none yet = no wait

        public AdPacing(IProgressStore store, EconomyTuning tuning) { _store = store; _tuning = tuning; }

        /// <summary>Show on a level end, before the level up screen (Money Sort), when all three hold: level ≥ first level, enough wins
        /// since the last interstitial, and enough seconds since the last full-screen ad (D-131, as Blockery / Money Sort
        /// a4464bc: a rewarded ad that was on screen restarts the break too, so two full-screen ads never run back to back).</summary>
        public static bool ShouldShow(int levelJustWon, int winsSinceInterstitial, double secondsSinceLastInterstitial, EconomyTuning t) =>
            levelJustWon >= t.InterstitialFirstLevel
            && winsSinceInterstitial >= t.InterstitialEveryNWins
            && secondsSinceLastInterstitial >= t.InterstitialMinIntervalSeconds;

        public bool ShouldShowAfterWin(int levelJustWon, DateTimeOffset now) =>
            ShouldShow(levelJustWon, _store.Progress.WinsSinceInterstitial, SecondsSinceLastInterstitial(now), _tuning);

        /// <summary>D-131 (Blockery / Money Sort a4464bc): the lose interstitial — after the revive offer was declined, on Retry
        /// or Home — has the level complete one's rules without the win count: level ≥ first level and the break since the
        /// last full-screen ad.</summary>
        public static bool ShouldShowOnLose(int levelLost, double secondsSinceLastInterstitial, EconomyTuning t) =>
            levelLost >= t.InterstitialFirstLevel && secondsSinceLastInterstitial >= t.InterstitialMinIntervalSeconds;

        public bool ShouldShowAfterLose(int levelLost, DateTimeOffset now) => ShouldShowOnLose(levelLost, SecondsSinceLastInterstitial(now), _tuning);

        /// <summary>D-131: a rewarded ad that was on screen just closed — the interstitial break restarts from now (the win count
        /// is the interstitial's own and stays).</summary>
        public void RecordRewardedClosed(DateTimeOffset now) => _lastInterstitialUtc = now;

        /// <summary>Seconds since the last interstitial this session; <see cref="double.MaxValue"/> before the first one,
        /// and a clock that went back counts as no time passed.</summary>
        public double SecondsSinceLastInterstitial(DateTimeOffset now) =>
            _lastInterstitialUtc is { } last ? Math.Max(0, (now - last).TotalSeconds) : double.MaxValue;

        /// <summary>Record a shown interstitial. Ad pacing is not worth failing a flow over: a failed save
        /// just means the counter resets on the next successful one.</summary>
        public void RecordInterstitialShown(DateTimeOffset now)
        {
            _lastInterstitialUtc = now;
            _store.Progress.WinsSinceInterstitial = 0;
            try { _store.Save(); } catch (Exception) { /* see summary */ }
        }
    }
}
