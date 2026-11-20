using System;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>The surfaces that can put MAX UI on screen, one visibility slot each. All three are
    /// fullscreen; there is no banner slot.</summary>
    public enum MaxAdSurface
    {
        /// <summary>A fullscreen show (interstitial or rewarded), opened by <see cref="MaxAdsService"/>.</summary>
        Show,

        /// <summary>The SDK-init window, in which MAX's consent flow can present — opened by
        /// <see cref="MaxAdsInitNode"/>.</summary>
        ConsentFlow,

        /// <summary>The privacy-options ("change privacy settings") window — MAX's consent form re-shown to
        /// an existing user, opened by <see cref="MaxAdsPrivacyOptions"/> (<c>IAdsPrivacyOptions</c>).</summary>
        PrivacyOptions,
    }

    /// <summary>
    /// The module's one visibility state (AD-MOD-12): one slot per surface, and the only constructor of
    /// <see cref="AdVisibilityChanged"/> in the module. Registered once, as a singleton, and handed to
    /// every producer.
    /// </summary>
    /// <remarks>
    /// <para><b>The fact is derived, never written.</b> <c>FullscreenAdVisible</c> = any slot open;
    /// <c>BannerVisible</c> is always false and <c>BannerHeightPx</c> always 0. The state starts at "not
    /// visible", publishes nothing until the derived value changes, and then publishes only the change —
    /// so a surface that closes while another is still open publishes nothing, and every level-triggered
    /// subscriber (input lock, audio duck, tick gate) holds until the last one closes.</para>
    /// <para><b>A producer opens and closes only its own slot</b>, in a <c>finally</c>.
    /// <see cref="Slot.Close"/> is idempotent per handle: a second close of the same handle never closes
    /// another producer's slot.</para>
    /// <para><b>At most one fullscreen surface.</b> <see cref="TryOpenExclusive"/> is refused while any
    /// slot is open. <see cref="Open"/> is unconditional — it is for SDK init, which is never refused.</para>
    /// <para><b>A throwing publisher is a warning, never a throw.</b> The new derived value is recorded
    /// anyway, so a failed <c>true</c> is still followed by <c>false</c> when the last slot closes.</para>
    /// <para>Callbacks arrive on the main thread (AD-MOD-2); the lock guards the fields and never
    /// marshals. The publish runs outside the lock, but it is not re-entrant-safe for subscribers: a
    /// subscriber that opens or closes a slot synchronously inside a publish has its own fact delivered
    /// nested, so subscribers later in the list receive the outer, stale value last. A visibility
    /// subscriber must therefore never show an ad (or otherwise open or close a slot) synchronously from
    /// inside the publish.</para>
    /// </remarks>
    public sealed class MaxAdVisibility
    {
        private const string Module = "[pf-ads-max] ";
        private const int SurfaceCount = 3;

        private readonly IAdVisibilityChangedPublisher _publisher;
        private readonly ILog _log;
        private readonly object _gate = new object();
        private readonly int[] _open = new int[SurfaceCount];
        private bool _lastPublished;

        public MaxAdVisibility(IAdVisibilityChangedPublisher publisher, ILog log)
        {
            _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>True while any fullscreen slot is open.</summary>
        public bool IsFullscreenOpen
        {
            get { lock (_gate) return AnyOpenLocked(); }
        }

        /// <summary>Opens a slot for <paramref name="surface"/> unconditionally (SDK init is never refused).</summary>
        public Slot Open(MaxAdSurface surface)
        {
            var index = Index(surface);
            Slot slot;
            lock (_gate)
            {
                _open[index]++;
                slot = new Slot(this, surface);
            }
            PublishIfChanged();
            return slot;
        }

        /// <summary>Opens a slot for <paramref name="surface"/> only when no fullscreen slot is open;
        /// a refusal returns false, a null <paramref name="slot"/>, and publishes nothing.</summary>
        public bool TryOpenExclusive(MaxAdSurface surface, out Slot slot)
        {
            var index = Index(surface);
            lock (_gate)
            {
                if (AnyOpenLocked())
                {
                    slot = null;
                    return false;
                }
                _open[index]++;
                slot = new Slot(this, surface);
            }
            PublishIfChanged();
            return true;
        }

        private void Release(Slot slot)
        {
            lock (_gate)
            {
                if (!slot.OpenLocked) return;
                slot.OpenLocked = false;
                _open[Index(slot.Surface)]--;
            }
            PublishIfChanged();
        }

        private bool AnyOpenLocked()
        {
            for (var i = 0; i < SurfaceCount; i++)
                if (_open[i] > 0) return true;
            return false;
        }

        private void PublishIfChanged()
        {
            bool value;
            lock (_gate)
            {
                value = AnyOpenLocked();
                if (value == _lastPublished) return;
                // Recorded before the publish: a publisher that throws on true is still followed by false.
                _lastPublished = value;
            }
            try
            {
                _publisher.Publish(new AdVisibilityChanged(value, false, 0));
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "AdVisibilityChanged(" + (value ? "true" : "false") + ") publish threw: " + ex.Message);
            }
        }

        private static int Index(MaxAdSurface surface)
        {
            var index = (int)surface;
            if (index < 0 || index >= SurfaceCount)
                throw new ArgumentOutOfRangeException(nameof(surface), surface, "not a MAX ad surface");
            return index;
        }

        /// <summary>One producer's open slot. <see cref="Close"/> is idempotent per handle.</summary>
        public sealed class Slot
        {
            private readonly MaxAdVisibility _owner;

            // Guarded by the owner's lock.
            internal bool OpenLocked = true;

            internal Slot(MaxAdVisibility owner, MaxAdSurface surface)
            {
                _owner = owner;
                Surface = surface;
            }

            public MaxAdSurface Surface { get; }

            public bool IsOpen
            {
                get { lock (_owner._gate) return OpenLocked; }
            }

            /// <summary>Closes this slot; a second call is a no-op and never closes another slot.</summary>
            public void Close() => _owner.Release(this);
        }
    }
}
