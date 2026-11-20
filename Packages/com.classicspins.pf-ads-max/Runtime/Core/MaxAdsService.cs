using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>
    /// The AppLovin MAX implementation of the pinned <see cref="IAdsService"/> port. Registered by
    /// <c>MaxAdsInstaller</c> after every framework installer, so it wins the single resolve over the
    /// framework's auto-rewarding fake.
    /// </summary>
    /// <remarks>
    /// <para><b>Failure is a result, never a throw.</b> Every exit path — a refusal, a bridge that
    /// throws, a display failure, a display timeout, a cancelled token — resolves to
    /// <see cref="AdResult.Failed"/> and logs a warning naming <c>pf-ads-max</c>. A cancelled token is
    /// deliberately a <c>Failed</c> result here, not a rethrow; once the fullscreen unit is up the token
    /// is ignored, because returning control to the game while an ad still covers the screen would desync
    /// every visibility consumer.</para>
    /// <para><b>Unit and format come from config, through <see cref="MaxAdUnits"/>.</b> The unit id is
    /// resolved platform → placement → format default; the format reuses the framework's own
    /// <c>ads.&lt;placement&gt;.rewarded</c> flag (non-zero ⇒ rewarded, absent ⇒ interstitial).</para>
    /// <para><b>Nothing shows from a unit nobody requested.</b> The first <c>IsReadyAsync</c> /
    /// <c>ShowAsync</c> of an unrequested unit requests it (a load starts — before init it is recorded and
    /// loaded at auto-load) and answers not-ready; a SKU that wants the first show to succeed calls
    /// <see cref="Preload"/> early.</para>
    /// <para><b>Readiness is pushed as well as pulled (AD-MOD-11).</b> <c>Preload</c>, <c>IsReadyAsync</c>
    /// and <c>ShowAsync</c> bind the placement to its unit in the module's one <see cref="MaxAdReadiness"/>,
    /// whether or not the SDK is initialized (<c>ShowAsync</c> after its cancelled-token and fullscreen
    /// refusals). <c>IsReadyAsync</c> is that state's pull — the same <c>ready(unit)</c> the push reads, so
    /// a unit being shown reads not-ready — and never publishes. A show marks its unit showing right after
    /// its visibility slot opens and unmarks it in the outermost <c>finally</c>, beside the slot's close; a
    /// refused show marks nothing.</para>
    /// <para><b>Display timeout (DW-36).</b> Only the gap between <c>Show</c> and the display signal
    /// (<see cref="IMaxBridge.OnDisplayed"/> / <see cref="IMaxBridge.OnDisplayFailed"/>) is timed —
    /// <c>ads.max.displayTimeoutSeconds</c>, absent or ≤ 0 ⇒ 10 s. A show with no display signal in that
    /// window resolves <c>Failed</c> and closes its visibility slot; once displayed, no timeout applies, so
    /// a unit whose Hidden never arrives never returns. A timed-out show the SDK later displays covers the
    /// screen without a visibility fact — a known risk; its slot is already closed, so the one-fullscreen
    /// refusal does not see it either and a second show can stack on top of it.</para>
    /// <para><b>No callback order throws.</b> Rewarded, Hidden, DisplayFailed and the timeout each call
    /// <c>TrySetResult</c>: the first writer wins and the second is a no-op that never throws through
    /// the SDK's callback stack. Hidden reads <see cref="AdResult.Skipped"/> on a rewarded unit and
    /// <see cref="AdResult.Shown"/> on an interstitial, so a native hidden-before-reward on a rewarded unit
    /// would read <c>Skipped</c> — whether MAX ever orders them that way is an unverified device
    /// question.</para>
    /// <para><b>The show slot brackets the show, and only the show (AD-MOD-12).</b> The service never
    /// publishes <see cref="AdVisibilityChanged"/> itself: it opens its <see cref="MaxAdSurface.Show"/>
    /// slot in the module's one <see cref="MaxAdVisibility"/> at show-start and closes it in a
    /// <c>finally</c>, and the state derives and publishes the fact — so a show that ends while the
    /// consent flow is still open releases nothing. <b>One fullscreen surface at a time:</b> a
    /// <c>ShowAsync</c> while any fullscreen slot is open (another show, the consent flow) is refused
    /// right after the cancelled-token check, before any bridge call. A refusal publishes nothing and
    /// subscribes to nothing.</para>
    /// <para><b>Every returned result is auto-logged</b> through <see cref="AdAnalyticsMapping"/> →
    /// <see cref="IAnalytics"/>, as the framework's shipped fake does — unless <c>ads.max.autoLog</c> is
    /// <c>0</c>.</para>
    /// </remarks>
    public sealed class MaxAdsService : IAdsService
    {
        private const string Module = "[pf-ads-max] ";

        private readonly IMaxBridge _max;
        private readonly MaxAdUnits _units;
        private readonly MaxAdVisibility _visibility;
        private readonly MaxAdReadiness _readiness;
        private readonly IAnalytics _analytics;
        private readonly IGameConfig _config;
        private readonly ILog _log;
        private readonly Func<TimeSpan, CancellationToken, UniTask> _delay;

        // No defaulted parameters: VContainer resolves EVERY parameter and ignores C# default values, so a
        // defaulted one would be a hard dependency wearing a disguise.
        public MaxAdsService(
            IMaxBridge max,
            MaxAdUnits units,
            MaxAdVisibility visibility,
            MaxAdReadiness readiness,
            IAnalytics analytics,
            IGameConfig config,
            ILog log,
            Func<TimeSpan, CancellationToken, UniTask> delay)
        {
            _max = max ?? throw new ArgumentNullException(nameof(max));
            _units = units ?? throw new ArgumentNullException(nameof(units));
            _visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
            _readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        }

        /// <summary>The placement-tier config key: <c>ads.max.&lt;placement&gt;.unitId</c>.</summary>
        public static string UnitIdKey(AdPlacement placement) => MaxAdUnits.UnitIdKey(placement.Value);

        /// <summary>The framework's placement-format key (shared with the ad gate).</summary>
        public static string RewardedKey(AdPlacement placement) => MaxAdUnits.RewardedKey(placement.Value);

        public UniTask<bool> IsReadyAsync(AdPlacement placement)
        {
            try
            {
                // Binds the placement and answers ready(unit) as it was before any request this call makes
                // (an unrequested unit reads false, then is requested). Publishes nothing.
                return UniTask.FromResult(_readiness.Query(placement));
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "readiness check for '" + placement.Value + "' threw: " + ex.Message);
                return UniTask.FromResult(false);
            }
        }

        public void Preload(AdPlacement placement)
        {
            try
            {
                // Binding requests an unrequested unit (before init the request is recorded and loaded when
                // auto-load starts); an already-requested one is requested again, as before.
                bool wasRequested;
                var unitId = _readiness.Bind(placement, out wasRequested);
                if (string.IsNullOrEmpty(unitId))
                {
                    _log.Warn(Module + "preload of '" + placement.Value + "' skipped — " + EmptyKeys(placement) + ".");
                    return;
                }
                if (wasRequested) _units.Request(unitId, _units.IsRewarded(placement));
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "preload of '" + placement.Value + "' threw: " + ex.Message);
            }
        }

        public async UniTask<AdResult> ShowAsync(AdPlacement placement, CancellationToken ct)
        {
            var result = await ShowCoreAsync(placement, ct);
            AutoLog(placement, result);
            return result;
        }

        private async UniTask<AdResult> ShowCoreAsync(AdPlacement placement, CancellationToken ct)
        {
            string unitId;
            bool rewarded;
            TimeSpan timeout;
            try
            {
                // ── Refusals: no visibility publish, no subscription. ──────────────────────────────
                if (ct.IsCancellationRequested)
                    return Refuse(placement, "the token was cancelled before the show");
                if (_visibility.IsFullscreenOpen)
                    return Refuse(placement, FullscreenOpen);

                // Bind before the init read (AD-MOD-11): a placement shown before init is still bound, and an
                // unrequested unit is requested here (recorded before init, loaded at auto-load).
                bool wasRequested;
                unitId = _readiness.Bind(placement, out wasRequested);
                if (!_max.IsInitialized)
                    return Refuse(placement, "the MAX SDK is not initialized");

                if (string.IsNullOrEmpty(unitId))
                    return Refuse(placement, EmptyKeys(placement));

                rewarded = _units.IsRewarded(placement);
                if (!wasRequested)
                    return Refuse(placement, "unit '" + unitId + "' had not been requested (a load was requested now; "
                                             + "call Preload early so the first show finds a loaded ad)");
                if (!_max.IsReady(unitId, rewarded))
                {
                    _units.Request(unitId, rewarded);
                    return Refuse(placement, "no ad is loaded for unit '" + unitId + "' (a load was requested)");
                }
                timeout = DisplayTimeout();
            }
            catch (Exception ex)
            {
                return Refuse(placement, "the bridge threw before the show: " + ex.Message);
            }

            // ── The show. From here on the token is ignored. ───────────────────────────────────────
            // The authoritative one-fullscreen guard. The check above already refused every case but
            // re-entrancy (nothing between the two points awaits), so this refusal is free insurance.
            // It comes before any subscription, so a refusal leaves none.
            MaxAdVisibility.Slot slot;
            if (!_visibility.TryOpenExclusive(MaxAdSurface.Show, out slot))
                return Refuse(placement, FullscreenOpen);

            var outcome = new UniTaskCompletionSource<AdResult>();
            var displaySignal = new CancellationTokenSource();

            void Displayed(string id)
            {
                if (id == unitId) displaySignal.Cancel();
            }

            void Hidden(string id)
            {
                if (id == unitId) outcome.TrySetResult(rewarded ? AdResult.Skipped : AdResult.Shown);
            }

            void Rewarded(string id)
            {
                if (id == unitId) outcome.TrySetResult(AdResult.Rewarded);
            }

            void DisplayFailed(string id, string message)
            {
                if (id != unitId) return;
                displaySignal.Cancel();
                _log.Warn(Module + "display of '" + placement.Value + "' failed: " + message);
                outcome.TrySetResult(AdResult.Failed);
            }

            try
            {
                // The unit reads not-ready while it is on screen (AD-MOD-11). Inside the try, so the outermost
                // finally unmarks it on every exit path; never throws.
                _readiness.ShowStarted(unitId);

                // Inside the try, so the slot opened above is closed on every exit path.
                _max.OnDisplayed += Displayed;
                _max.OnHidden += Hidden;
                _max.OnRewarded += Rewarded;
                _max.OnDisplayFailed += DisplayFailed;
                _max.Show(unitId, rewarded, placement.Value);
                if (!displaySignal.IsCancellationRequested)
                    DisplayTimeoutAsync(placement, timeout, displaySignal.Token, outcome).Forget();
                return await outcome.Task;
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "show of '" + placement.Value + "' threw: " + ex.Message);
                return AdResult.Failed;
            }
            finally
            {
                try
                {
                    _max.OnDisplayed -= Displayed;
                    _max.OnHidden -= Hidden;
                    _max.OnRewarded -= Rewarded;
                    _max.OnDisplayFailed -= DisplayFailed;
                    displaySignal.Cancel();
                    displaySignal.Dispose();

                    // Ready the next fill for this unit, through MaxAdUnits — not a direct bridge Load — so
                    // a retry already pending for this unit is never pre-empted (at most one pending retry;
                    // a request while it is pending does not load early).
                    _units.Request(unitId, rewarded);
                }
                finally
                {
                    // Close only this show's slot, whatever the teardown above does: a stranded slot
                    // latches the audio duck, the interaction lock and the gameplay gate, and refuses every
                    // later show. The state publishes false only when no other surface is still open.
                    try
                    {
                        slot.Close();
                    }
                    finally
                    {
                        // Unmark the unit whatever the close does, so it never reads not-ready forever.
                        _readiness.ShowEnded(unitId);
                    }
                }
            }
        }

        private const string FullscreenOpen = "another fullscreen ad surface is open";

        /// <summary>Fails the show when no display signal arrives within <paramref name="timeout"/>; a display
        /// signal (or the show ending) cancels it.</summary>
        private async UniTaskVoid DisplayTimeoutAsync(
            AdPlacement placement, TimeSpan timeout, CancellationToken displayed, UniTaskCompletionSource<AdResult> outcome)
        {
            try
            {
                await _delay(timeout, displayed);
            }
            catch (OperationCanceledException)
            {
                return; // displayed, display-failed or the show already ended
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "display timeout for '" + placement.Value + "' could not run: " + ex.Message);
                return;
            }
            if (displayed.IsCancellationRequested || outcome.Task.Status != UniTaskStatus.Pending) return;
            _log.Warn(Module + "show of '" + placement.Value + "' failed — no display signal within "
                      + timeout.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                      + " s (" + MaxAdUnits.DisplayTimeoutKey + "); a later callback for this show is ignored.");
            outcome.TrySetResult(AdResult.Failed);
        }

        private TimeSpan DisplayTimeout()
        {
            int seconds;
            if (!MaxAdUnits.TryReadInt(_config, MaxAdUnits.DisplayTimeoutKey, out seconds) || seconds <= 0)
                seconds = MaxAdUnits.DefaultDisplayTimeoutSeconds;
            return TimeSpan.FromSeconds(seconds);
        }

        private string EmptyKeys(AdPlacement placement)
        {
            IReadOnlyList<string> keys;
            try { keys = _units.KeysFor(placement); }
            catch (Exception) { keys = new[] { UnitIdKey(placement) }; }
            return "no unit id: config key" + (keys.Count == 1 ? " '" : "s '") + string.Join("', '", keys) + "' "
                   + (keys.Count == 1 ? "is" : "are all") + " empty";
        }

        private AdResult Refuse(AdPlacement placement, string reason)
        {
            _log.Warn(Module + "show of '" + placement.Value + "' refused — " + reason + ".");
            return AdResult.Failed;
        }

        private bool AutoLogEnabled()
        {
            int value;
            return !MaxAdUnits.TryReadInt(_config, MaxAdUnits.AutoLogKey, out value) || value != 0;
        }

        private void AutoLog(AdPlacement placement, AdResult result)
        {
            try
            {
                if (!AutoLogEnabled()) return;
                var evt = AdAnalyticsMapping.FromResult(placement, result);
                if (evt != null) _analytics.Track(evt);
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "analytics auto-log of '" + placement.Value + "' threw: " + ex.Message);
            }
        }
    }
}
