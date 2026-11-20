using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace ClassicSpins.PrototypeFramework.FirebaseRc
{
    /// <summary>
    /// The thin shell over Firebase Remote Config, in primitives only. The real one (<c>FirebaseRcBridge</c>)
    /// lives in the gated <c>Sdk</c> role; without the SDK the installer hands out <c>NullFirebaseRcBridge</c>.
    /// </summary>
    public interface IFirebaseRcBridge
    {
        /// <summary>
        /// Fetch (Firebase throttles to <paramref name="minimumFetchIntervalMs"/>), activate, and return every value
        /// that came from the server, by Firebase key. When the fetch itself fails the last activated values come
        /// back. <c>null</c> when Firebase is unavailable or refused. Never throws on purpose.
        /// </summary>
        UniTask<IReadOnlyDictionary<string, string>> FetchAndActivateAsync(long minimumFetchIntervalMs, CancellationToken ct);
    }

    /// <summary>What the installer asset holds.</summary>
    public sealed class FirebaseRcSettings
    {
        public FirebaseRcSettings(int minimumFetchIntervalMinutes)
            => MinimumFetchIntervalMs = System.Math.Max(0, minimumFetchIntervalMinutes) * 60_000L;

        public long MinimumFetchIntervalMs { get; }
    }

    /// <summary>Game key ⟷ Firebase parameter key. Firebase keys allow letters, digits and '_' only.</summary>
    public static class FirebaseKeys
    {
        public const string ModuleId = "com.classicspins.pf-firebase-rc";

        public static string For(string gameKey) => gameKey.Replace('.', '_');

        public static bool IsLegal(string firebaseKey)
        {
            if (string.IsNullOrEmpty(firebaseKey) || firebaseKey.Length > 256) return false;
            char c0 = firebaseKey[0];
            if (!(c0 == '_' || (c0 >= 'a' && c0 <= 'z') || (c0 >= 'A' && c0 <= 'Z'))) return false;
            foreach (char c in firebaseKey)
                if (!(c == '_' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))) return false;
            return true;
        }
    }
}
