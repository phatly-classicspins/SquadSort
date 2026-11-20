using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.HeadlessTests
{
    /// <summary>
    /// The <c>IapInit</c> replacement. <c>BootGraph.Reconcile</c> enforces <c>Id</c> and <c>Provides</c>, not
    /// <c>Required</c> — so the module pins all three here and fails in its own gate, not at a consumer's boot.
    /// </summary>
    [TestFixture]
    public sealed class UnityIapInitNodeTests
    {
        private FakeUnityIapBridge _bridge;
        private RecordingLog _log;
        private UnityIapPurchaseService _service;
        private UnityIapInitNode _node;

        [SetUp]
        public void SetUp()
        {
            _bridge = new FakeUnityIapBridge();
            _log = new RecordingLog();
            var economy = new Economy(_log);
            _service = new UnityIapPurchaseService(_bridge, new ListCatalog(Catalog.Default()), economy.Interpreter,
                economy.Entitlements, new RecordingAnalytics(), _log, UnityIapPlatform.Android, new FakeDelay().Func);
            _node = new UnityIapInitNode(_service, _log);
        }

        [Test]
        public void It_has_the_replaced_nodes_id_requires_and_provides_and_is_never_required()
        {
            Assert.Multiple(() =>
            {
                Assert.That(_node.Id, Is.EqualTo("IapInit"));
                Assert.That(UnityIapInitNode.NodeId, Is.EqualTo("IapInit"));
                Assert.That(_node.Requires, Is.EquivalentTo(new[] { BootCaps.ConfigAvailable, BootCaps.ConsentResolved }));
                Assert.That(_node.Provides, Is.EquivalentTo(new[] { BootCaps.IapReady }));
                Assert.That(_node.Required, Is.False, "a dead store never blocks boot");
                Assert.That(_node.Predicate, Is.Null);
                Assert.That(_node.SoftTimeout, Is.EqualTo(TimeSpan.Zero));
                Assert.That(_node.HardTimeout, Is.EqualTo(TimeSpan.Zero));
            });
        }

        [Test]
        public void It_matches_the_framework_node_it_replaces()
        {
            // The framework's IapInitNode is Infrastructure (not compiled here), so its shape is read as text.
            var src = System.IO.File.ReadAllText(System.IO.Path.Combine(TestSupport.FrameworkRoot,
                "Runtime/Infrastructure/Iap/IapInitNode.cs"));
            Assert.Multiple(() =>
            {
                Assert.That(src, Does.Contain("public string Id => \"IapInit\";"));
                Assert.That(src, Does.Contain("BootCaps.ConfigAvailable,"));
                Assert.That(src, Does.Contain("BootCaps.ConsentResolved,"));
                Assert.That(src, Does.Contain("new[] { BootCaps.IapReady }"));
            });
        }

        [Test]
        public async Task A_healthy_store_succeeds()
        {
            Assert.That(await _node.RunAsync(null, CancellationToken.None).AsTask(), Is.EqualTo(BootNodeResult.Succeeded));
            Assert.That(_service.IsInitialized, Is.True);
        }

        [Test]
        public async Task A_store_that_fails_degrades_and_never_throws()
        {
            _bridge.ConnectError = "offline";
            Assert.That(await _node.RunAsync(null, CancellationToken.None).AsTask(), Is.EqualTo(BootNodeResult.Degraded));
            Assert.That(_log.Warns.Any(w => w.Contains("pf-iap-unity") && w.Contains("offline")), Is.True);
        }

        [Test]
        public async Task A_rerun_never_reconnects_a_healthy_store()
        {
            await _node.RunAsync(null, CancellationToken.None).AsTask();
            await _node.RunAsync(null, CancellationToken.None).AsTask();
            Assert.That(_bridge.ConnectCalls, Is.EqualTo(1));
        }

        [Test]
        public void A_cancellation_is_rethrown_for_the_scheduler()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Assert.That(async () => await _node.RunAsync(null, cts.Token).AsTask(), Throws.InstanceOf<OperationCanceledException>());
            }
        }

        [Test]
        public void It_replaces_through_BootNodeOverride()
        {
            var o = BootNodeOverride.Replace(UnityIapInitNode.NodeId, _node);
            Assert.That(o.Kind, Is.EqualTo(BootNodeOverride.OverrideKind.Replace));
            Assert.That(o.TargetId, Is.EqualTo("IapInit"));
            Assert.That(o.Replacement, Is.SameAs(_node));
        }
    }
}
