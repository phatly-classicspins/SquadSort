using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.Core
{
    /// <summary>
    /// The Unity IAP replacement for the framework's <c>IapInit</c> boot node, installed with
    /// <see cref="BootNodeOverride.Replace"/>. Same <see cref="Id"/>, <see cref="Requires"/> and
    /// <see cref="Provides"/> as the node it replaces, and <see cref="Required"/> <c>false</c>: a store that
    /// will not connect degrades <c>IapReady</c> (still emitted) and the game runs on the baked catalog with
    /// "loading" prices, never an error screen.
    /// </summary>
    /// <remarks>
    /// What it adds over the framework node is bounded store work: UGS init, connect, product fetch and every
    /// rerun go through <see cref="UnityIapPurchaseService.InitializeAsync"/>, which waits at most
    /// <see cref="UnityIapPurchaseService.ServicesInitTimeout"/> + <see cref="UnityIapPurchaseService.ConnectTimeout"/>
    /// + <see cref="UnityIapPurchaseService.ProductFetchTimeout"/>
    /// on a realtime delay and runs once per process once it has succeeded — so a rerun from <c>IapReady</c>
    /// never reconnects a healthy store. Nothing interactive happens here: no timeout is cut over a player.
    /// </remarks>
    public sealed class UnityIapInitNode : IBootNode
    {
        /// <summary>The framework boot node this one replaces.</summary>
        public const string NodeId = "IapInit";

        private static readonly IReadOnlyList<BootCap> RequiresCaps = new[] { BootCaps.ConfigAvailable, BootCaps.ConsentResolved };
        private static readonly IReadOnlyList<BootCap> ProvidesCaps = new[] { BootCaps.IapReady };

        private readonly UnityIapPurchaseService _purchases;
        private readonly ILog _log;

        public UnityIapInitNode(UnityIapPurchaseService purchases, ILog log)
        {
            _purchases = purchases ?? throw new ArgumentNullException(nameof(purchases));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public string Id => NodeId;
        public IReadOnlyList<BootCap> Requires => RequiresCaps;
        public IReadOnlyList<BootCap> Provides => ProvidesCaps;
        public bool Required => false;
        public TimeSpan SoftTimeout => TimeSpan.Zero;
        public TimeSpan HardTimeout => TimeSpan.Zero;
        public Func<BootContext, bool> Predicate => null;
        public string ExclusiveGroup => "";
        public float Weight => 1f;

        public async UniTask<BootNodeResult> RunAsync(BootContext ctx, CancellationToken ct)
        {
            try
            {
                ct.ThrowIfCancellationRequested();
                await _purchases.InitializeAsync(ct);
                _log.Info("[pf-iap-unity] IapInit: store ready (catalog priced).");
                return BootNodeResult.Succeeded;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _log.Warn("[pf-iap-unity] IapInit: store init failed — IapReady degraded (still emitted); baked catalog "
                          + "with loading prices: " + ex.Message);
                return BootNodeResult.Degraded;
            }
        }
    }
}
