using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>
    /// The module's <see cref="IAdsPrivacyOptions"/> (AD-MOD-9, FR56): "change privacy settings" re-shows
    /// MAX's own consent form (its CMP — Google UMP where it applies) to an existing user. It replaces the
    /// framework's <see cref="NullAdsPrivacyOptions"/>, declared <c>replacement</c> in <c>ports[]</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>Available</b> = the SDK has initialized ∧ it reports a supported CMP, both read on
    /// <b>every</b> call (init completes long after the container is built). A throwing read is one
    /// warning and reads false.</para>
    /// <para><b>Refusals, in order</b> — each returns false with one warning naming <c>pf-ads-max</c> and
    /// the reason, never calls the bridge's show and publishes nothing: the token is already cancelled; a
    /// previous call is still running (MAX keeps one static completion callback, so a second show would
    /// overwrite the first one's); any fullscreen slot is open; not available; and the authoritative
    /// guard, <see cref="MaxAdVisibility.TryOpenExclusive"/> on <see cref="MaxAdSurface.PrivacyOptions"/>.</para>
    /// <para><b>Its own visibility slot (AD-MOD-12).</b> The form covers the screen, so the call holds the
    /// privacy-options slot from before the bridge call until the completion callback, and closes it —
    /// and clears the running flag — in a <c>finally</c> on every path. While it is open, every ad show is
    /// refused by the one-fullscreen rule.</para>
    /// <para><b>The token is ignored once the form is up.</b> The form is human-paced; returning while it
    /// still covers the screen would desync visibility (the <see cref="MaxAdsService"/> precedent). There
    /// is no timeout over it.</para>
    /// <para><b>Writes nothing back.</b> MAX stores and applies the outcome; the framework's consent
    /// service is never touched. The result is true on a completion with no error, false with a warning
    /// naming MAX's error otherwise. A callback that arrives synchronously, or twice, never throws — the
    /// first value wins.</para>
    /// </remarks>
    public sealed class MaxAdsPrivacyOptions : IAdsPrivacyOptions
    {
        private const string Module = "[pf-ads-max] ";
        private const string Subject = "privacy options";
        private const string FullscreenOpen = "another fullscreen ad surface is open";

        private readonly IMaxBridge _max;
        private readonly MaxAdVisibility _visibility;
        private readonly ILog _log;
        private readonly object _gate = new object();
        private bool _running;

        // No defaulted parameters: VContainer resolves every parameter and ignores C# default values.
        public MaxAdsPrivacyOptions(IMaxBridge max, MaxAdVisibility visibility, ILog log)
        {
            _max = max ?? throw new ArgumentNullException(nameof(max));
            _visibility = visibility ?? throw new ArgumentNullException(nameof(visibility));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>Initialized ∧ a supported CMP, read now. A faulting read is logged and reads false.</summary>
        public bool IsAvailable
        {
            get
            {
                string why;
                bool fault;
                var available = Available(out why, out fault);
                if (fault) _log.Warn(Module + Subject + " are unavailable: " + why + ".");
                return available;
            }
        }

        /// <summary>Show MAX's consent form and resolve on its completion callback. Never throws.</summary>
        public async UniTask<bool> ShowAsync(CancellationToken ct)
        {
            // ── Refusals: no slot, no bridge show, no publish. ────────────────────────────────────────
            if (ct.IsCancellationRequested)
                return Refuse("the token was cancelled before the form was shown");
            if (IsRunning)
                return Refuse("a previous ShowAsync is still running (MAX keeps one completion callback)");
            if (_visibility.IsFullscreenOpen)
                return Refuse(FullscreenOpen);

            string why;
            bool fault;
            if (!Available(out why, out fault))
                return Refuse(why);

            // The authoritative one-fullscreen guard; nothing between the check above and here awaits.
            MaxAdVisibility.Slot slot;
            if (!_visibility.TryOpenExclusive(MaxAdSurface.PrivacyOptions, out slot))
                return Refuse(FullscreenOpen);

            // ── The form. From here on the token is ignored. ──────────────────────────────────────────
            lock (_gate) _running = true;
            try
            {
                var done = new UniTaskCompletionSource<string>();
                // TrySetResult: a synchronous or a doubled callback never throws, and the first value wins.
                _max.ShowCmpForExistingUser(error => done.TrySetResult(error));
                var reported = await done.Task;
                if (reported == null) return true;

                _log.Warn(Module + Subject + ": MAX's consent form reported an error: "
                          + (reported.Length == 0 ? "(no message)" : reported));
                return false;
            }
            catch (Exception ex)
            {
                _log.Warn(Module + Subject + " failed to show: " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
            finally
            {
                // Close only this call's slot, whatever happened; then let the next call in.
                try
                {
                    slot.Close();
                }
                finally
                {
                    lock (_gate) _running = false;
                }
            }
        }

        private bool IsRunning
        {
            get { lock (_gate) return _running; }
        }

        private bool Refuse(string why)
        {
            _log.Warn(Module + Subject + " refused: " + why + ".");
            return false;
        }

        private bool Available(out string why, out bool fault)
        {
            fault = false;
            try
            {
                if (!_max.IsInitialized)
                {
                    why = "the AppLovin MAX SDK is not initialized";
                    return false;
                }
            }
            catch (Exception ex)
            {
                fault = true;
                why = "reading the SDK's initialized state threw (" + ex.GetType().Name + ": " + ex.Message + ")";
                return false;
            }

            try
            {
                if (!_max.HasSupportedCmp)
                {
                    why = "MAX reports no supported consent platform (CMP)";
                    return false;
                }
            }
            catch (Exception ex)
            {
                fault = true;
                why = "reading whether MAX has a supported CMP threw (" + ex.GetType().Name + ": " + ex.Message + ")";
                return false;
            }

            why = null;
            return true;
        }
    }
}
