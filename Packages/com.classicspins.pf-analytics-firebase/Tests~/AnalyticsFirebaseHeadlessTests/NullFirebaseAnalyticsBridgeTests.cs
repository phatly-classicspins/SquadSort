using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.HeadlessTests
{
    [TestFixture]
    public sealed class NullFirebaseAnalyticsBridgeTests
    {
        [Test] public void Every_member_refuses_without_throwing_and_names_the_module()
        {
            var log = new TestLog(); var bridge = new NullFirebaseAnalyticsBridge(log);
            bool? started = null; string error = null;
            bridge.Start((ok, text) => { started = ok; error = text; });
            Assert.That(started, Is.False); Assert.That(error, Does.Contain("pf-analytics-firebase"));
            Assert.That(bridge.SetConsent(true, true), Is.False);
            Assert.That(bridge.SetCollectionEnabled(false), Is.False);
            Assert.That(bridge.LogEvent("wire", Array.Empty<KeyValuePair<string, object>>()), Is.False);
            Assert.That(bridge.SetUserProperty("property", "value"), Is.False);
            Assert.That(bridge.SetUserId("u"), Is.False);
            Assert.That(bridge.ResetAnalyticsData(), Is.False);
            Assert.That(log.Warnings, Has.Count.EqualTo(7));
            Assert.That(log.Warnings, Has.All.Contains("pf-analytics-firebase"));
        }
        [Test] public void Broken_logger_and_completion_never_escape()
        {
            var bridge = new NullFirebaseAnalyticsBridge(new TestLog { Throws = true });
            Assert.DoesNotThrow(() => bridge.Start((ok, text) => throw new Exception("callback")));
            Assert.DoesNotThrow(() => bridge.Start(null));
            Assert.That(bridge.SetConsent(true, true), Is.False); Assert.That(bridge.SetCollectionEnabled(true), Is.False);
            Assert.That(bridge.LogEvent(null, null), Is.False); Assert.That(bridge.SetUserProperty(null, null), Is.False);
            Assert.That(bridge.SetUserId(null), Is.False); Assert.That(bridge.ResetAnalyticsData(), Is.False);
        }
    }
}
