using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust.HeadlessTests
{
    public sealed class NullAdjustAnalyticsBridgeTests
    {
        [TestCase(false)] [TestCase(true)] public void Every_operation_refuses_even_when_logger_throws(bool throws)
        {
            var log = new TestLog { Throws = throws }; var bridge = new NullAdjustAnalyticsBridge(log);
            Func<bool>[] calls = { () => bridge.ConfigureAdSignals(true), () => bridge.Start("app", "sandbox"),
                () => bridge.SetEnabled(false), () => bridge.TrackToken("token", Array.Empty<KeyValuePair<string, string>>()),
                () => bridge.TrackRevenue("applovin_max_sdk", 1, "USD", "n", "u", "p"), () => bridge.AddCallbackParameter("k", "v"),
                () => bridge.AddPartnerParameter("k", "v"), () => bridge.ForgetMe() };
            foreach (var call in calls) Assert.That(call(), Is.False);
            if (!throws) { Assert.That(log.Warnings, Has.Count.EqualTo(calls.Length)); Assert.That(log.Warnings.TrueForAll(w => w.Contains("pf-analytics-adjust") && w.Contains("SDK missing")), Is.True); }
        }
    }
}
