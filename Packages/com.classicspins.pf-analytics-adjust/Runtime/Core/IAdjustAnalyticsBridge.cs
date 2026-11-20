using System.Collections.Generic;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust.Core
{
    /// <summary>Synchronous primitive-only boundary. True means the SDK API was invoked, not dashboard delivery.</summary>
    public interface IAdjustAnalyticsBridge
    {
        bool ConfigureAdSignals(bool granted);
        bool Start(string appToken, string environment);
        bool SetEnabled(bool enabled);
        bool TrackToken(string token, IReadOnlyList<KeyValuePair<string, string>> parameters);
        bool TrackRevenue(string source, double amount, string currency, string network, string unit, string placement);
        bool AddCallbackParameter(string key, string value);
        bool AddPartnerParameter(string key, string value);
        bool ForgetMe();
    }
}
