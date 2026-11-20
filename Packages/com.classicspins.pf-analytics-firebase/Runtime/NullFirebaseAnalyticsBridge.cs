using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Analytics.Firebase.Core;

namespace ClassicSpins.PrototypeFramework.Analytics.Firebase
{
    /// <summary>Absence refuses every operation, including privacy, and never throws (even if logging throws).</summary>
    public sealed class NullFirebaseAnalyticsBridge : IFirebaseAnalyticsBridge
    {
        private readonly ILog _log;
        public NullFirebaseAnalyticsBridge(ILog log) { _log = log; }
        private bool Refuse(string call)
        {
            try { _log?.Warn("[pf-analytics-firebase] " + call + " refused: Firebase SDK missing; follow the module manual installation checklist (PF_FIREBASE unarmed)."); }
            catch (Exception) { }
            return false;
        }
        public void Start(Action<bool, string> completed)
        {
            Refuse("Start");
            try { completed?.Invoke(false, "pf-analytics-firebase: Firebase SDK missing"); } catch (Exception) { }
        }
        public bool SetConsent(bool analyticsStorage, bool adSignals) => Refuse("SetConsent");
        public bool SetCollectionEnabled(bool enabled) => Refuse("SetCollectionEnabled");
        public bool LogEvent(string name, IReadOnlyList<KeyValuePair<string, object>> parameters) => Refuse("LogEvent");
        public bool SetUserProperty(string name, string value) => Refuse("SetUserProperty");
        public bool SetUserId(string id) => Refuse("SetUserId");
        public bool ResetAnalyticsData() => Refuse("ResetAnalyticsData");
    }
}
