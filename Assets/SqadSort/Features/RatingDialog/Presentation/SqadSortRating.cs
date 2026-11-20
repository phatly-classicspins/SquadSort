using System;
using System.Threading;
using System.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Presentation;
using ClassicSpins.Rating;
using Cysharp.Threading.Tasks;
using Game.Application;
using Game.Gen;

namespace Game.Presentation
{
    /// <summary>
    /// D-056: Money Sort's rating prompt in SqadSort — the flow is com.classicspins.rating's <see cref="RatingPrompter"/>;
    /// this class plugs in what SqadSort owns: the popup is Money Sort's RatingUI opened directly (07-11-2026, LocKeys),
    /// the once-per-install flag lives in the save (<see cref="LevelProgressService.MarkRatingPromptShown"/>, G18), the
    /// numbers are remote config (<see cref="EconomyTuning"/>), and focus comes from the kernel's lifecycle hook through
    /// <see cref="RatingFocus"/> (a second <c>OnApplicationPause</c> is banned). One per Gameplay scope.
    /// </summary>
    public sealed class SqadSortRating : IRatingPopup, IRatingState, IDisposable
    {
        private readonly PopupUi _popups;
        private readonly ILocalizationService _loc;
        private readonly LevelProgressService _progress;
        private readonly RatingFocus _focus;
        private readonly RatingPrompter _prompter;
        private readonly IGameTracking _tracking;   // D-061: Money Sort's meta_started / meta_completed "rating"
        private int _level;

        public SqadSortRating(PopupUi popups, ILocalizationService loc, LevelProgressService progress, EconomyTuning tuning,
            RatingFocus focus, IRatingHost host, IGameTracking tracking, ILog log)
        {
            _popups = popups; _loc = loc; _progress = progress; _focus = focus; _tracking = tracking;
            var settings = new RatingSettings
            {
                PromptAfterLevel = tuning.RatingPromptLevel,
                StoreMinStars = tuning.RatingStoreMinStars,
                IosAppId = tuning.RatingIosAppId,
                AndroidPackage = tuning.RatingAndroidPackage,
                FeedbackUrl = tuning.RatingFeedbackUrl,
            };
            _prompter = new RatingPrompter(settings, this, this, host) { Log = m => log.Info(m) };
            _focus.Changed += _prompter.OnAppFocusChanged;
        }

        /// <summary>After a win of <paramref name="completedLevel"/>: offer the prompt if it is due and run it to the end
        /// (see <see cref="RatingResult"/> for what the caller does next).</summary>
        public UniTask<RatingOutcome> TryPromptAsync(int completedLevel, CancellationToken ct)
        {
            _level = completedLevel;
            return _prompter.TryPromptAsync(completedLevel, ct).AsUniTask();
        }

        public void Dispose() => _focus.Changed -= _prompter.OnAppFocusChanged;

        // ── IRatingState: the save ───────────────────────────────────────────────────────────────
        bool IRatingState.PromptShown => _progress.RatingPromptShown;
        bool IRatingState.MarkPromptShown() => _progress.MarkRatingPromptShown();

        // ── IRatingPopup: the dialog ─────────────────────────────────────────────────────────────
        async Task<int> IRatingPopup.AskAsync(CancellationToken ct)
        {
            string meta = "level_" + _level;
            _tracking.MetaStarted("rating", meta);
            // ✕ and Back close with 0; a cancelled screen throws OperationCanceledException out of ShowAsync
            int stars = await _popups.ShowAsync<Game.Views.RatingUI, int>(AssetKeys.UI.RatingUI, () => 0, (ui, h) =>
            {
                ui.SetData(new Game.Views.RatingOffer(picked => h.Close(picked), () => h.Close(0)));
                ui.SetLabel("_titleText", _loc.Get(LocKeys.RatingTitle));
                ui.SetButtonLabel("_submitButton", _loc.Get(LocKeys.RatingSubmit));
            }, ct);
            if (stars > 0) _tracking.MetaCompleted("rating", meta);                    // submitted, not closed
            return stars;
        }

        async Task IRatingPopup.ThanksAsync(bool positive, CancellationToken ct)
        {
            await _popups.ShowAsync<Game.Views.RatingUI, int>(AssetKeys.UI.RatingUI, () => 0, (ui, h) =>
            {
                ui.SetData(new Game.Views.RatingThankYouOffer(_loc.Get(positive ? LocKeys.RatingThanksPositive : LocKeys.RatingThanksNegative),
                    () => h.Close(0)));
                ui.SetLabel("_titleText", _loc.Get(LocKeys.RatingThanksTitle));
                ui.SetButtonLabel("_closeButton", _loc.Get(LocKeys.RatingContinue));
            }, ct);
        }
    }

    /// <summary>
    /// D-056: the app's focus as one event, fed by the kernel's single lifecycle hook (AD-16) — the rating flow waits for
    /// the player to come back from the store. Session-long (Root), registered as an <see cref="IAppPhaseHandler"/>.
    /// </summary>
    public sealed class RatingFocus : IAppPhaseHandler
    {
        private bool _focused = true;

        /// <summary>False: the app went to the background or lost focus; true: it is back. Each real change once.</summary>
        public event Action<bool> Changed;

        public AppLifecyclePhase Phase => AppLifecyclePhase.PresentationFlush;
        public void OnPause(bool paused) => Set(!paused);
        public void OnFocus(bool focused) => Set(focused);
        public void OnQuit() { }

        private void Set(bool focused)
        {
            if (focused == _focused) return;
            _focused = focused;
            Changed?.Invoke(focused);
        }
    }
}
