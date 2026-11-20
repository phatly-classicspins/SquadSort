using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;

namespace ClassicSpins.PrototypeFramework.Iap.Unity.HeadlessTests
{
    /// <summary>A scriptable <see cref="IUnityIapBridge"/>: records every call, answers the fetches
    /// synchronously unless told not to, and lets a test raise every store callback by hand.</summary>
    internal sealed class FakeUnityIapBridge : IUnityIapBridge
    {
        /// <summary>What <see cref="InitializeServicesAsync"/> resolves (null ⇒ UGS initialized). Ignored while
        /// <see cref="HoldServices"/>; <see cref="ThrowOnServices"/> throws instead.</summary>
        public string ServicesError;
        public bool HoldServices;
        public bool ThrowOnServices;
        public readonly UniTaskCompletionSource<string> PendingServices = new UniTaskCompletionSource<string>();

        /// <summary>What <see cref="ConnectAsync"/> resolves (null ⇒ connected). Ignored while <see cref="HoldConnect"/>.</summary>
        public string ConnectError;
        public bool HoldConnect;
        public readonly UniTaskCompletionSource<string> PendingConnect = new UniTaskCompletionSource<string>();

        /// <summary>Answer <see cref="FetchProducts"/> with every requested id priced at <see cref="Price"/>.</summary>
        public bool AutoAnswerProducts = true;
        public string Price = "$1.99";
        /// <summary>Ids the store reports unavailable.</summary>
        public readonly HashSet<string> Unavailable = new HashSet<string>();
        /// <summary>Non-null ⇒ <see cref="FetchProducts"/> answers with a fetch failure instead.</summary>
        public string ProductsFetchError;

        /// <summary>Answer <see cref="FetchPurchases"/> with <see cref="OwnedNonConsumables"/> (and re-raise
        /// <see cref="PendingAtStore"/> through <see cref="PurchasePending"/>, like the real bridge).</summary>
        public bool AutoAnswerPurchases = true;
        public readonly List<string> OwnedNonConsumables = new List<string>();
        public readonly List<(string tx, string product)> PendingAtStore = new List<(string, string)>();
        public string PurchasesFetchError;

        /// <summary>Non-null ⇒ <see cref="RestoreTransactions"/> answers at once with (Ok, Error).</summary>
        public (bool Ok, string Error)? AppleRestoreAnswer = (true, null);

        public int ServicesCalls;
        public int ConnectCalls;
        /// <summary>The bridge steps in call order ("services", "connect").</summary>
        public readonly List<string> Steps = new List<string>();
        public readonly List<string[]> FetchProductsIds = new List<string[]>();
        public readonly List<bool[]> FetchProductsNonConsumable = new List<bool[]>();
        public int FetchPurchasesCalls;
        public readonly List<string> Purchases = new List<string>();
        public readonly List<string> Confirms = new List<string>();
        public int RestoreTransactionsCalls;
        public bool ThrowOnPurchase;

        public event Action<UnityIapStoreProduct[]> ProductsFetched;
        public event Action<string> ProductsFetchFailed;
        public event Action<string, string> PurchasePending;
        public event Action<string, string, string> PurchaseFailed;
        public event Action<string> PurchaseDeferred;
        public event Action<string, bool, string> PurchaseConfirmed;
        public event Action<string[]> PurchasesFetched;
        public event Action<string> PurchasesFetchFailed;

        public UniTask<string> InitializeServicesAsync()
        {
            ServicesCalls++;
            Steps.Add("services");
            if (ThrowOnServices) throw new InvalidOperationException("UGS exploded");
            return HoldServices ? PendingServices.Task : UniTask.FromResult(ServicesError);
        }

        public UniTask<string> ConnectAsync()
        {
            ConnectCalls++;
            Steps.Add("connect");
            return HoldConnect ? PendingConnect.Task : UniTask.FromResult(ConnectError);
        }

