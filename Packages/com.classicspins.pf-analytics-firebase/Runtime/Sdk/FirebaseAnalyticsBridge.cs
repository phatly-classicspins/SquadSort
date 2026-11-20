using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Analytics.Firebase.Core;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.Sdk
{
    /// <summary>Thin shell only; vendor SDK 14.1.0 compilation and native behavior require the installed SDK.</summary>
    public sealed class FirebaseAnalyticsBridge : IFirebaseAnalyticsBridge
    {
        public void Start(Action<bool, string> completed) => StartAsync(completed).Forget();
        private static async UniTask StartAsync(Action<bool, string> completed)
        {
            bool ok = false;
            string error = null;
            try
            {
                var status = await global::Firebase.FirebaseApp.CheckAndFixDependenciesAsync();
                ok = status == global::Firebase.DependencyStatus.Available;
                if (!ok) error = status.ToString();
            }
            catch (Exception ex) { error = ex.Message; }
            await UniTask.SwitchToMainThread();
            try { completed?.Invoke(ok, error); } catch (Exception) { }
        }
        public bool SetConsent(bool analyticsStorage, bool adSignals) => Try(() =>
        {
            var ad = adSignals ? global::Firebase.Analytics.ConsentStatus.Granted : global::Firebase.Analytics.ConsentStatus.Denied;
            global::Firebase.Analytics.FirebaseAnalytics.SetConsent(
                new Dictionary<global::Firebase.Analytics.ConsentType, global::Firebase.Analytics.ConsentStatus>
                {
                    [global::Firebase.Analytics.ConsentType.AnalyticsStorage] = analyticsStorage ? global::Firebase.Analytics.ConsentStatus.Granted : global::Firebase.Analytics.ConsentStatus.Denied,
                    [global::Firebase.Analytics.ConsentType.AdStorage] = ad,
                    [global::Firebase.Analytics.ConsentType.AdUserData] = ad,
                    [global::Firebase.Analytics.ConsentType.AdPersonalization] = ad,
                });
        });
        public bool SetCollectionEnabled(bool enabled) => Try(() => global::Firebase.Analytics.FirebaseAnalytics.SetAnalyticsCollectionEnabled(enabled));
        public bool SetUserProperty(string name, string value) => Try(() => global::Firebase.Analytics.FirebaseAnalytics.SetUserProperty(name, value));
        public bool SetUserId(string id) => Try(() => global::Firebase.Analytics.FirebaseAnalytics.SetUserId(id));
        public bool ResetAnalyticsData() => Try(() => global::Firebase.Analytics.FirebaseAnalytics.ResetAnalyticsData());
        public bool LogEvent(string name, IReadOnlyList<KeyValuePair<string, object>> parameters) => Try(() =>
        {
            var result = new global::Firebase.Analytics.Parameter[parameters.Count];
            for (int i = 0; i < result.Length; i++)
            {
                var p = parameters[i];
                if (p.Value is long integer) result[i] = new global::Firebase.Analytics.Parameter(p.Key, integer);
                else if (p.Value is double number) result[i] = new global::Firebase.Analytics.Parameter(p.Key, number);
                else if (p.Value is string text) result[i] = new global::Firebase.Analytics.Parameter(p.Key, text);
                else throw new ArgumentException("Unsupported analytics wire value");
            }
            global::Firebase.Analytics.FirebaseAnalytics.LogEvent(name, result);
        });
        private static bool Try(Action call) { try { call(); return true; } catch (Exception) { return false; } }
    }
}
