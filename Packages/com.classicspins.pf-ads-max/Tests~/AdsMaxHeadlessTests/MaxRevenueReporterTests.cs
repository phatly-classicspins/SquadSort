using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    /// Story 10.3 — every row of the I/O matrix for <see cref="MaxRevenueReporter"/> (FR39/FR40): a paid
    /// impression becomes exactly one normalized <see cref="AdRevenuePaid"/>, invalid revenue is dropped with
    /// a warning, the switch is <c>ads.max.revenueLog</c> alone, consent is the sink's business, and nothing
    /// ever throws back into the SDK callback stack. The bridge's own wiring (<c>Runtime/Sdk</c>) is held by
    /// source scan.
    /// </summary>
    [TestFixture]
    public sealed class MaxRevenueReporterTests
    {
        private const string Unit = "u";

        private FakeMaxBridge _bridge;
        private RecordingAnalytics _analytics;
        private DictConfig _config;
        private RecordingLog _log;
        private MaxRevenueReporter _reporter;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeMaxBridge { Initialized = true, Ready = true };
            _analytics = new RecordingAnalytics();
            _config = new DictConfig().Set("ads.max.p.unitId", Unit);
            _log = new RecordingLog();
            _reporter = new MaxRevenueReporter(_bridge, _analytics, _config, _log);
        }

        [TearDown]
        public void TearDown() => _reporter.Dispose();

        private static MaxRevenue Paid(
            double revenue = 0.0123, string format = "REWARDED", bool rewarded = true, string placement = "p",
            string precision = "exact", string unit = Unit, string network = "AppLovin")
            => new MaxRevenue(unit, format, rewarded, network, placement, revenue, precision);

        private AdRevenuePaid[] Records => _analytics.Tracked.OfType<AdRevenuePaid>().ToArray();

        private void AssertOneWarningNamingTheModule()
        {
            Assert.That(_log.Warns, Has.Count.EqualTo(1), string.Join(" | ", _log.Warns));
            Assert.That(_log.Warns[0], Does.Contain("pf-ads-max"));
        }

        // ── the record ─────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void A_paid_impression_tracks_exactly_one_normalized_record()
        {
            _bridge.RaiseRevenuePaid(Paid());

            Assert.That(_analytics.Tracked, Has.Count.EqualTo(1));
            Assert.That(_analytics.Tracked[0], Is.EqualTo(
                new AdRevenuePaid("applovin_max", "p", "u", "rewarded", "AppLovin", 0.0123, "USD", "exact")));
            Assert.That(_log.Warns, Is.Empty);
        }

        [Test]
        public void Mediator_and_currency_are_the_named_constants()
        {
            Assert.That(MaxRevenueReporter.Mediator, Is.EqualTo("applovin_max"));
            Assert.That(MaxRevenueReporter.Currency, Is.EqualTo("USD"));
            Assert.That(MaxRevenueReporter.RevenueLogKey, Is.EqualTo("ads.max.revenueLog"));
        }

        [Test]
        public void Null_unit_network_and_precision_become_empty_and_undefined()
        {
            _bridge.RaiseRevenuePaid(new MaxRevenue(null, null, false, null, null, 0.5, null));

            Assert.That(Records.Single(), Is.EqualTo(
                new AdRevenuePaid("applovin_max", "", "", "interstitial", "", 0.5, "USD", "undefined")));
        }

        // ── revenue validity ───────────────────────────────────────────────────────────────────────────

        [Test]
        public void Negative_revenue_records_nothing_and_warns_once()
        {
            _bridge.RaiseRevenuePaid(Paid(revenue: -1));

            Assert.That(_analytics.Tracked, Is.Empty);
            AssertOneWarningNamingTheModule();
        }

        [Test]
        public void Zero_revenue_is_kept()
        {
            _bridge.RaiseRevenuePaid(Paid(revenue: 0));

            Assert.That(Records.Single().Revenue, Is.EqualTo(0d));
            Assert.That(_log.Warns, Is.Empty);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void Non_finite_revenue_records_nothing_and_warns_once(double revenue)
        {
            _bridge.RaiseRevenuePaid(Paid(revenue: revenue));

            Assert.That(_analytics.Tracked, Is.Empty);
            AssertOneWarningNamingTheModule();
        }

        // ── consent is the sink's ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Consent_denied_still_calls_Track_once_and_the_sink_drops_it()
        {
            var consent = new FakeConsent();
            consent.ResolveInitial(new[] { ConsentPurpose.PersonalizedAds }); // resolved, Analytics denied
            var provider = new CountingProvider();
            var sink = new CountingAnalytics(new FanOutAnalytics(
                new IAnalyticsProvider[] { provider }, consent, new InMemoryAnalyticsBufferStore(), _log, null));
            var bridge = new FakeMaxBridge();
            using (new MaxRevenueReporter(bridge, sink, _config, _log))
            {
                bridge.RaiseRevenuePaid(Paid());
            }

            Assert.That(sink.TrackCalls, Is.EqualTo(1), "the producer emits whatever the consent state");
            Assert.That(provider.Sent, Is.Empty, "the sink applies consent: a denied provider receives nothing");
        }

        [Test]
        public void Consent_granted_reaches_the_provider_once()
        {
            var consent = new FakeConsent();
            consent.ResolveInitial(new[] { ConsentPurpose.Analytics });
            var provider = new CountingProvider();
            var bridge = new FakeMaxBridge();
            using (new MaxRevenueReporter(bridge,
                       new FanOutAnalytics(new IAnalyticsProvider[] { provider }, consent, new InMemoryAnalyticsBufferStore(), _log, null),
                       _config, _log))
            {
                bridge.RaiseRevenuePaid(Paid());
            }

            Assert.That(provider.Sent.OfType<AdRevenuePaid>().Count(), Is.EqualTo(1));
        }

        // ── the switches ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public void RevenueLog_0_records_nothing()
        {
            _config.Set(MaxRevenueReporter.RevenueLogKey, 0);
            _bridge.RaiseRevenuePaid(Paid());

            Assert.That(_analytics.Tracked, Is.Empty);
        }

        [Test]
        public void RevenueLog_string_0_records_nothing_and_non_zero_records()
        {
            _config.Set(MaxRevenueReporter.RevenueLogKey, "0");
            _bridge.RaiseRevenuePaid(Paid());
            Assert.That(_analytics.Tracked, Is.Empty);

            _config.Set(MaxRevenueReporter.RevenueLogKey, 1);
            _bridge.RaiseRevenuePaid(Paid());
            Assert.That(Records, Has.Length.EqualTo(1), "the switch is read at callback time");
        }

        [Test]
        public async Task AutoLog_0_silences_the_funnel_but_not_the_revenue_record()
        {
            _config.Set(MaxAdUnits.AutoLogKey, 0).Set("ads.p.rewarded", 1);
            var delay = new FakeDelay();
            var units = new MaxAdUnits(_bridge, _config, _log, null, delay.Func);
            var service = new MaxAdsService(_bridge, units, new MaxAdVisibility(new RecordingPublisher(), _log),
                new MaxAdReadiness(_bridge, units, _config, new NullAdReadinessChangedPublisher(), _log),
                _analytics, _config, _log, delay.Func);
            var placement = new AdPlacement("p");
            service.Preload(placement);

            var show = service.ShowAsync(placement, CancellationToken.None).AsTask();
            _bridge.RaiseDisplayed(Unit);
            _bridge.RaiseRevenuePaid(Paid());
            _bridge.RaiseRewarded(Unit);
            _bridge.RaiseHidden(Unit);

            Assert.That(await show, Is.EqualTo(AdResult.Rewarded));
            Assert.That(_analytics.Tracked, Is.EqualTo(new GameEvent[]
            {
                new AdRevenuePaid("applovin_max", "p", "u", "rewarded", "AppLovin", 0.0123, "USD", "exact"),
            }), "AdRevenuePaid only — no AdShown / AdRewarded with the funnel switch off");
        }

        // ── placement ──────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void No_echo_is_an_empty_placement_never_the_unit_binding()
        {
            // ads.max.p.unitId = u: the unit is bound to placement p, yet with no SDK echo the field stays "".
            _bridge.RaiseRevenuePaid(Paid(placement: null));

            Assert.That(Records.Single().Placement, Is.EqualTo(""));
        }

        // ── vocabularies ───────────────────────────────────────────────────────────────────────────────

        [TestCase("REWARDED", false, "rewarded")]
        [TestCase("rewarded", false, "rewarded")]
        [TestCase("INTER", true, "interstitial")]
        [TestCase("interstitial", true, "interstitial")]
        [TestCase("BANNER", false, "banner")]
        [TestCase("LEADER", false, "banner")]
        [TestCase("MREC", false, "mrec")]
        [TestCase("APPOPEN", false, "app_open")]
        [TestCase("APP_OPEN", false, "app_open")]
        [TestCase(" app_open ", false, "app_open")]
        [TestCase("NATIVE", true, "rewarded")]
        [TestCase("NATIVE", false, "interstitial")]
        [TestCase("", true, "rewarded")]
        [TestCase("", false, "interstitial")]
        [TestCase(null, true, "rewarded")]
        [TestCase(null, false, "interstitial")]
        public void AdFormat_normalizes_to_the_vocabulary(string raw, bool rewarded, string expected)
        {
            _bridge.RaiseRevenuePaid(Paid(format: raw, rewarded: rewarded));

            Assert.That(Records.Single().AdFormat, Is.EqualTo(expected));
        }

        [TestCase("exact", "exact")]
        [TestCase("EXACT", "exact")]
        [TestCase("estimated", "estimated")]
        [TestCase("publisher_defined", "publisher_defined")]
        [TestCase("undefined", "undefined")]
        [TestCase("something_new", "undefined")]
        [TestCase("", "undefined")]
        [TestCase(null, "undefined")]
        public void Precision_normalizes_to_the_vocabulary(string raw, string expected)
        {
            _bridge.RaiseRevenuePaid(Paid(precision: raw));

            Assert.That(Records.Single().Precision, Is.EqualTo(expected));
        }

        // ── threads ────────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Off_thread_callbacks_are_both_tracked_with_exactly_one_thread_warning()
        {
            var worker = new Thread(() =>
            {
                _bridge.RaiseRevenuePaid(Paid());
                _bridge.RaiseRevenuePaid(Paid());
            });
            worker.Start();
            worker.Join();

            Assert.That(Records, Has.Length.EqualTo(2), "Core never marshals and never drops for a thread");
            Assert.That(_log.Warns.Count(w => w.Contains("thread")), Is.EqualTo(1), string.Join(" | ", _log.Warns));
            AssertOneWarningNamingTheModule();
        }

        [Test]
        public void On_thread_callbacks_log_no_thread_warning()
        {
            _bridge.RaiseRevenuePaid(Paid());
            _bridge.RaiseRevenuePaid(Paid());

            Assert.That(Records, Has.Length.EqualTo(2));
            Assert.That(_log.Warns, Is.Empty);
        }

        // ── never throws ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public void A_throwing_Track_is_a_warning_never_a_throw()
        {
            var analytics = new ThrowingAnalytics();
            var bridge = new FakeMaxBridge();
            using (new MaxRevenueReporter(bridge, analytics, _config, _log))
            {
                Assert.DoesNotThrow(() => bridge.RaiseRevenuePaid(Paid()));
            }

            Assert.That(analytics.TrackCalls, Is.EqualTo(1));
            AssertOneWarningNamingTheModule();
        }

        [Test]
        public void A_throwing_config_read_is_a_warning_and_the_record_is_still_tracked()
        {
            var bridge = new FakeMaxBridge();
            using (new MaxRevenueReporter(bridge, _analytics, new ThrowingConfig(), _log))
            {
                Assert.DoesNotThrow(() => bridge.RaiseRevenuePaid(Paid()));
            }

            Assert.That(Records, Has.Length.EqualTo(1), "absent ⇒ on: a faulty config layer loses no revenue");
            AssertOneWarningNamingTheModule();
        }

        // ── lifetime ───────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void The_constructor_subscribes_and_Dispose_unsubscribes_idempotently()
        {
            Assert.That(_bridge.RevenueSubscribers, Is.EqualTo(1));

            _reporter.Dispose();
            _reporter.Dispose();

            Assert.That(_bridge.RevenueSubscribers, Is.Zero);
            _bridge.RaiseRevenuePaid(Paid());
            Assert.That(_analytics.Tracked, Is.Empty);
        }

        [Test]
        public void The_constructor_rejects_null_dependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new MaxRevenueReporter(null, _analytics, _config, _log));
            Assert.Throws<ArgumentNullException>(() => new MaxRevenueReporter(_bridge, null, _config, _log));
            Assert.Throws<ArgumentNullException>(() => new MaxRevenueReporter(_bridge, _analytics, null, _log));
            Assert.Throws<ArgumentNullException>(() => new MaxRevenueReporter(_bridge, _analytics, _config, null));
        }

        // ── the Sdk bridge and the installer, by source scan ───────────────────────────────────────────

        private static string Strip(string source)
            => Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline), @"//.*", "");

        private static string Source(params string[] parts)
            => Strip(File.ReadAllText(Path.Combine(new[] { TestSupport.ModuleRoot }.Concat(parts).ToArray())));

        [Test]
        public void MaxBridge_asks_for_main_thread_events_before_InitializeSdk_and_wires_both_revenue_callbacks()
        {
            var src = Source("Runtime", "Sdk", "MaxBridge.cs");
            var flag = Regex.Match(src, @"MaxSdkBase\.InvokeEventsOnUnityMainThread\s*=\s*true\s*;");
            var init = src.IndexOf("InitializeSdk(", StringComparison.Ordinal);

            Assert.That(flag.Success, Is.True, "the bridge sets InvokeEventsOnUnityMainThread = true");
            Assert.That(init, Is.GreaterThan(-1));
            Assert.That(flag.Index, Is.LessThan(init), "the flag is set before InitializeSdk");
            Assert.That(src, Does.Match(@"MaxSdkCallbacks\.Interstitial\.OnAdRevenuePaidEvent\s*\+="));
            Assert.That(src, Does.Match(@"MaxSdkCallbacks\.Rewarded\.OnAdRevenuePaidEvent\s*\+="));
            Assert.That(src, Does.Contain("OnRevenuePaid?.Invoke("));
        }

        [Test]
        public void The_installer_resolves_the_reporter_before_it_builds_the_ads_service()
        {
            var src = Source("Runtime", "MaxAdsInstaller.cs");
            var resolve = src.IndexOf("resolver.Resolve<MaxRevenueReporter>()", StringComparison.Ordinal);
            var service = src.IndexOf("new MaxAdsService(", StringComparison.Ordinal);

            Assert.That(src, Does.Contain("new MaxRevenueReporter("));
            Assert.That(resolve, Is.GreaterThan(-1), "the service factory resolves the reporter");
            Assert.That(resolve, Is.LessThan(service), "the reporter listens before any show can be issued");
        }
    }
}
