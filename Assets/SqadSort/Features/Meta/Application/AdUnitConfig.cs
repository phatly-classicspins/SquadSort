using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Gen;

namespace Game.Application
{
    /// <summary>
    /// D-115: what the pf-ads-max module reads — one MAX ad unit per format and platform
    /// (<c>ads.max.&lt;android|ios&gt;.&lt;rewarded|interstitial|banner&gt;.unitId</c>, the module's format defaults: every
    /// rewarded placement shares the RV unit, as Money Sort) and the rewarded flag (<c>ads.&lt;placement&gt;.rewarded</c> = 1;
    /// absent ⇒ interstitial). Bundled with the build, never remote: the module's build preflight counts a remote-only
    /// unit id as missing, so these keys stay out of <see cref="EconomyConfig"/> and the Firebase template.
    /// </summary>
    public static class AdUnitConfig
    {
        // SqadSort's own MAX ad units go here (AppLovin dashboard ▸ MAX ▸ Ad Units). Empty until the app exists in MAX — never
        // another game's units. TODO(sdk-ids): fill them, then set AllowMissingUnitIds back to 0 (docs/sdk-placeholders.md).
        private const string AndroidRewarded = "", AndroidInter = "", AndroidBanner = "";
        private const string IosRewarded = "", IosInter = "", IosBanner = "";
        // 1 = pf-ads-max's unit-id preflight is waived (logged as a warning) so builds pass before the MAX app exists; every show
        // of an empty unit resolves Failed ("No ads available"). Set 0 once the unit ids above are real.
        private const int AllowMissingUnitIds = 1;
        // TODO(sdk-ids): SqadSort's Adjust app token (Adjust dashboard ▸ app ▸ App Token). A 12-character placeholder until the
        // app exists in Adjust: pf-analytics-adjust's preflight needs a well-formed bundled token; this one tracks nothing.
        private const string AdjustAppToken = "sqadsorttodo";
        // D-124 (Chipsaway feedback 5): MAX pauses Unity while a fullscreen ad is up (iOS UnityPause in didDisplayAd, Android
        // onPause), so the module's display signal is only delivered after the ad closes — by then its realtime display timeout
        // (default 10 s) has expired and a watched rewarded ad reads Failed ("No ads available", no reward). Until pf-ads-max
        // stops counting while the app is paused, the timeout must outlast any ad. A show that fails still ends at once on
        // MAX's DisplayFailed callback.
        private const int DisplayTimeoutSeconds = 600;

        /// <summary>Every rewarded placement; the rest are interstitial or banner.</summary>
        public static readonly AdPlacement[] Rewarded =
        {
            AdPlacements.RewardedRevive, AdPlacements.RewardedRvSlot, AdPlacements.RewardedWinDouble, AdPlacements.RewardedBooster,
            AdPlacements.RewardedLife, AdPlacements.RewardedSkipLevel, AdPlacements.RewardedGoldBrick,
        };

        /// <summary>Format-default unit-id key → unit id, for both platforms.</summary>
        public static Dictionary<string, string> UnitIds() => new Dictionary<string, string>
        {
            ["ads.max.android.rewarded.unitId"] = AndroidRewarded,
            ["ads.max.android.interstitial.unitId"] = AndroidInter,
            ["ads.max.android.banner.unitId"] = AndroidBanner,
            ["ads.max.ios.rewarded.unitId"] = IosRewarded,
            ["ads.max.ios.interstitial.unitId"] = IosInter,
            ["ads.max.ios.banner.unitId"] = IosBanner,
        };

        /// <summary>The single bundled <see cref="ConfigDefaults"/>: the economy keys, the ad unit keys and the Adjust app token.</summary>
        public static ConfigDefaults With(ConfigDefaults economy)
        {
            var all = new Dictionary<string, object>();
            foreach (var kv in economy.Values) all[kv.Key] = kv.Value;
            foreach (var kv in UnitIds()) all[kv.Key] = kv.Value;
            foreach (var p in Rewarded) all["ads." + p.Value + ".rewarded"] = 1;
            all["ads.max.allowMissingUnitIds"] = AllowMissingUnitIds;
            all["ads.max.displayTimeoutSeconds"] = DisplayTimeoutSeconds;
            all["analytics.adjust.appToken"] = AdjustAppToken;
            return new ConfigDefaults(all);
        }
    }
}
