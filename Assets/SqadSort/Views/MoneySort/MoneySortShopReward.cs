using System; using UnityEngine;
namespace Game.Views {
    public enum MoneySortShopOfferGroup
    {
        // Top of the shop; also unlocks playing without internet.
        RemoveAds,
        SpecialPack,
        CoinPack,
        // Only sold from the Starter Bundle popup.
        Starter
    }

    [Serializable]
    public sealed class MoneySortShopReward
    {
        [Min(0)] public int coins;
        [Min(0)] public int shuffle;
        [Min(0)] public int hammer;
        [Min(0)] public int freeMove;
        [Min(0)] public int infiniteLivesMinutes;
        // Removes banner + interstitial forever and lets the game run offline.
        public bool removeAds;

        public int GetBoosterCount(MoneySortBoosterType type) => type switch
        {
            MoneySortBoosterType.Shuffle => shuffle,
            MoneySortBoosterType.Hammer => hammer,
            MoneySortBoosterType.FreeMove => freeMove,
            _ => 0
        };
    }


}