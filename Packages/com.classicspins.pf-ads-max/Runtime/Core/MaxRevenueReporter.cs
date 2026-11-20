using System;
using System.Threading;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace ClassicSpins.PrototypeFramework.Ads.Max.Core
{
    /// <summary>
    /// Turns every paid impression the bridge reports (<see cref="IMaxBridge.OnRevenuePaid"/>) into exactly
    /// one framework <see cref="AdRevenuePaid"/> record, tracked through <see cref="IAnalytics.Track"/>. This
    /// is the record's only producer path: money is never derived from the show-funnel events.
    /// </summary>
    /// <remarks>
    /// <para><b>The producer owns every value</b> (<c>revenue-mapping.md</c>): <see cref="Mediator"/> and
    /// <see cref="Currency"/> are constants; <c>AdFormat</c> and <c>Precision</c> are normalized into fixed
    /// vocabularies (<see cref="NormalizeFormat"/>, <see cref="NormalizePrecision"/>); <c>Placement</c> is the
    /// SDK's echo of the show placement or <c>""</c> — never looked up from a unit binding; the unit id and
    /// network are verbatim (null ⇒ <c>""</c>).</para>
    /// <para><b>Revenue.</b> A negative value (the SDK's missing-value <c>-1</c>) and a non-finite one
    /// (NaN, ±∞ — it would poison every downstream sum) are dropped with a warning. <c>0</c> is kept.</para>
    /// <para><b>No consent here.</b> The record is tracked whatever the consent state — the analytics sink
    /// applies consent. The only switch is <see cref="RevenueLogKey"/> (absent ⇒ on, <c>0</c> ⇒ off), read
    /// at callback time and independent of the funnel switch <c>ads.max.autoLog</c>. A config read that
    /// throws is logged and treated as on, so no revenue is lost to a faulty config layer.</para>
    /// <para><b>Never throws into the SDK callback stack.</b> A throwing <see cref="IAnalytics.Track"/> or
    /// config read is caught and logged as a warning naming <c>pf-ads-max</c>.</para>
    /// <para><b>Never marshals threads.</b> The bridge asks the SDK for main-thread delivery. If a callback
    /// nevertheless arrives on a thread other than the one this reporter was constructed on, one warning is
    /// logged (the first time only) and the record is still tracked.</para>
    /// <para><b>Lifetime.</b> Subscribes in the constructor, so it must exist before the first paid
    /// impression; the installer resolves it as the ads service is built (revenue only follows a show, and
    /// every show goes through that service). <see cref="Dispose"/> unsubscribes.</para>
    /// </remarks>
    public sealed class MaxRevenueReporter : IDisposable
    {
        private const string Module = "[pf-ads-max] ";

        /// <summary>The framework's mediator token for AppLovin MAX (<c>AdRevenuePaid.Mediator</c>).</summary>
        public const string Mediator = "applovin_max";

        /// <summary>The currency every MAX revenue value is reported in (<c>AdRevenuePaid.Currency</c>).</summary>
        public const string Currency = "USD";

        /// <summary>Revenue-record switch (int; absent ⇒ on, <c>0</c> ⇒ off). Independent of <c>ads.max.autoLog</c>.</summary>
        public const string RevenueLogKey = "ads.max.revenueLog";

        // ── the AdFormat vocabulary ─────────────────────────────────────────────────────────────────────
        public const string FormatRewarded = "rewarded";
        public const string FormatInterstitial = "interstitial";
        public const string FormatBanner = "banner";
        public const string FormatMrec = "mrec";
        public const string FormatAppOpen = "app_open";

        // ── the Precision vocabulary ────────────────────────────────────────────────────────────────────
        public const string PrecisionExact = "exact";
        public const string PrecisionEstimated = "estimated";
        public const string PrecisionPublisherDefined = "publisher_defined";
        public const string PrecisionUndefined = "undefined";

        private readonly IMaxBridge _max;
        private readonly IAnalytics _analytics;
        private readonly IGameConfig _config;
        private readonly ILog _log;
        private readonly int _ownerThreadId;
        private int _offThreadWarned;
        private int _disposed;

        // No defaulted parameters: VContainer resolves every parameter and ignores C# default values.
        public MaxRevenueReporter(IMaxBridge max, IAnalytics analytics, IGameConfig config, ILog log)
        {
            _max = max ?? throw new ArgumentNullException(nameof(max));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _ownerThreadId = Environment.CurrentManagedThreadId;

            _max.OnRevenuePaid += RevenuePaid;
        }

        /// <summary>
        /// The <c>AdFormat</c> vocabulary value for the SDK's raw label: <c>REWARDED</c> ⇒ rewarded,
        /// <c>INTER</c> ⇒ interstitial, <c>BANNER</c> / <c>LEADER</c> ⇒ banner, <c>MREC</c> ⇒ mrec,
        /// <c>APPOPEN</c> / <c>APP_OPEN</c> ⇒ app_open, and each vocabulary value itself — case-insensitive,
        /// trimmed. Anything else (or empty) falls back to the format of the callback that fired, so the
        /// field is always in the vocabulary and no revenue is dropped for a label.
        /// </summary>
        public static string NormalizeFormat(string raw, bool rewarded)
        {
            var value = raw == null ? "" : raw.Trim().ToUpperInvariant();
            switch (value)
            {
                case "REWARDED": return FormatRewarded;
                case "INTER":
                case "INTERSTITIAL": return FormatInterstitial;
                case "BANNER":
                case "LEADER": return FormatBanner;
                case "MREC": return FormatMrec;
                case "APPOPEN":
                case "APP_OPEN": return FormatAppOpen;
                default: return rewarded ? FormatRewarded : FormatInterstitial;
            }
        }

        /// <summary>
        /// The <c>Precision</c> vocabulary value for the SDK's raw label (case-insensitive, trimmed): one of
        /// <c>exact</c>, <c>estimated</c>, <c>publisher_defined</c>, <c>undefined</c>; anything else, or empty,
        /// ⇒ <c>undefined</c>.
        /// </summary>
        public static string NormalizePrecision(string raw)
        {
            var value = raw == null ? "" : raw.Trim().ToLowerInvariant();
            switch (value)
            {
                case PrecisionExact: return PrecisionExact;
                case PrecisionEstimated: return PrecisionEstimated;
                case PrecisionPublisherDefined: return PrecisionPublisherDefined;
                default: return PrecisionUndefined;
            }
        }

        /// <summary>Whether <paramref name="revenue"/> may be recorded: finite and not negative (<c>0</c> is kept).</summary>
        public static bool IsRecordable(double revenue)
            => !double.IsNaN(revenue) && !double.IsInfinity(revenue) && revenue >= 0d;

        /// <summary>The normalized record for <paramref name="paid"/>. Private: it does not check the revenue,
        /// so its only caller is the callback, after <see cref="IsRecordable"/>.</summary>
        private static AdRevenuePaid ToRecord(MaxRevenue paid)
            => new AdRevenuePaid(
                Mediator,
                paid.Placement ?? "",
                paid.AdUnitId ?? "",
                NormalizeFormat(paid.AdFormat, paid.Rewarded),
                paid.Network ?? "",
                paid.Revenue,
                Currency,
                NormalizePrecision(paid.Precision));

        private void RevenuePaid(MaxRevenue paid)
        {
            try
            {
                if (Environment.CurrentManagedThreadId != _ownerThreadId
                    && Interlocked.CompareExchange(ref _offThreadWarned, 1, 0) == 0)
                {
                    _log.Warn(Module + "a revenue callback arrived off the thread the reporter was built on — the bridge "
                              + "should deliver SDK events on the main thread. Tracking it anyway; this is logged once.");
                }

                if (!IsEnabled()) return;

                if (!IsRecordable(paid.Revenue))
                {
                    _log.Warn(Module + "revenue for unit '" + (paid.AdUnitId ?? "") + "' is "
                              + paid.Revenue.ToString(System.Globalization.CultureInfo.InvariantCulture)
                              + " (missing or not a finite non-negative value) — no AdRevenuePaid recorded.");
                    return;
                }

                _analytics.Track(ToRecord(paid));
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "AdRevenuePaid for unit '" + (paid.AdUnitId ?? "") + "' could not be tracked: "
                          + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private bool IsEnabled()
        {
            try
            {
                int value;
                return !MaxAdUnits.TryReadInt(_config, RevenueLogKey, out value) || value != 0;
            }
            catch (Exception ex)
            {
                _log.Warn(Module + "reading '" + RevenueLogKey + "' threw (" + ex.GetType().Name + ": " + ex.Message
                          + ") — treating AdRevenuePaid as on.");
                return true;
            }
        }

        /// <summary>Stop listening to the bridge. Idempotent.</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _max.OnRevenuePaid -= RevenuePaid;
        }
    }
}