        public void FetchProducts(string[] productIds, bool[] nonConsumable)
        {
            FetchProductsIds.Add(productIds);
            FetchProductsNonConsumable.Add(nonConsumable);
            if (!AutoAnswerProducts) return;
            if (ProductsFetchError != null) { ProductsFetchFailed?.Invoke(ProductsFetchError); return; }
            RaiseProductsFetched(productIds.Select(id => new UnityIapStoreProduct(id, Price, !Unavailable.Contains(id))).ToArray());
        }

        public void FetchPurchases()
        {
            FetchPurchasesCalls++;
            if (!AutoAnswerPurchases) return;
            if (PurchasesFetchError != null) { PurchasesFetchFailed?.Invoke(PurchasesFetchError); return; }
            PurchasesFetched?.Invoke(OwnedNonConsumables.ToArray());
            foreach (var (tx, product) in PendingAtStore.ToArray()) PurchasePending?.Invoke(tx, product);
        }

        public void Purchase(string productId)
        {
            Purchases.Add(productId);
            if (ThrowOnPurchase) throw new InvalidOperationException("store exploded");
        }

        public void ConfirmPurchase(string transactionId)
        {
            Confirms.Add(transactionId);
            PendingAtStore.RemoveAll(p => p.tx == transactionId);
        }

        public void RestoreTransactions(Action<bool, string> onCompleted)
        {
            RestoreTransactionsCalls++;
            if (AppleRestoreAnswer is { } answer) onCompleted(answer.Ok, answer.Error);
        }

        public void RaiseProductsFetched(UnityIapStoreProduct[] products) => ProductsFetched?.Invoke(products);
        public void RaiseProductsFetchFailed(string reason) => ProductsFetchFailed?.Invoke(reason);
        public void RaisePending(string tx, string product) => PurchasePending?.Invoke(tx, product);
        public void RaiseFailed(string product, string reason, string details = "d") => PurchaseFailed?.Invoke(product, reason, details);
        public void RaiseDeferred(string product) => PurchaseDeferred?.Invoke(product);
        public void RaiseConfirmed(string tx, bool ok, string reason) => PurchaseConfirmed?.Invoke(tx, ok, reason);
        public void RaisePurchasesFetched(params string[] owned) => PurchasesFetched?.Invoke(owned);
        public void RaisePurchasesFetchFailed(string reason) => PurchasesFetchFailed?.Invoke(reason);
    }

    /// <summary>A delay the test elapses by hand; cancellation cancels it.</summary>
    internal sealed class FakeDelay
    {
        public sealed class Call
        {
            public TimeSpan Delay;
            public UniTaskCompletionSource Source;
            public bool IsPending => Source.Task.Status == UniTaskStatus.Pending;
        }

        public readonly List<Call> Calls = new List<Call>();

        public Func<TimeSpan, CancellationToken, UniTask> Func => Invoke;

        public UniTask Invoke(TimeSpan delay, CancellationToken ct)
        {
            var call = new Call { Delay = delay, Source = new UniTaskCompletionSource() };
            Calls.Add(call);
            if (ct.IsCancellationRequested) call.Source.TrySetCanceled(ct);
            else ct.Register(() => call.Source.TrySetCanceled(ct));
            return call.Source.Task;
        }

        public int Pending => Calls.Count(c => c.IsPending);

        public void ElapsePending()
        {
            foreach (var c in Calls.Where(c => c.IsPending).ToArray()) c.Source.TrySetResult();
        }
    }

    internal sealed class RecordingAnalytics : IAnalytics
    {
        public readonly List<GameEvent> Events = new List<GameEvent>();
        public void Track(GameEvent evt) => Events.Add(evt);
        public void SetUserProperty(UserPropKey prop, string value) { }

        public IEnumerable<T> Of<T>() where T : GameEvent => Events.OfType<T>();
    }

