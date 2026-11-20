using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;

namespace ClassicSpins.PrototypeFramework.FirebaseRc
{
    /// <summary>
    /// The bridge when the Firebase SDK is not installed: refuses every fetch as a result and says so with the
    /// module id. The game keeps its bundled defaults; nothing pretends to be remote.
    /// </summary>
    public sealed class NullFirebaseRcBridge : IFirebaseRcBridge
    {
        private readonly ILog _log;

        public NullFirebaseRcBridge(ILog log) => _log = log;

        public UniTask<IReadOnlyDictionary<string, string>> FetchAndActivateAsync(long minimumFetchIntervalMs, CancellationToken ct)
        {
            _log?.Warn($"[{FirebaseKeys.ModuleId}] refused fetch: com.google.firebase.remote-config is not installed (PF_FIREBASE_RC unset).");
            return UniTask.FromResult<IReadOnlyDictionary<string, string>>(null);
        }
    }
}
