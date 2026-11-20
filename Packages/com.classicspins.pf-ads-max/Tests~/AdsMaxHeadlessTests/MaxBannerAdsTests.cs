using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Ads.Max.Core;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Ads.Max.HeadlessTests
{
    [TestFixture]
    public sealed class MaxBannerAdsTests
    {
        private static readonly AdPlacement Banner = new AdPlacement("banner_gameplay");
        private static readonly AdPlacement Other = new AdPlacement("banner_home");
        private static readonly Func<TimeSpan, CancellationToken, UniTask> NoDelay = (_, __) => UniTask.CompletedTask;

        private FakeMaxBridge _bridge;
        private DictConfig _config;
        private RecordingLog _log;
        private SwitchGate _gate;

        /// <summary>An <see cref="IAdGate"/> that refuses the placements in <see cref="Refused"/> (remove-ads stand-in).</summary>
        private sealed class SwitchGate : IAdGate
        {
            public readonly HashSet<string> Refused = new HashSet<string>();
            public bool CanShow(AdPlacement placement) => !Refused.Contains(placement.Value);
            public void RecordShown(AdPlacement placement) { }
        }

        private MaxBannerAds Make(FakeMaxBridge bridge, string platform = MaxAdUnits.Android)
            => new MaxBannerAds(bridge, new MaxAdUnits(bridge, _config, _log, platform, NoDelay), _config, _gate, _log);

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeMaxBridge { Initialized = true };
            _config = new DictConfig().Set("ads.max.banner_gameplay.unitId", "unit-b").Set("ads.max.banner_home.unitId", "unit-h");
            _log = new RecordingLog();
            _gate = new SwitchGate();
        }

        [Test]
        public void A_placement_the_gate_refuses_is_never_shown_and_hides_a_shown_banner()
        {
            var banner = Make(_bridge);
            banner.Show(Banner);
            _gate.Refused.Add(Banner.Value);   // remove-ads bought while the banner is up
            banner.Show(Banner);
            banner.Show(Banner);
            Assert.That(_bridge.BannerCalls, Is.EqualTo(new[] { "show:unit-b", "hide:unit-b" }));
        }

        [Test]
        public void A_wish_before_init_that_the_gate_refuses_by_init_shows_nothing()
        {
            _bridge.Initialized = false;
            var banner = Make(_bridge);
            banner.Show(Banner);
            _gate.Refused.Add(Banner.Value);
            _bridge.RaiseInitialized();
            Assert.That(_bridge.BannerCalls, Is.Empty);
        }

        [Test]
        public void Show_shows_the_configured_unit_once_and_hide_hides_it_once()
        {
            var banner = Make(_bridge);
            banner.Show(Banner);
            banner.Show(Banner);
            banner.Hide();
            banner.Hide();
            Assert.That(_bridge.BannerCalls, Is.EqualTo(new[] { "show:unit-b", "hide:unit-b" }));
        }

        [Test]
        public void Another_placement_replaces_the_banner()
        {
            var banner = Make(_bridge);
            banner.Show(Banner);
            banner.Show(Other);
            Assert.That(_bridge.BannerCalls, Is.EqualTo(new[] { "show:unit-b", "hide:unit-b", "show:unit-h" }));
        }

        [Test]
        public void The_platform_key_wins_then_the_placement_then_the_banner_default()
        {
            _config.Set("ads.max.android.banner_gameplay.unitId", "android-b");
            Make(_bridge).Show(Banner);
            Assert.That(_bridge.BannerCalls, Is.EqualTo(new[] { "show:android-b" }));

            var fresh = new FakeMaxBridge { Initialized = true };
            _config = new DictConfig().Set("ads.max.ios.banner.unitId", "ios-default").Set("ads.max.interstitial.unitId", "inter");
            Make(fresh, MaxAdUnits.Ios).Show(Banner);
            Assert.That(fresh.BannerCalls, Is.EqualTo(new[] { "show:ios-default" }), "a banner never falls back to an interstitial unit");
        }

        [Test]
        public void A_wish_before_init_is_applied_on_init_and_a_hide_before_init_cancels_it()
        {
            _bridge.Initialized = false;
            var banner = Make(_bridge);
            banner.Show(Banner);
            Assert.That(_bridge.BannerCalls, Is.Empty);
            _bridge.RaiseInitialized();
            Assert.That(_bridge.BannerCalls, Is.EqualTo(new[] { "show:unit-b" }));

            var fresh = new FakeMaxBridge();
            var other = Make(fresh);
            other.Show(Banner);
            other.Hide();
            fresh.RaiseInitialized();
            Assert.That(fresh.BannerCalls, Is.Empty);
        }

        [Test]
        public void No_unit_id_shows_nothing_and_warns_once()
        {
            _config = new DictConfig();
            var banner = Make(_bridge);
            banner.Show(Banner);
            banner.Show(Banner);
            Assert.That(_bridge.BannerCalls, Is.Empty);
            Assert.That(_log.Warns.Count(w => w.Contains("pf-ads-max") && w.Contains("ads.max.banner_gameplay.unitId")), Is.EqualTo(1));
        }

        [Test]
        public void A_throwing_bridge_is_logged_never_thrown()
        {
            _bridge.ThrowOnBanner = true;
            Assert.DoesNotThrow(() => Make(_bridge).Show(Banner));
            Assert.That(_log.Warns.Any(w => w.Contains("pf-ads-max")), Is.True);
        }
    }
}
