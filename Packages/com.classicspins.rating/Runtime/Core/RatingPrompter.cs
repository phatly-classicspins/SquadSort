using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassicSpins.Rating
{
    /// <summary>
    /// The rating flow of the Money Sort prototype, game-independent and engine-free:
    /// <list type="number">
    /// <item>after a win at <see cref="RatingSettings.PromptAfterLevel"/> or later, once per install, record the offer
    /// (before showing: quitting mid-popup never brings it back) and show the star question;</item>
    /// <item>closed ⇒ <see cref="RatingResult.Closed"/> (the game decides what follows — Money Sort plays its interstitial);</item>
    /// <item>stars ≥ <see cref="RatingSettings.StoreMinStars"/> ⇒ the store's review page, fewer ⇒ the feedback page;</item>
    /// <item>when the player comes back (the game lost focus and got it again) ⇒ the thank-you, then
    /// <see cref="RatingResult.Rated"/>. No page to open, or the game never lost focus within
    /// <see cref="RatingSettings.LeaveTimeoutSeconds"/> ⇒ the thank-you right away.</item>
    /// </list>
    /// The engine side forwards focus with <see cref="OnAppFocusChanged"/>. One prompt runs at a time; call from the main thread.
    /// </summary>
    public sealed class RatingPrompter
    {
        public const int MaxStars = 5;

        private readonly RatingSettings _settings;
        private readonly IRatingState _state;
        private readonly IRatingPopup _popup;
        private readonly IRatingHost _host;
        private TaskCompletionSource<bool> _left, _back;
        private bool _running;

        public RatingPrompter(RatingSettings settings, IRatingState state, IRatingPopup popup, IRatingHost host)
        {
            _settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Sanitized();
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _popup = popup ?? throw new ArgumentNullException(nameof(popup));
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        /// <summary>Diagnostics (what was decided and why). Optional.</summary>
        public Action<string> Log { get; set; }

        public RatingSettings Settings => _settings;

        /// <summary>A prompt is up or waiting for the player to come back.</summary>
        public bool IsRunning => _running;

        /// <summary>Would a win of <paramref name="completedLevel"/> offer the prompt now?</summary>
        public bool ShouldPrompt(int completedLevel) =>
            _settings.Enabled && completedLevel >= _settings.PromptAfterLevel && !_state.PromptShown && !_running;

        /// <summary>The page a rating of <paramref name="stars"/> opens, or "" when there is none (thank-you only).</summary>
        public string UrlFor(int stars)
        {
            if (stars >= _settings.StoreMinStars)
            {
                string pkg = _settings.AndroidPackage.Length > 0 ? _settings.AndroidPackage : _host.AppIdentifier;
                return RatingStoreLinks.WriteReview(_host.Platform, _settings.IosAppId, pkg, _host.NativeStoreLinks);
            }
            return _settings.FeedbackUrl;
        }

        /// <summary>Offer the prompt if a win of <paramref name="completedLevel"/> earns it, and run it to the end.
        /// Cancelling <paramref name="ct"/> (the screen went away) takes the popup down and throws
        /// <see cref="OperationCanceledException"/>; the offer stays recorded.</summary>
        public async Task<RatingOutcome> TryPromptAsync(int completedLevel, CancellationToken ct)
        {
            if (!ShouldPrompt(completedLevel)) return new RatingOutcome(RatingResult.NotShown, 0, "");
            if (!_state.MarkPromptShown())
            {
                Log?.Invoke("[Rating] the offer could not be saved; skipping the prompt.");
                return new RatingOutcome(RatingResult.NotShown, 0, "");
            }
            _running = true;
            try
            {
                Log?.Invoke($"[Rating] prompt offered after level {completedLevel}.");
                int stars = await _popup.AskAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (stars <= 0)
                {
                    Log?.Invoke("[Rating] closed without rating.");
                    return new RatingOutcome(RatingResult.Closed, 0, "");
                }
                stars = Math.Min(MaxStars, stars);
                bool positive = stars >= _settings.StoreMinStars;
                string url = UrlFor(stars);
                Log?.Invoke($"[Rating] {stars} star(s) → {(url.Length > 0 ? url : "no page")}.");
                if (url.Length > 0) await OpenAndWaitForReturn(url, ct);
                await _popup.ThanksAsync(positive, ct);
                return new RatingOutcome(RatingResult.Rated, stars, url);
            }
            finally
            {
                _running = false;
                _left = _back = null;
            }
        }

        /// <summary>The game lost (<c>false</c>) or regained (<c>true</c>) focus. Forward every change; a pause is a focus loss.</summary>
        public void OnAppFocusChanged(bool focused)
        {
            if (!focused) _left?.TrySetResult(true);
            else if (_left != null && _left.Task.IsCompleted) _back?.TrySetResult(true);
        }

        /// <summary>Convenience for a pause callback: paused ⇒ focus lost, resumed ⇒ focus back.</summary>
        public void OnAppPaused(bool paused) => OnAppFocusChanged(!paused);

        private async Task OpenAndWaitForReturn(string url, CancellationToken ct)
        {
            var left = _left = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var back = _back = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (ct.Register(() => { left.TrySetCanceled(); back.TrySetCanceled(); }))
            {
                try { _host.OpenUrl(url); }
                catch (Exception e)
                {
                    Log?.Invoke($"[Rating] opening the page failed ({e.Message}); thank-you right away.");
                    return;
                }
                var timeout = _host.Delay(TimeSpan.FromSeconds(_settings.LeaveTimeoutSeconds), ct);
                var first = await Task.WhenAny(left.Task, timeout);
                ct.ThrowIfCancellationRequested();
                if (first != left.Task)
                {
                    Log?.Invoke("[Rating] the game never lost focus after opening the page; thank-you right away.");
                    return;
                }
                await back.Task;   // throws when cancelled
            }
        }
    }
}
