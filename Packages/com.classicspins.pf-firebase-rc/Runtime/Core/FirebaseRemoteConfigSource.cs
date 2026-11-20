using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;

namespace ClassicSpins.PrototypeFramework.FirebaseRc
{
    /// <summary>
    /// Firebase Remote Config as the framework's <see cref="IRemoteConfigSource"/>. The framework's <c>RemoteFetch</c>
    /// boot node calls <see cref="FetchAsync"/>; the payload goes through the framework's merge and cache like any
    /// other remote layer.
    /// </summary>
    /// <remarks>
    /// <para><b>Keys.</b> The game's keys are the ones it ships defaults for (<see cref="ConfigDefaults"/>). Each is
    /// read from its Firebase spelling (<see cref="FirebaseKeys.For"/>); a Firebase parameter that names no game key
    /// is ignored and reported once, so a typo in the console is visible instead of silently doing nothing.</para>
    /// <para><b>Not modified.</b> The ETag is a hash of the mapped values, so an unchanged console answers
    /// <c>null</c> and the framework skips the cache write and the change event.</para>
    /// <para><b>Failure keeps what the player has.</b> A refused or failed fetch answers <c>null</c> and is logged;
    /// the framework keeps its current values (bundled defaults, or the cached last fetch).</para>
    /// </remarks>
    public sealed class FirebaseRemoteConfigSource : IRemoteConfigSource
    {
        private readonly IFirebaseRcBridge _bridge;
        private readonly FirebaseRcSettings _settings;
        private readonly Dictionary<string, string> _gameKeyByFirebaseKey;
        private readonly ILog _log;
        private bool _reportedUnknown;

        public FirebaseRemoteConfigSource(IFirebaseRcBridge bridge, FirebaseRcSettings settings, ConfigDefaults defaults, ILog log)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            if (defaults == null) throw new ArgumentNullException(nameof(defaults));

            _gameKeyByFirebaseKey = new Dictionary<string, string>();
            foreach (var key in defaults.Values.Keys)
            {
                string fk = FirebaseKeys.For(key);
                if (!FirebaseKeys.IsLegal(fk))
                    _log.Warn($"[{FirebaseKeys.ModuleId}] config key '{key}' has no legal Firebase spelling; it stays on its default.");
                else if (_gameKeyByFirebaseKey.TryGetValue(fk, out var other))
                    _log.Warn($"[{FirebaseKeys.ModuleId}] '{key}' and '{other}' are both '{fk}' on Firebase; '{key}' stays on its default.");
                else
                    _gameKeyByFirebaseKey[fk] = key;
            }
        }

        public async UniTask<RemoteConfigPayload> FetchAsync(RemoteFetchContext ctx, CancellationToken ct)
        {
            IReadOnlyDictionary<string, string> remote;
            try
            {
                remote = await _bridge.FetchAndActivateAsync(_settings.MinimumFetchIntervalMs, ct);
            }
            catch (OperationCanceledException)
            {
                throw;   // the boot node's hard timeout owns cancellation
            }
            catch (Exception ex)
            {
                _log.Warn($"[{FirebaseKeys.ModuleId}] fetch failed: {ex.Message}");
                return null;
            }

            if (remote == null)
            {
                _log.Warn($"[{FirebaseKeys.ModuleId}] no remote values (Firebase unavailable); keeping current config.");
                return null;
            }

            var values = Map(remote);
            if (values.Count == 0) return null;   // nothing published yet: the defaults already are the config

            string etag = ETagOf(values);
            if (ctx != null && ctx.KnownETag == etag) return null;
            _log.Info($"[{FirebaseKeys.ModuleId}] {values.Count} remote value(s) applied.");
            return new RemoteConfigPayload(etag, 1, values);
        }

        private Dictionary<string, string> Map(IReadOnlyDictionary<string, string> remote)
        {
            var values = new Dictionary<string, string>();
            List<string> unknown = null;
            foreach (var kv in remote)
            {
                if (_gameKeyByFirebaseKey.TryGetValue(kv.Key, out var gameKey)) values[gameKey] = kv.Value ?? "";
                else (unknown ??= new List<string>()).Add(kv.Key);
            }
            if (unknown != null && !_reportedUnknown)
            {
                _reportedUnknown = true;
                _log.Warn($"[{FirebaseKeys.ModuleId}] ignored Firebase parameter(s) the game has no key for: {string.Join(", ", unknown.OrderBy(k => k, StringComparer.Ordinal))}");
            }
            return values;
        }

        /// <summary>FNV-1a over the sorted pairs: stable across runs and platforms.</summary>
        public static string ETagOf(IReadOnlyDictionary<string, string> values)
        {
            unchecked
            {
                ulong h = 14695981039346656037UL;
                foreach (var kv in values.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    foreach (char c in kv.Key) { h ^= c; h *= 1099511628211UL; }
                    h ^= 0x1F; h *= 1099511628211UL;
                    foreach (char c in kv.Value) { h ^= c; h *= 1099511628211UL; }
                    h ^= 0x1E; h *= 1099511628211UL;
                }
                return h.ToString("x16");
            }
        }
    }
}
