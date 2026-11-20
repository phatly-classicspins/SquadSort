using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>D-063 (features/online.md): the No Internet gate — held only when internet is required, Remove Ads is not owned and
    /// the device is offline; Retry looks again after at least 1.5 s.</summary>
    public class OnlineGateTests
    {
        sealed class FakeNetwork : INetworkStatus { public bool IsOnline { get; set; } = true; }

        sealed class Entitlements : IEntitlementService
        {
            readonly HashSet<EntitlementKey> _owned = new HashSet<EntitlementKey>();
            public bool Has(EntitlementKey key) => _owned.Contains(key);
            public void Grant(EntitlementKey key, GrantSource source, TimeSpan? duration = null) => _owned.Add(key);
            public void Revoke(EntitlementKey key, GrantSource source) => _owned.Remove(key);
        }

        FakeNetwork net; Entitlements ent; EconomyTuning tuning; OnlineGate gate;

        [SetUp]
        public void SetUp()
        {
            net = new FakeNetwork(); ent = new Entitlements(); tuning = new EconomyTuning();
            gate = new OnlineGate(net, ent, tuning);
        }

        [Test]
        public void Online_players_are_never_held()
        {
            Assert.That(gate.ShouldBlock, Is.False);
        }

        [Test]
        public void Offline_without_Remove_Ads_is_held()
        {
            net.IsOnline = false;
            Assert.That(gate.ShouldBlock, Is.True);
        }

        [Test]
        public void Remove_Ads_owners_play_offline()
        {
            net.IsOnline = false;
            ent.Grant(OnlineGate.RemoveAds, GrantSource.Reward);
            Assert.That(gate.ShouldBlock, Is.False);
            Assert.That(OnlineGate.RemoveAds, Is.EqualTo(new EntitlementKey("remove_ads")), "the key the framework's AdGate reads");
        }

        [Test]
        public void The_remote_switch_turns_the_gate_off()
        {
            net.IsOnline = false;
            tuning.RequireInternet = false;
            Assert.That(gate.ShouldBlock, Is.False);
        }

        [Test]
        public void The_check_interval_follows_remote_config_and_never_drops_below_a_second()
        {
            Assert.That(gate.CheckInterval, Is.EqualTo(TimeSpan.FromSeconds(3)));
            tuning.InternetCheckSeconds = 0;
            Assert.That(gate.CheckInterval, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(new EconomyTuning { InternetCheckSeconds = -5 }.Sanitize().InternetCheckSeconds, Is.EqualTo(1));
        }

        [Test]
        public async Task Retry_waits_at_least_one_and_a_half_seconds_then_looks_again()
        {
            var waits = new List<TimeSpan>();
            Task Delay(TimeSpan t, CancellationToken ct) { waits.Add(t); return Task.CompletedTask; }

            net.IsOnline = false;
            Assert.That(await gate.RetryAsync(Delay, CancellationToken.None), Is.False, "still offline: Retry comes back");
            net.IsOnline = true;
            Assert.That(await gate.RetryAsync(Delay, CancellationToken.None), Is.True, "back online: the popup closes");
            Assert.That(waits, Is.EqualTo(new[] { TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(1.5) }));
        }

        [Test]
        public void The_two_keys_are_remote_config_defaults()
        {
            var values = EconomyConfig.Defaults().Values;
            Assert.That(values["require_internet"], Is.EqualTo(true));
            Assert.That(values["internet_check_seconds"], Is.EqualTo(3));
        }
    }
}
