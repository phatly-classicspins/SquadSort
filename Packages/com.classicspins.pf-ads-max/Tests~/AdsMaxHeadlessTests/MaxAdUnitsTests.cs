using System;
using System.Linq;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;
using ClassicSpins.PrototypeFramework.Domain;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Ads.Max.HeadlessTests
{
    /// <summary>
    /// <see cref="MaxAdUnits"/>: the unit lookup order, the requested set, auto-load and the load-retry
    /// backoff — time driven by hand through <see cref="FakeDelay"/>.
    /// </summary>
    [TestFixture]
    public sealed class MaxAdUnitsTests
    {
        private static readonly AdPlacement P = new AdPlacement("p");

        private FakeMaxBridge _bridge;
        private DictConfig _config;
        private RecordingLog _log;
        private FakeDelay _delay;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeMaxBridge { Initialized = true };
            _config = new DictConfig();
            _log = new RecordingLog();
            _delay = new FakeDelay();
        }

        private MaxAdUnits Units(string platform = MaxAdUnits.Android)
            => new MaxAdUnits(_bridge, _config, _log, platform, _delay.Func);

        // ── lookup order ───────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Keys_run_platform_placement_then_placement_then_platform_format_then_format()
        {
            Assert.That(MaxAdUnits.UnitIdKeys("android", "p", rewarded: true), Is.EqualTo(new[]
            {
                "ads.max.android.p.unitId", "ads.max.p.unitId", "ads.max.android.rewarded.unitId", "ads.max.rewarded.unitId",
            }));
            Assert.That(MaxAdUnits.UnitIdKeys(null, "p", rewarded: false), Is.EqualTo(new[]
            {
                "ads.max.p.unitId", "ads.max.interstitial.unitId",
            }), "no platform ⇒ the platform tiers are skipped");
        }

        [TestCase("ads.max.android.p.unitId", "A")]
        [TestCase("ads.max.p.unitId", "B")]
        [TestCase("ads.max.android.interstitial.unitId", "C")]
        [TestCase("ads.max.interstitial.unitId", "D")]
        public void The_first_non_empty_tier_wins(string firstSetKey, string expected)
        {
            var all = new[]
            {
                ("ads.max.android.p.unitId", "A"), ("ads.max.p.unitId", "B"),
                ("ads.max.android.interstitial.unitId", "C"), ("ads.max.interstitial.unitId", "D"),
            };
            var from = Array.FindIndex(all, t => t.Item1 == firstSetKey);
            for (var i = 0; i < all.Length; i++) _config.Set(all[i].Item1, i < from ? "  " : all[i].Item2);

            Assert.That(Units().Resolve(P), Is.EqualTo(expected));
        }

        [Test]
        public void The_format_comes_from_the_frameworks_rewarded_flag()
        {
            _config.Set("ads.max.rewarded.unitId", "R").Set("ads.max.interstitial.unitId", "I");
            var units = Units();
            Assert.That(units.Resolve(P), Is.EqualTo("I"), "absent ads.p.rewarded ⇒ interstitial");
            _config.Set("ads.p.rewarded", 1);
            Assert.That(units.Resolve(P), Is.EqualTo("R"));
        }

        [Test]
        public void Without_a_platform_the_platform_tiers_are_ignored()
        {
            _config.Set("ads.max.android.p.unitId", "A").Set("ads.max.ios.p.unitId", "I");
            Assert.That(Units(platform: null).Resolve(P), Is.Null);
            Assert.That(Units(platform: MaxAdUnits.Ios).Resolve(P), Is.EqualTo("I"));
        }

        [Test]
        public void A_blank_placement_never_falls_through_to_a_format_default()
        {
            // A format default would otherwise show for a placement nobody named — the service's existing
            // "no unit id" refusal path is the only correct outcome here.
            _config.Set("ads.max.rewarded.unitId", "R").Set("ads.max.interstitial.unitId", "I");
            var units = Units();
            Assert.That(units.Resolve(new AdPlacement("")), Is.Null);
            Assert.That(units.Resolve(new AdPlacement("   ")), Is.Null);
            Assert.That(units.Resolve(new AdPlacement(null)), Is.Null);
        }

        [Test]
        public void The_placement_tier_is_the_pre_v2_key_unchanged()
            => Assert.That(MaxAdUnits.UnitIdKey("p"), Is.EqualTo(MaxAdsService.UnitIdKey(P)));

        // ── dev-config helpers ─────────────────────────────────────────────────────────────────────────

        [TestCase(" a, ,b ", new[] { "a", "b" })]
        [TestCase("a,,b,", new[] { "a", "b" })]
        [TestCase("single", new[] { "single" })]
        [TestCase(" , ", new string[0])]
        [TestCase("", new string[0])]
        [TestCase(null, new string[0])]
        public void SplitList_trims_and_drops_empties(string value, string[] expected)
            => Assert.That(MaxAdUnits.SplitList(value), Is.EqualTo(expected));

        [Test]
        public void TryReadInt_tells_absent_from_zero()
        {
            int v;
            Assert.That(MaxAdUnits.TryReadInt(_config, "k", out v), Is.False);
            _config.Set("k", 0);
            Assert.That(MaxAdUnits.TryReadInt(_config, "k", out v), Is.True);
            Assert.That(v, Is.Zero);
            _config.Set("k", 7L);
            Assert.That(MaxAdUnits.TryReadInt(_config, "k", out v) && v == 7, Is.True, "a JSON long reads too");
        }

        [Test]
        public void TryReadInt_accepts_a_numeric_string_but_not_other_strings()
        {
            // ads.max.autoLog = "0" (a string "0") must still read as present/zero, not absent — otherwise
            // AutoLogEnabled's "absent ⇒ on" default would keep logging with the switch set to off.
            int v;
            _config.Set("k", "0");
            Assert.That(MaxAdUnits.TryReadInt(_config, "k", out v), Is.True, "a numeric string is not absent");
            Assert.That(v, Is.Zero);

            _config.Set("k", "7");
            Assert.That(MaxAdUnits.TryReadInt(_config, "k", out v) && v == 7, Is.True, "a numeric string reads too");

            _config.Set("k", " 7 ");
            Assert.That(MaxAdUnits.TryReadInt(_config, "k", out v) && v == 7, Is.True,
                "NumberStyles.Integer allows the surrounding whitespace");

            _config.Set("k", "7.5");
            Assert.That(MaxAdUnits.TryReadInt(_config, "k", out v), Is.False, "not a whole number");

            _config.Set("k", "seven");
            Assert.That(MaxAdUnits.TryReadInt(_config, "k", out v), Is.False, "a non-numeric string stays absent");
        }

        // ── auto-load ──────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void StartAutoLoad_loads_each_format_default_once()
        {
            _config.Set("ads.max.android.rewarded.unitId", "R").Set("ads.max.interstitial.unitId", "I");
            var units = Units();

            units.StartAutoLoad();
            units.StartAutoLoad();

            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { ("R", true), ("I", false) }));
            Assert.That(units.IsRequested("R") && units.IsRequested("I"), Is.True);
        }

        [Test]
        public void StartAutoLoad_does_nothing_while_the_bridge_is_not_initialized_but_warns()
        {
            _config.Set("ads.max.rewarded.unitId", "R");
            _bridge.Initialized = false;
            var units = Units();

            units.StartAutoLoad();
            Assert.That(_bridge.LoadCalls, Is.Empty);
            Assert.That(_log.Warns.Any(w => w.Contains("[pf-ads-max]")), Is.True, string.Join(" | ", _log.Warns));
        }

        [Test]
        public void A_request_before_init_is_recorded_and_loaded_at_StartAutoLoad()
        {
            _bridge.Initialized = false;
            var units = Units();
            units.Request("U", false);
            Assert.That(units.IsRequested("U"), Is.True);
            Assert.That(_bridge.LoadCalls, Is.Empty);

            _bridge.Initialized = true;
            units.StartAutoLoad();
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { ("U", false) }));
        }

        [Test]
        public void A_request_after_init_loads_at_once()
        {
            var units = Units();
            units.Request("U", true);
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { ("U", true) }));
        }

        [Test]
        public void A_second_request_after_init_but_before_StartAutoLoad_clears_LoadAtStart_so_it_is_not_loaded_twice()
        {
            // Request #1 before init sets LoadAtStart; request #2 after init (before StartAutoLoad runs) loads
            // directly and must clear LoadAtStart, or StartAutoLoad would load the same unit again.
            _bridge.Initialized = false;
            var units = Units();
            units.Request("U", false);
            Assert.That(_bridge.LoadCalls, Is.Empty);

            _bridge.Initialized = true;
            units.Request("U", false);
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { ("U", false) }), "the second request loads directly");

            units.StartAutoLoad();
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { ("U", false) }), "StartAutoLoad must not load it again");
        }

        [Test]
        public void A_later_request_of_the_same_unit_with_the_other_format_follows_the_latest_request_and_warns()
        {
            var units = Units();
            units.Request("U", true); // rewarded
            _bridge.LoadCalls.Clear();

            units.Request("U", false); // same unit id, now interstitial — a misconfiguration, but followed

            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { ("U", false) }), "the stored format now follows the latest Request");
            Assert.That(_log.Warns.Any(w => w.Contains("[pf-ads-max]") && w.Contains("U")), Is.True,
                string.Join(" | ", _log.Warns));

            _bridge.LoadCalls.Clear();
            _log.Warns.Clear();
            units.Request("U", false); // same format again — no flip, no warning
            Assert.That(_log.Warns, Is.Empty);
        }

        // ── retry / backoff ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Eight_failures_back_off_2_4_8_16_32_64_64_64_and_each_retry_loads_again()
        {
            var units = Units();
            units.Request("U", true);

            for (var i = 0; i < 8; i++)
            {
                _bridge.RaiseLoadFailed("U", "no fill");
                _delay.ElapseLast();
            }

            Assert.That(_delay.Seconds, Is.EqualTo(new[] { 2.0, 4, 8, 16, 32, 64, 64, 64 }));
            Assert.That(_bridge.LoadCalls, Has.Count.EqualTo(1 + 8), "the request plus one load per retry");
            Assert.That(_bridge.LoadCalls.All(c => c == ("U", true)), Is.True);
        }

        [Test]
        public void OnLoaded_resets_the_backoff()
        {
            var units = Units();
            units.Request("U", false);
            for (var i = 0; i < 3; i++) { _bridge.RaiseLoadFailed("U", "x"); _delay.ElapseLast(); }

            _bridge.RaiseLoaded("U");
            _bridge.RaiseLoadFailed("U", "x");

            Assert.That(_delay.Seconds, Is.EqualTo(new[] { 2.0, 4, 8, 2 }));
        }

        [Test]
        public void At_most_one_retry_is_pending_and_a_request_during_it_does_not_load_early()
        {
            var units = Units();
            units.Request("U", false);
            _bridge.LoadCalls.Clear();

            _bridge.RaiseLoadFailed("U", "x");
            _bridge.RaiseLoadFailed("U", "x");
            units.Request("U", false);

            Assert.That(_delay.Calls, Has.Count.EqualTo(1));
            Assert.That(_bridge.LoadCalls, Is.Empty);

            _delay.ElapseLast();
            Assert.That(_bridge.LoadCalls, Is.EqualTo(new[] { ("U", false) }));
        }

        [Test]
        public void A_unit_nobody_requested_is_never_retried()
        {
            Units();
            _bridge.RaiseLoadFailed("stranger", "x");
            Assert.That(_delay.Calls, Is.Empty);
        }

        [Test]
        public void No_retry_before_init()
        {
            _bridge.Initialized = false;
            var units = Units();
            units.Request("U", false);
            _bridge.RaiseLoadFailed("U", "x");
            Assert.That(_delay.Calls, Is.Empty);
        }

        [Test]
        public void Dispose_cancels_a_pending_retry_no_load_no_throw_and_stops_listening()
        {
            var units = Units();
            units.Request("U", false);
            _bridge.LoadCalls.Clear();
            _bridge.RaiseLoadFailed("U", "x");

            Assert.DoesNotThrow(() => units.Dispose());
            Assert.That(_delay.Calls.Single().IsCancelled, Is.True);
            Assert.That(_bridge.LoadCalls, Is.Empty);
            Assert.That(_bridge.LoadSubscribers, Is.Zero);
            Assert.DoesNotThrow(() => units.Dispose(), "idempotent");
        }

        [Test]
        public void BackoffFor_caps_at_64_seconds()
        {
            Assert.That(MaxAdUnits.BackoffFor(1), Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(MaxAdUnits.BackoffFor(6), Is.EqualTo(TimeSpan.FromSeconds(64)));
            Assert.That(MaxAdUnits.BackoffFor(50), Is.EqualTo(TimeSpan.FromSeconds(64)));
        }
    }
}
