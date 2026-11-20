using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.RemoteConfig;

namespace ClassicSpins.PrototypeFramework.FirebaseRc.Sdk
{
    /// <summary>
    /// The one runtime type that names the Firebase SDK. Compiled only when <c>PF_FIREBASE_RC</c> is armed by
    /// <c>com.google.firebase.remote-config</c> (versionDefines). Works in the Editor too (Firebase's desktop
    /// implementation reads the same config file), so play mode fetches the real console values.
    /// </summary>
    public sealed class FirebaseRcBridge : IFirebaseRcBridge
    {
        private DependencyStatus? _status;

        public async UniTask<IReadOnlyDictionary<string, string>> FetchAndActivateAsync(long minimumFetchIntervalMs, CancellationToken ct)
        {
            try
            {
                if (_status == null)
                    _status = await FirebaseApp.CheckAndFixDependenciesAsync().AsUniTask().AttachExternalCancellation(ct);
                if (_status != DependencyStatus.Available) return null;

                var rc = FirebaseRemoteConfig.DefaultInstance;
                await rc.SetConfigSettingsAsync(new ConfigSettings { MinimumFetchIntervalInMilliseconds = (ulong)minimumFetchIntervalMs })
                    .AsUniTask().AttachExternalCancellation(ct);
                try
                {
                    await rc.FetchAsync(TimeSpan.FromMilliseconds(minimumFetchIntervalMs)).AsUniTask().AttachExternalCancellation(ct);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { /* offline or throttled: fall through to what was fetched last time */ }
                await rc.ActivateAsync().AsUniTask().AttachExternalCancellation(ct);

                var values = new Dictionary<string, string>();
                foreach (var kv in rc.AllValues)
                    if (kv.Value.Source == ValueSource.RemoteValue) values[kv.Key] = kv.Value.StringValue;
                return values;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { return null; }
        }
    }

    /// <summary>Proof for the Setup Wizard and Doctor check 16 that the gated assembly compiled.</summary>
    internal static class PfModuleMarker_PF_FIREBASE_RC { }
}
