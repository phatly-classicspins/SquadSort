using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Analytics.Adjust.Core;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust
{
    public sealed class NullAdjustAnalyticsBridge : IAdjustAnalyticsBridge
    {
        private readonly ILog _log;
        public NullAdjustAnalyticsBridge(ILog log) { _log = log; }
        private bool Refuse(string operation)
        {
            try { _log?.Warn("[pf-analytics-adjust] " + operation + " refused: Adjust SDK missing (PF_ADJUST unarmed); install the module SDK requirements."); }
            catch (Exception) { }
            return false;
        }
        public bool ConfigureAdSignals(bool granted) => Refuse("ConfigureAdSignals");
        public bool Start(string appToken, string environment) => Refuse("Start");
        public bool SetEnabled(bool enabled) => Refuse("SetEnabled");
        public bool TrackToken(string token, IReadOnlyList<KeyValuePair<string, string>> parameters) => Refuse("TrackToken");
        public bool TrackRevenue(string source, double amount, string currency, string network, string unit, string placement) => Refuse("TrackRevenue");
        public bool AddCallbackParameter(string key, string value) => Refuse("AddCallbackParameter");
        public bool AddPartnerParameter(string key, string value) => Refuse("AddPartnerParameter");
        public bool ForgetMe() => Refuse("ForgetMe");
    }
}
