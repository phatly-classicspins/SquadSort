using Game.Application;

namespace Game.Infrastructure
{
    /// <summary>
    /// D-063: the device's connection as Unity reports it (<c>Application.internetReachability</c> — Wi-Fi or mobile data present,
    /// no server ping, as in Money Sort). <see cref="ForceOffline"/> is the tester's "Force offline" cheat.
    /// </summary>
    public sealed class UnityNetworkStatus : INetworkStatus
    {
        public bool ForceOffline { get; set; }

        public bool IsOnline => !ForceOffline
            && UnityEngine.Application.internetReachability != UnityEngine.NetworkReachability.NotReachable;
    }
}
