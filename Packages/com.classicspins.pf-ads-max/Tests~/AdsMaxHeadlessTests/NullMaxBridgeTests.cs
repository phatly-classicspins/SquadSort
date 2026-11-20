using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace ClassicSpins.PrototypeFramework.Ads.Max.HeadlessTests
{
    /// <summary>
    /// The refusing bridge the installer falls back to when <c>PF_MAX</c> is unsatisfied: every member
    /// refuses, logs the module by name and never throws.
    /// </summary>
    [TestFixture]
    public sealed class NullMaxBridgeTests
    {
        private RecordingLog _log;
        private NullMaxBridge _bridge;

        [SetUp]
        public void SetUp()
        {
            _log = new RecordingLog();
            _bridge = new NullMaxBridge(_log);
        }

        private void AssertLoggedOnce()
        {
            Assert.That(_log.Warns, Has.Count.EqualTo(1));
            Assert.That(_log.Warns.Single(), Does.Contain("pf-ads-max"));
        }

        [Test]
        public void Is_never_initialized() => Assert.That(_bridge.IsInitialized, Is.False);

        [Test]
        public async Task InitializeAsync_resolves_false_and_logs()
        {
            Assert.That(await _bridge.InitializeAsync().AsTask(), Is.False);
            AssertLoggedOnce();
        }

        [Test]
        public void IsReady_is_false_and_logs()
        {
            Assert.That(_bridge.IsReady("u", true), Is.False);
            AssertLoggedOnce();
        }

        [Test]
        public void Load_is_a_logged_no_op()
        {
            Assert.DoesNotThrow(() => _bridge.Load("u", false));
            AssertLoggedOnce();
        }

        [Test]
        public void Show_is_a_logged_no_op()
        {
            Assert.DoesNotThrow(() => _bridge.Show("u", true, "p"));
            AssertLoggedOnce();
        }

        [Test]
        public void SetVerboseLogging_is_a_logged_no_op()
        {
            Assert.DoesNotThrow(() => _bridge.SetVerboseLogging(true));
            AssertLoggedOnce();
        }

        [Test]
        public void SetTestDeviceAdvertisingIds_is_a_logged_no_op()
        {
            Assert.DoesNotThrow(() => _bridge.SetTestDeviceAdvertisingIds(new[] { "a" }));
            AssertLoggedOnce();
        }

        [Test]
        public void ShowMediationDebugger_is_a_logged_no_op()
        {
            Assert.DoesNotThrow(() => _bridge.ShowMediationDebugger());
            AssertLoggedOnce();
            Assert.That(_log.Warns.Single(), Does.Contain("ShowMediationDebugger"));
        }

        [Test]
        public void ShowCreativeDebugger_is_a_logged_no_op()
        {
            Assert.DoesNotThrow(() => _bridge.ShowCreativeDebugger());
            AssertLoggedOnce();
            Assert.That(_log.Warns.Single(), Does.Contain("ShowCreativeDebugger"));
        }

        [Test]
        public void HasSupportedCmp_is_false_and_logs_nothing()
        {
            Assert.That(_bridge.HasSupportedCmp, Is.False);
            Assert.That(_log.Warns, Is.Empty, "a state read, like IsInitialized");
        }

        [Test]
        public void ShowCmpForExistingUser_logs_and_completes_with_a_refusal_message()
        {
            var calls = 0;
            string received = null;
            Assert.DoesNotThrow(() => _bridge.ShowCmpForExistingUser(e => { calls++; received = e; }));
            AssertLoggedOnce();
            Assert.That(_log.Warns.Single(), Does.Contain("ShowCmpForExistingUser"));
            Assert.That(calls, Is.EqualTo(1), "completes, so nothing awaiting it can hang");
            Assert.That(received, Is.Not.Null.And.Not.Empty, "an error message, never success");
            Assert.That(received, Does.Contain("pf-ads-max"));
            Assert.DoesNotThrow(() => _bridge.ShowCmpForExistingUser(null), "a null callback is tolerated");
        }

        [Test]
        public void Events_accept_subscribers_without_throwing()
        {
            Assert.DoesNotThrow(() =>
            {
                _bridge.OnLoaded += _ => { };
                _bridge.OnDisplayed += _ => { };
                _bridge.OnHidden += _ => { };
                _bridge.OnRewarded += _ => { };
                _bridge.OnDisplayFailed += (_, __) => { };
                _bridge.OnLoadFailed += (_, __) => { };
                _bridge.OnRevenuePaid += _ => { };
                _bridge.OnHidden -= _ => { };
            });
        }
    }
}
