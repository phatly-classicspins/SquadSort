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
        // SqadSort's own MAX ad units go here (AppLovin dashboard ▸ MAX ▸ Ad Units). Empty until the app exists in MAX: the
        // pf-ads-max build preflight then fails the build (AllowMissingUnitIds = 0), so no build ships with another game's units.
        private const string AndroidRewarded = "", AndroidInter = "", AndroidBanner = "";
        private const string IosRewarded = "", IosInter = "", IosBanner = "";
        // pf-ads-max's unit-id preflight stays a build failure (0); set 1 only to ship with a unit left empty.
        private const int AllowMissingUnitIds = 0;
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

        /// <summary>The single bundled <see cref="ConfigDefaults"/>: the economy keys plus the ad unit keys.</summary>
        public static ConfigDefaults With(ConfigDefaults economy)
        {
            var all = new Dictionary<string, object>();
            foreach (var kv in economy.Values) all[kv.Key] = kv.Value;
            foreach (var kv in UnitIds()) all[kv.Key] = kv.Value;
            foreach (var p in Rewarded) all["ads." + p.Value + ".rewarded"] = 1;
            all["ads.max.allowMissingUnitIds"] = AllowMissingUnitIds;
            all["ads.max.displayTimeoutSeconds"] = DisplayTimeoutSeconds;
            return new ConfigDefaults(all);
        }
    }
}
