using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;
using ClassicSpins.PrototypeFramework.Domain;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Ads.Max.HeadlessTests
{
    /// <summary>
    /// Story 10.11 (FR56, AD-MOD-9, AD-MOD-12) — every row of the I/O matrix for
    /// <see cref="MaxAdsPrivacyOptions"/>: available only once initialized with a supported CMP, both read
    /// at call time; the five refusals in order, each one warning with no bridge show and no publish; the
    /// form holds its own <see cref="MaxAdSurface.PrivacyOptions"/> slot until MAX's completion callback,
    /// closed in a <c>finally</c>; nothing throws. The framework-only row (the Null resolves) is
    /// <c>AdsInstallerPrivacyOptionsTests</c> under the Unity runner and <c>AdsPrivacyOptionsPortTests</c>
    /// headless.
    /// </summary>
    [TestFixture]
    public sealed class MaxAdsPrivacyOptionsTests
    {
        private FakeMaxBridge _bridge;
        private RecordingPublisher _publisher;
        private RecordingLog _log;
        private MaxAdVisibility _state;
        private MaxAdsPrivacyOptions _options;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeMaxBridge { Initialized = true, SupportedCmp = true };
            _publisher = new RecordingPublisher();
            _log = new RecordingLog();
            _state = new MaxAdVisibility(_publisher, _log);
            _options = new MaxAdsPrivacyOptions(_bridge, _state, _log);
        }

        private Task<bool> Show(CancellationToken ct = default) => _options.ShowAsync(ct).AsTask();

        private void AssertOneWarningNaming(string reason)
        {
            Assert.That(_log.Warns, Has.Count.EqualTo(1), string.Join(" | ", _log.Warns));
            Assert.That(_log.Warns[0], Does.Contain("pf-ads-max"));
            Assert.That(_log.Warns[0], Does.Contain(reason));
        }

        private void AssertRefusedUntouched(bool result, string reason)
        {
            Assert.That(result, Is.False);
            Assert.That(_bridge.CmpCalls, Is.Zero, "no ShowCmpForExistingUser");
            Assert.That(_publisher.Published, Is.Empty, "no publish");
            Assert.That(_state.IsFullscreenOpen, Is.False, "no slot left open");
            AssertOneWarningNaming(reason);
        }

        /// <summary>Every completed form's end: no slot left open, and the pair true → false published.</summary>
        private void AssertPairPublishedAndClosed()
        {
            Assert.That(_state.IsFullscreenOpen, Is.False, "the privacy slot is closed");
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false }));
        }

        // ── shape ──────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void It_is_the_framework_port_and_construction_reads_nothing()
        {
            Assert.That(_options, Is.InstanceOf<IAdsPrivacyOptions>());
            Assert.That(_bridge.IsInitializedReads, Is.Zero, "init completes long after the container is built");
            Assert.That(_bridge.Calls, Is.Empty);
            Assert.That(_publisher.Published, Is.Empty);
        }

        [Test]
        public void Null_constructor_arguments_throw()
        {
            Assert.Throws<ArgumentNullException>(() => new MaxAdsPrivacyOptions(null, _state, _log));
            Assert.Throws<ArgumentNullException>(() => new MaxAdsPrivacyOptions(_bridge, null, _log));
            Assert.Throws<ArgumentNullException>(() => new MaxAdsPrivacyOptions(_bridge, _state, null));
        }

        // ── availability ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Available_once_initialized_with_a_supported_CMP_read_at_call_time()
        {
            _bridge.Initialized = false;
            Assert.That(_options.IsAvailable, Is.False);
            _bridge.Initialized = true;
            Assert.That(_options.IsAvailable, Is.True, "read now, never cached");
            _bridge.SupportedCmp = false;
            Assert.That(_options.IsAvailable, Is.False);
            Assert.That(_log.Warns, Is.Empty, "an unavailable read is not a fault");
        }

        [Test]
        public async Task Before_init_it_is_unavailable_and_ShowAsync_refuses()
        {
            _bridge.Initialized = false;
            Assert.That(_options.IsAvailable, Is.False);
            AssertRefusedUntouched(await Show(), "not initialized");
        }

        [Test]
        public async Task With_no_supported_CMP_it_is_unavailable_and_ShowAsync_refuses()
        {
            _bridge.SupportedCmp = false;
            Assert.That(_options.IsAvailable, Is.False);
            AssertRefusedUntouched(await Show(), "no supported consent platform");
        }

        [Test]
        public async Task The_CMP_is_never_read_before_init()
        {
            _bridge.Initialized = false;
            _bridge.ThrowOnHasSupportedCmp = true;
            Assert.That(_options.IsAvailable, Is.False);
            Assert.That(_log.Warns, Is.Empty, "short-circuits on the initialized read");
            AssertRefusedUntouched(await Show(), "not initialized");
        }

        [TestCase("IsInitialized")]
        [TestCase("HasSupportedCmp")]
        public async Task A_throwing_read_is_unavailable_with_a_warning_and_never_throws(string read)
        {
            if (read == "IsInitialized") _bridge.ThrowOnIsInitialized = true;
            else _bridge.ThrowOnHasSupportedCmp = true;

            bool available = true;
            Assert.DoesNotThrow(() => available = _options.IsAvailable);
            Assert.That(available, Is.False);
            AssertOneWarningNaming("threw");

            _log.Warns.Clear();
            AssertRefusedUntouched(await Show(), "threw");
        }

        // ── refusals ───────────────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task A_cancelled_token_refuses_before_anything()
        {
            AssertRefusedUntouched(await Show(new CancellationToken(true)), "cancelled");
            Assert.That(_bridge.IsInitializedReads, Is.Zero, "refused before any bridge read");
        }

        [TestCase(MaxAdSurface.Show)]
        [TestCase(MaxAdSurface.ConsentFlow)]
        public async Task An_open_fullscreen_slot_refuses_with_no_extra_publish(MaxAdSurface surface)
        {
            var other = _state.Open(surface);
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }));

            Assert.That(await Show(), Is.False);
            Assert.That(_bridge.CmpCalls, Is.Zero, "no ShowCmpForExistingUser");
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "no extra publish");
            Assert.That(_bridge.IsInitializedReads, Is.Zero, "refused before the availability read");
            AssertOneWarningNaming("another fullscreen ad surface is open");

            other.Close();
            Assert.That(_state.IsFullscreenOpen, Is.False, "the other producer's slot was never touched");
        }

        [Test]
        public async Task A_refused_exclusive_open_refuses_with_no_bridge_show_and_leaves_no_running_flag()
        {
            // The fifth, authoritative guard: another surface opens between the fullscreen check and
            // TryOpenExclusive (here, inside the availability read).
            MaxAdVisibility.Slot other = null;
            _bridge.DuringHasSupportedCmp = () =>
            {
                _bridge.DuringHasSupportedCmp = null;
                other = _state.Open(MaxAdSurface.Show);
            };

            Assert.That(await Show(), Is.False);
            Assert.That(other, Is.Not.Null, "the hook ran mid-availability-read");
            Assert.That(_bridge.CmpCalls, Is.Zero, "no ShowCmpForExistingUser");
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "only the other slot's own publish");
            AssertOneWarningNaming("another fullscreen ad surface is open");

            other.Close();
            _log.Warns.Clear();
            _bridge.CompleteCmpSynchronously = true;
            Assert.That(await Show(), Is.True, "the running flag was never left set");
            Assert.That(_bridge.CmpCalls, Is.EqualTo(1));
        }

        [Test]
        public async Task A_second_call_while_the_first_is_pending_refuses_and_the_first_still_resolves()
        {
            var first = Show();
            Assert.That(first.IsCompleted, Is.False, "the form is up");

            Assert.That(await Show(), Is.False);
            Assert.That(_bridge.CmpCalls, Is.EqualTo(1), "one bridge show in total");
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "the refusal publishes nothing");
            AssertOneWarningNaming("still running");

            _bridge.PendingCmp(null);
            Assert.That(await first, Is.True);
            AssertPairPublishedAndClosed();
        }

        // ── the form ───────────────────────────────────────────────────────────────────────────────────

        [Test]
        public async Task The_happy_path_publishes_true_waits_for_the_callback_then_resolves_true_and_publishes_false()
        {
            var show = Show();

            Assert.That(_bridge.CmpCalls, Is.EqualTo(1));
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "visible before the form");
            Assert.That(_state.IsFullscreenOpen, Is.True, "the privacy slot is open while the form is up");
            Assert.That(show.IsCompleted, Is.False, "resolves on the completion callback");

            _bridge.PendingCmp(null);

            Assert.That(await show, Is.True);
            AssertPairPublishedAndClosed();
            Assert.That(_log.Warns, Is.Empty);
        }

        [Test]
        public async Task A_CMP_error_resolves_false_with_a_warning_naming_it_and_closes_the_slot()
        {
            var show = Show();
            _bridge.PendingCmp("FormUnavailable: no form configured");

            Assert.That(await show, Is.False);
            AssertPairPublishedAndClosed();
            AssertOneWarningNaming("FormUnavailable: no form configured");
        }

        [Test]
        public async Task A_synchronous_callback_resolves_closes_the_slot_and_the_next_call_proceeds()
        {
            _bridge.CompleteCmpSynchronously = true;

            Assert.That(await Show(), Is.True);
            AssertPairPublishedAndClosed();

            Assert.That(await Show(), Is.True, "the running flag was cleared");
            Assert.That(_bridge.CmpCalls, Is.EqualTo(2));
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true, false, true, false }));
            Assert.That(_log.Warns, Is.Empty);
        }

        [Test]
        public async Task A_synchronous_error_resolves_false_and_closes_the_slot()
        {
            _bridge.CompleteCmpSynchronously = true;
            _bridge.SyncCmpResult = "FormUnavailable: stub";

            Assert.That(await Show(), Is.False);
            AssertPairPublishedAndClosed();
            AssertOneWarningNaming("FormUnavailable: stub");
        }

        [Test]
        public async Task A_doubled_callback_never_throws_and_the_first_value_wins()
        {
            var show = Show();
            var callback = _bridge.PendingCmp;

            Assert.DoesNotThrow(() => callback(null));
            Assert.DoesNotThrow(() => callback("Late: second completion"));
            Assert.That(await show, Is.True, "the first value wins");
            Assert.DoesNotThrow(() => callback("Later: third completion"));

            AssertPairPublishedAndClosed();
            Assert.That(_log.Warns, Is.Empty);
        }

        [Test]
        public async Task A_doubled_synchronous_callback_never_throws_and_the_first_value_wins()
        {
            _bridge.CompleteCmpSynchronously = true;

            Assert.That(await Show(), Is.True);
            Assert.DoesNotThrow(() => _bridge.PendingCmp("Late: second completion"));
            AssertPairPublishedAndClosed();
            Assert.That(_log.Warns, Is.Empty);
        }

        [Test]
        public async Task A_throwing_bridge_show_resolves_false_closes_the_slot_and_clears_the_running_flag()
        {
            _bridge.ThrowOnShowCmp = true;

            Assert.That(await Show(), Is.False);
            AssertPairPublishedAndClosed();
            AssertOneWarningNaming("ShowCmpForExistingUser exploded");

            _bridge.ThrowOnShowCmp = false;
            _bridge.CompleteCmpSynchronously = true;
            Assert.That(await Show(), Is.True, "the running flag was cleared");
            Assert.That(_bridge.CmpCalls, Is.EqualTo(2));
        }

        [Test]
        public async Task A_late_cancel_is_ignored_and_the_result_still_waits_for_the_callback()
        {
            using (var cts = new CancellationTokenSource())
            {
                var show = Show(cts.Token);
                cts.Cancel();
                await Task.Delay(30);

                Assert.That(show.IsCompleted, Is.False, "the form is human-paced; the token is ignored once it is up");
                Assert.That(_state.IsFullscreenOpen, Is.True, "the slot stays open while the form covers the screen");

                _bridge.PendingCmp(null);
                Assert.That(await show, Is.True);
            }
            AssertPairPublishedAndClosed();
        }

        [Test]
        public async Task An_ad_shown_during_the_form_is_refused_by_the_one_fullscreen_rule()
        {
            var placement = new AdPlacement("p");
            var config = new DictConfig().Set("ads.max.p.unitId", "u");
            var delay = new FakeDelay();
            var units = new MaxAdUnits(_bridge, config, _log, null, delay.Func);
            var readiness = new MaxAdReadiness(_bridge, units, config, new NullAdReadinessChangedPublisher(), _log);
            var service = new MaxAdsService(_bridge, units, _state, readiness, new RecordingAnalytics(), config, _log, delay.Func);
            service.Preload(placement);

            var form = Show();
            Assert.That(_state.IsFullscreenOpen, Is.True);

            Assert.That(await service.ShowAsync(placement, CancellationToken.None).AsTask(), Is.EqualTo(AdResult.Failed));
            Assert.That(_bridge.ShowCalls, Is.Empty, "the ad never reached the SDK");
            Assert.That(_publisher.Fullscreen, Is.EqualTo(new[] { true }), "the refused show published nothing");
            Assert.That(_log.Warns.Any(w => w.Contains("another fullscreen ad surface is open")), Is.True,
                string.Join(" | ", _log.Warns));

            _bridge.PendingCmp(null);
            Assert.That(await form, Is.True);
            AssertPairPublishedAndClosed();
        }

        [Test]
        public async Task The_form_open_during_an_ad_is_refused()
        {
            var ad = _state.Open(MaxAdSurface.Show);
            Assert.That(await Show(), Is.False);
            Assert.That(_bridge.CmpCalls, Is.Zero);
            ad.Close();

            _log.Warns.Clear();
            _bridge.CompleteCmpSynchronously = true;
            Assert.That(await Show(), Is.True, "once the ad is gone the form opens");
        }
    }
}
