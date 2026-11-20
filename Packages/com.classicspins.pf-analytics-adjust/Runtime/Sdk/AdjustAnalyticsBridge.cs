using System.Collections.Generic;
using AdjustSdk;
using UnityEngine;
using ClassicSpins.PrototypeFramework.Analytics.Adjust.Core;
using VendorAdjust = AdjustSdk.Adjust;

namespace ClassicSpins.PrototypeFramework.Analytics.Adjust.Sdk
{
    /// <summary>Only this role names vendor types. APIs inspected at adjust/unity_sdk tag v5.8.2.</summary>
    public sealed class AdjustAnalyticsBridge : IAdjustAnalyticsBridge
    {
        private static bool Supported => !UnityEngine.Application.isEditor &&
            (UnityEngine.Application.platform == RuntimePlatform.Android || UnityEngine.Application.platform == RuntimePlatform.IPhonePlayer);
        public bool ConfigureAdSignals(bool granted)
        {
            if (!Supported) return false;
            var sharing = new AdjustThirdPartySharing(null);
            var value = granted ? "1" : "0";
            sharing.AddGranularOption("google_dma", "ad_personalization", value);
            sharing.AddGranularOption("google_dma", "ad_user_data", value);
            VendorAdjust.TrackThirdPartySharing(sharing);
            return true;
        }
        public bool Start(string appToken, string environment)
        {
            if (!Supported) return false;
            var config = new AdjustConfig(appToken, environment == "production" ? AdjustEnvironment.Production : AdjustEnvironment.Sandbox);
            VendorAdjust.InitSdk(config);
            return true;
        }
        public bool SetEnabled(bool enabled)
        {
            if (!Supported) return false;
            if (enabled) VendorAdjust.Enable(); else VendorAdjust.Disable();
            return true;
        }
        public bool TrackToken(string token, IReadOnlyList<KeyValuePair<string, string>> parameters)
        {
            if (!Supported) return false;
            var evt = new AdjustEvent(token);
            foreach (var p in parameters) evt.AddCallbackParameter(p.Key, p.Value);
            VendorAdjust.TrackEvent(evt);
            return true;
        }
        public bool TrackRevenue(string source, double amount, string currency, string network, string unit, string placement)
        {
            if (!Supported) return false;
            // https://dev.adjust.com/en/sdk/unity/integrations/applovin/ specifies applovin_max_sdk.
            var revenue = new AdjustAdRevenue(source);
            revenue.SetRevenue(amount, currency);
            revenue.AdRevenueNetwork = network;
            revenue.AdRevenueUnit = unit;
            revenue.AdRevenuePlacement = placement;
            VendorAdjust.TrackAdRevenue(revenue);
            return true;
        }
        public bool AddCallbackParameter(string key, string value)
        { if (!Supported) return false; VendorAdjust.AddGlobalCallbackParameter(key, value); return true; }
        public bool AddPartnerParameter(string key, string value)
        { if (!Supported) return false; VendorAdjust.AddGlobalPartnerParameter(key, value); return true; }
        public bool ForgetMe()
        { if (!Supported) return false; VendorAdjust.GdprForgetMe(); return true; }
    }
}
