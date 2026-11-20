using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>
    /// The AppLovin MAX implementation of the framework's <see cref="IBannerAds"/> port: one bottom-centre
    /// banner. Registered by <c>MaxAdsInstaller</c> after the framework installers, so it wins over the
    /// framework's <c>FakeBannerAds</c>.
    /// </summary>
    /// <remarks>
    /// <para><b>Unit id, most specific first:</b> <c>ads.max.&lt;platform&gt;.&lt;placement&gt;.unitId</c>,
    /// <c>ads.max.&lt;placement&gt;.unitId</c>, then the banner format default
    /// <c>ads.max.&lt;platform&gt;.banner.unitId</c>, <c>ads.max.banner.unitId</c> — the
    /// <see cref="MaxAdUnits"/> key shape. Never an interstitial or rewarded default.</para>
    /// <para><b>The game's wish is state, applied when it can be.</b> <see cref="Show"/> / <see cref="Hide"/>
    /// record what the game wants; the wish is applied now if the SDK is initialized, else on the bridge's
    /// <see cref="IMaxBridge.OnInitialized"/> — a banner asked for while MAX's consent flow is still up
    /// appears once init succeeds.</para>
    /// <para><b>Remove-ads is enforced here too.</b> Every apply asks <see cref="IAdGate.CanShow"/>; a placement
    /// the gate refuses (remove-ads held — a banner is non-rewarded) is hidden, not shown. The caller still owns
    /// re-applying after a purchase (call <see cref="Show"/> or <see cref="Hide"/> again).</para>
    /// <para><b>Preflight marker.</b> Set <c>ads.&lt;placement&gt;.banner = 1</c> (<see cref="BannerKey"/>) in the
    /// bundled config so the build preflight checks the banner keys above for that placement.</para>
    /// <para><b>Never throws</b> (NFR-06): a missing unit id or a bridge that throws logs a warning naming
    /// <c>pf-ads-max</c> and leaves the screen as it was.</para>
    /// <para><b>No visibility slot yet.</b> A banner never covers gameplay; it takes no slot in
    /// <see cref="MaxAdVisibility"/>, so <c>BannerVisible</c> / <c>BannerHeightPx</c> stay inert.</para>
    /// </remarks>
    public sealed class MaxBannerAds : IBannerAds
    {
        private const string Module = "[pf-ads-max] ";
        public const string BannerFormat = "banner";

        private readonly IMaxBridge _max;
        private readonly IGameConfig _config;
        private readonly IAdGate _gate;
        private readonly ILog _log;
        private readonly string _platform;

        private AdPlacement? _wanted;   // what the game asked for; null = no banner
        private string _shownUnit;      // the unit currently shown through the bridge
        private string _warnedEmpty;    // the placement already warned about an empty unit id

        public MaxBannerAds(IMaxBridge max, MaxAdUnits units, IGameConfig config, IAdGate gate, ILog log)
        {
            _max = max ?? throw new ArgumentNullException(nameof(max));
            if (units == null) throw new ArgumentNullException(nameof(units));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _platform = units.Platform;
            _max.OnInitialized += Apply;
        }

        /// <summary>The bundled-config marker of a banner placement (<c>ads.&lt;placement&gt;.banner</c>, non-zero ⇒ banner),
        /// read by the build preflight only — the runtime knows a banner by the port it was called through.</summary>
        public static string BannerKey(string placement) => "ads." + placement + ".banner";

        /// <summary>The unit-id keys tried for a banner <paramref name="placement"/> on <paramref name="platform"/>, in order.</summary>
        public static IReadOnlyList<string> KeysFor(string platform, string placement)
        {
            var keys = new List<string>(4);
            bool hasPlatform = !string.IsNullOrWhiteSpace(platform);
            if (hasPlatform) keys.Add(MaxAdUnits.UnitIdKey(platform.Trim() + "." + placement));
            keys.Add(MaxAdUnits.UnitIdKey(placement));
            if (hasPlatform) keys.Add(MaxAdUnits.UnitIdKey(platform.Trim() + "." + BannerFormat));
            keys.Add(MaxAdUnits.UnitIdKey(BannerFormat));
            return keys;
        }

        public void Show(AdPlacement placement)
        {
            _wanted = placement;
            Apply();
        }

        public void Hide()
        {
            _wanted = null;
            Apply();
        }

        private string Resolve(AdPlacement placement)
        {
            foreach (var key in KeysFor(_platform, placement.Value))
            {
                var value = _config.Get(new ConfigKey<string>(key));
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            return null;
        }

        private void Apply()
        {
            try
            {
                if (!_max.IsInitialized) return;   // OnInitialized applies the wish later

                string unit = null;
                if (_wanted is { } placement && !string.IsNullOrWhiteSpace(placement.Value) && _gate.CanShow(placement))
                {
                    unit = Resolve(placement);
                    if (unit == null && _warnedEmpty != placement.Value)
                    {
                        _warnedEmpty = placement.Value;
                        _log.Warn(Module + "banner '" + placement.Value + "' not shown — no unit id in "
                                  + string.Join(", ", KeysFor(_platform, placement.Value)) + ".");
                    }
                }

                if (_shownUnit != null && _shownUnit != unit)
                {
                    var hide = _shownUnit;
                    _shownUnit = null;
                    _max.HideBanner(hide);
                }
                if (unit != null && _shownUnit != unit)
                {
                    _max.ShowBanner(unit);
                    _shownUnit = unit;
                }
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "banner update threw: " + ex.Message);
            }
        }
    }
}
