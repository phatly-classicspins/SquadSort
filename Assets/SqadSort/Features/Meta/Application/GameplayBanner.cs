using ClassicSpins.PrototypeFramework.Application;
using Game.Gen;

namespace Game.Application
{
    /// <summary>
    /// D-115 (P9 tech doc Banner_level, as Money Sort): the bottom banner is up while a level is played from
    /// <see cref="EconomyTuning.BannerFirstLevel"/> on, and down on the level-complete screen and off Gameplay. Remove Ads
    /// (the ad gate) keeps it down. The ads module decides how it draws.
    /// </summary>
    public sealed class GameplayBanner
    {
        private readonly IBannerAds _banner;
        private readonly IAdGate _gate;
        private readonly EconomyTuning _tuning;

        public GameplayBanner(IBannerAds banner, IAdGate gate, EconomyTuning tuning)
        {
            _banner = banner; _gate = gate; _tuning = tuning;
        }

        /// <summary>Pure rule: shown from the first level on; 0 = never.</summary>
        public static bool ShouldShow(int level, int firstLevel) => firstLevel > 0 && level >= firstLevel;

        /// <summary>A level started (or restarted): show or hide for it. True when the banner is (asked to be) up, so the
        /// gameplay layout keeps room for it.</summary>
        public bool LevelStarted(int level)
        {
            bool up = ShouldShow(level, _tuning.BannerFirstLevel) && _gate.CanShow(AdPlacements.BannerGameplay);
            if (up) _banner.Show(AdPlacements.BannerGameplay);
            else _banner.Hide();
            return up;
        }

        /// <summary>The level-complete screen, or leaving Gameplay.</summary>
        public void Hide() => _banner.Hide();
    }
}
