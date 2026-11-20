using System;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Iap.Unity.Core;

namespace ClassicSpins.PrototypeFramework.Iap.Unity
{
    /// <summary>
    /// The refusing bridge the installer registers when <c>PF_UNITY_IAP</c> is not armed — Unity IAP is absent,
    /// or older than the 5.0.0 floor. Every member refuses and logs a warning naming <c>pf-iap-unity</c>;
    /// nothing throws, and nothing is ever bought or granted.
    /// </summary>
    /// <remarks>
    /// <para><b>Why it exists at all.</b> If the installer were define-gated, removing the SDK would turn the
    /// installer asset into a missing script and the game would silently fall back to the framework's
    /// <c>FakePurchaseService</c> — which grants every purchase for free. With this bridge behind
    /// <see cref="UnityIapPurchaseService"/>, absence is loud: <c>IapInit</c> degrades, every
    /// <c>BuyAsync</c> resolves <see cref="PurchaseResult.Failed"/>, and the log names the module.</para>
    /// <para>Engine-free on purpose, so the module's headless test project compiles this one file alongside
    /// <c>Runtime/Core/**</c>.</para>
    /// </remarks>
    public sealed class NullUnityIapBridge : IUnityIapBridge
    {
        private const string Refusal =
            "[pf-iap-unity] Unity IAP is not installed (PF_UNITY_IAP is not armed: com.unity.purchasing >= 5.0.0 is not "
            + "resolved), so purchases are disabled — ";

        private readonly ILog _log;

        public NullUnityIapBridge(ILog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public UniTask<string> InitializeServicesAsync()
        {
            const string message = "InitializeServicesAsync refused. Install the module's requirements via Framework/Setup Wizard ▸ Modules.";
            _log.Warn(Refusal + message);
            return UniTask.FromResult("pf-iap-unity: Unity IAP is not installed — " + message);
        }

        public UniTask<string> ConnectAsync()
        {
            const string message = "ConnectAsync refused. Install the module's requirements via Framework/Setup Wizard ▸ Modules.";
            _log.Warn(Refusal + message);
            return UniTask.FromResult("pf-iap-unity: Unity IAP is not installed — " + message);
        }

        public void FetchProducts(string[] productIds, bool[] nonConsumable) => _log.Warn(Refusal + "FetchProducts refused.");

        public void FetchPurchases() => _log.Warn(Refusal + "FetchPurchases refused.");

        public void Purchase(string productId) => _log.Warn(Refusal + "Purchase('" + productId + "') refused.");

        public void ConfirmPurchase(string transactionId) => _log.Warn(Refusal + "ConfirmPurchase('" + transactionId + "') refused.");

        public void RestoreTransactions(Action<bool, string> onCompleted)
        {
            const string message = "RestoreTransactions refused.";
            _log.Warn(Refusal + message);
            // Complete anyway, so nothing awaiting the callback can hang.
            onCompleted?.Invoke(false, "pf-iap-unity: Unity IAP is not installed — " + message);
        }

        // Never raised: nothing can connect, fetch or sell. Explicit accessors keep the unused-event warning away.
        public event Action<UnityIapStoreProduct[]> ProductsFetched { add { } remove { } }
        public event Action<string> ProductsFetchFailed { add { } remove { } }
        public event Action<string, string> PurchasePending { add { } remove { } }
        public event Action<string, string, string> PurchaseFailed { add { } remove { } }
        public event Action<string> PurchaseDeferred { add { } remove { } }
        public event Action<string, bool, string> PurchaseConfirmed { add { } remove { } }
        public event Action<string[]> PurchasesFetched { add { } remove { } }
        public event Action<string> PurchasesFetchFailed { add { } remove { } }
    }
}
