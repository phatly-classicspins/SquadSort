namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>
    /// One paid impression as the SDK reported it, carried across <see cref="IMaxBridge.OnRevenuePaid"/>
    /// as primitives only — strings, a <c>bool</c> and a <c>double</c> — so no MAX type crosses the bridge
    /// (AD-MOD-1). The values are the SDK's raw ones; <see cref="MaxRevenueReporter"/> normalizes them into
    /// the framework's <c>AdRevenuePaid</c> record.
    /// </summary>
    public readonly struct MaxRevenue
    {
        public MaxRevenue(
            string adUnitId,
            string adFormat,
            bool rewarded,
            string network,
            string placement,
            double revenue,
            string precision)
        {
            AdUnitId = adUnitId;
            AdFormat = adFormat;
            Rewarded = rewarded;
            Network = network;
            Placement = placement;
            Revenue = revenue;
            Precision = precision;
        }

        /// <summary>The ad unit the impression was paid on (the SDK's ad unit identifier).</summary>
        public string AdUnitId { get; }

        /// <summary>The SDK's raw format label (e.g. <c>REWARDED</c>, <c>INTER</c>); may be null or empty.</summary>
        public string AdFormat { get; }

        /// <summary>Which callback fired: <c>true</c> for the rewarded one, <c>false</c> for the
        /// interstitial one. The reporter falls back to it when <see cref="AdFormat"/> is unrecognised.</summary>
        public bool Rewarded { get; }

        /// <summary>The network that served the impression, verbatim.</summary>
        public string Network { get; }

        /// <summary>The SDK's echo of the placement passed to the show call; null or empty when absent.</summary>
        public string Placement { get; }

        /// <summary>The impression's revenue in USD; the SDK reports <c>-1</c> when it has none.</summary>
        public double Revenue { get; }

        /// <summary>The SDK's raw revenue-precision label (e.g. <c>exact</c>); may be null or empty.</summary>
        public string Precision { get; }
    }
}
