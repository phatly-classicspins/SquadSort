using System;
using System.Threading;
using System.Threading.Tasks;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace Game.Application
{
    /// <summary>D-063: whether the device has a connection right now. The Unity side reads <c>Application.internetReachability</c>
    /// (no server ping — the Money Sort rule); a tester can force it offline.</summary>
    public interface INetworkStatus
    {
        bool IsOnline { get; }
    }

    /// <summary>
    /// D-063 (features/online.md, Money Sort's internet gate): the game is held behind the No Internet popup while
    /// <c>require_internet</c> is on, the player does not own Remove Ads and the device is offline. Engine-free — the popup, the
    /// polling and the Unity reachability live outside it.
    /// </summary>
    public sealed class OnlineGate
    {
        /// <summary>The Retry button keeps "Connecting…" at least this long, so a reconnect attempt reads as one.</summary>
        public static readonly TimeSpan MinRetry = TimeSpan.FromSeconds(1.5);

        /// <summary>The Remove Ads entitlement — the same key the framework's AdGate reads.</summary>
        public static readonly EntitlementKey RemoveAds = new EntitlementKey("remove_ads");

        private readonly INetworkStatus _network;
        private readonly IEntitlementService _entitlements;
        private readonly EconomyTuning _tuning;

        public OnlineGate(INetworkStatus network, IEntitlementService entitlements, EconomyTuning tuning)
        {
            _network = network; _entitlements = entitlements; _tuning = tuning;
        }

        /// <summary>True: the No Internet popup must be up.</summary>
        public bool ShouldBlock => _tuning.RequireInternet && !_entitlements.Has(RemoveAds) && !_network.IsOnline;

        /// <summary>How often the gate is looked at while the app runs (<c>internet_check_seconds</c>).</summary>
        public TimeSpan CheckInterval => TimeSpan.FromSeconds(Math.Max(1, _tuning.InternetCheckSeconds));

        /// <summary>Retry: wait <see cref="MinRetry"/> (through <paramref name="delay"/>, so tests need no clock), then look again.
        /// True when the popup can close.</summary>
        public async Task<bool> RetryAsync(Func<TimeSpan, CancellationToken, Task> delay, CancellationToken ct)
        {
            await delay(MinRetry, ct);
            return !ShouldBlock;
        }
    }
}