    internal sealed class RecordingLog : ILog
    {
        public readonly List<string> Infos = new List<string>();
        public readonly List<string> Warns = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) => Warns.Add(message);
        public void Error(string message) => Errors.Add(message);
    }

    internal sealed class ListCatalog : ICatalogSource
    {
        private readonly IReadOnlyList<ProductInfo> _products;
        public ListCatalog(params ProductInfo[] products) => _products = products;
        public IReadOnlyList<ProductInfo> LoadCatalog() => _products;
    }

    internal sealed class StubClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
        public UniTask<bool> SyncServerTimeAsync(CancellationToken ct) => UniTask.FromResult(true);
        public bool IsTrusted => true;
    }

    internal sealed class EmptyConfig : IGameConfig
    {
        private readonly ObservableSource<Unit> _changed = new ObservableSource<Unit>();
        public T Get<T>(ConfigKey<T> key) => default;
        public UniTask RefreshRemoteAsync(CancellationToken ct) => UniTask.CompletedTask;
        public IObservable<Unit> OnChanged => _changed;
    }

    /// <summary>The real economy behind the real <see cref="GrantInterpreter"/>, so a test reads what a
    /// purchase actually granted.</summary>
    internal sealed class Economy
    {
        public readonly FakeWallet Wallet = new FakeWallet();
        public readonly FakeEntitlementService Entitlements = new FakeEntitlementService(new StubClock());
        public readonly FakeInventory Inventory = new FakeInventory(new EmptyConfig(), new RecordingLog());
        public readonly GrantInterpreter Interpreter;

        public Economy(ILog log) => Interpreter = new GrantInterpreter(Wallet, Entitlements, Inventory, log);

        public long Coins => Wallet.Balance(new ResourceKey("coins"));
        public bool Has(string entitlement) => Entitlements.Has(new EntitlementKey(entitlement));
    }

    internal static class Catalog
    {
        public const string Gold = "com.brainbucks.sushi.sort.gold_1k";
        public const string RemoveAds = "com.brainbucks.sushi.sort.remove_ads";
        public const string Starter = "com.brainbucks.sushi.sort.starter_pack";
        public const string Vip = "com.brainbucks.sushi.sort.vip_7d";
        public const string Off = "com.brainbucks.sushi.sort.disabled";

        public static ProductInfo GoldPack() => new ProductInfo(Gold, new[] { new GrantSpec(GrantKinds.Coin, "coins", 1000) });

        public static ProductInfo RemoveAdsProduct() => new ProductInfo(RemoveAds,
            new[] { new GrantSpec(GrantKinds.Entitlement, "remove_ads") }, Kind: ProductKind.NonConsumable);

        public static ProductInfo StarterPack() => new ProductInfo(Starter,
            new[] { new GrantSpec(GrantKinds.Entitlement, "starter_pack"), new GrantSpec(GrantKinds.Coin, "coins", 500) },
            Kind: ProductKind.NonConsumable);

        /// <summary>A non-consumable time-grant — re-applied by an explicit restore only.</summary>
        public static ProductInfo VipWeek() => new ProductInfo(Vip,
            new[] { new GrantSpec(GrantKinds.Entitlement, "vip", Days: 7) }, Kind: ProductKind.NonConsumable);

        public static ProductInfo Disabled() => new ProductInfo(Off,
            new[] { new GrantSpec(GrantKinds.Coin, "coins", 5) }, Disabled: true);

        public static ProductInfo[] Default() => new[] { GoldPack(), RemoveAdsProduct(), StarterPack(), Disabled() };
    }

    internal static class TestSupport
    {
        /// <summary>The module package root (baked in by the csproj).</summary>
        public static string ModuleRoot => Metadata("PfModuleRoot");

        /// <summary>The framework package root the Domain/Application sources were linked from.</summary>
        public static string FrameworkRoot => Metadata("PfFrameworkPackage");

        private static string Metadata(string key)
        {
            var value = typeof(TestSupport).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Single(a => a.Key == key).Value;
            return System.IO.Path.GetFullPath(value);
        }
    }
}
