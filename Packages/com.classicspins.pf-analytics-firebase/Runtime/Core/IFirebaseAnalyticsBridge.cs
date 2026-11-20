using System;
using System.Collections.Generic;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase.Core
{
    /// <summary>Primitive-only SDK boundary. Start completion is dispatched to Unity's main thread by the provider.</summary>
    public interface IFirebaseAnalyticsBridge
    {
        void Start(Action<bool, string> completed);
        bool SetConsent(bool analyticsStorage, bool adSignals);
        bool SetCollectionEnabled(bool enabled);
        bool LogEvent(string name, IReadOnlyList<KeyValuePair<string, object>> parameters);
        bool SetUserProperty(string name, string value);
        bool SetUserId(string id);
        bool ResetAnalyticsData();
    }
}
