using System;
using System.Collections.Generic;

namespace Game.Application
{
    /// <summary>Store sections, top to bottom (features/shop.md §2).</summary>
    public enum ShopSection { RemoveAds, Limited, Special, Gold }

    /// <summary>The corner tag on a pack card (GDD 6.1).</summary>
    public enum ShopTag { None, Hot, Off, BestValue }

    /// <summary>
    /// One IAP pack (features/shop.md §1). <see cref="Id"/> is the store product id without the <c>squad.sort.</c> prefix and is
    /// also the framework catalog id (<c>Resources/Config/products.json</c>). The rewards are granted by
    /// <see cref="LevelProgressService.GrantPurchase"/>; only <c>remove_ads</c> also carries a framework entitlement grant.
    /// </summary>
    public sealed record ShopPack(string Id, ShopSection Section, long Coins, int BoosterEach, int UnlimitedLivesHours,
        bool RemoveAds, bool OneTime, decimal PriceUsd, ShopTag Tag)
    {
        public bool HasBoosters => BoosterEach > 0;
        /// <summary>The platform store product id; Android uses lowercase, iOS preserves the shipped spelling.</summary>
        public string StoreId => PlatformProductCatalog.StoreId(ShopCatalog.StorePrefix + Id, ShopCatalog.AndroidStore);
    }

    /// <summary>The catalog of GDD 6.1 in Store order (features/shop.md §1). Data, not UI: the Store lists what
    /// <see cref="LevelProgressService.ShopPacks"/> says is on sale now.</summary>
    public static class ShopCatalog
    {
        public const string StorePrefix = "squad.sort.";
#if UNITY_ANDROID
        public const bool AndroidStore = true;
#else
        public const bool AndroidStore = false;
#endif
        public const string RemoveAdsId = "remove_ads", StarterId = "starter_pack";

        public static readonly IReadOnlyList<ShopPack> All = new[]
        {
            new ShopPack(RemoveAdsId,    ShopSection.RemoveAds, 1500,   1, 0, true,  true,  5.99m,  ShopTag.Hot),   // Squad Sort GDD §6.1: each booster ×1 (D-140 had none)
            new ShopPack(StarterId,      ShopSection.Limited,   2500,   2, 2, false, true,  1.99m,  ShopTag.Off),
            new ShopPack("pack_small",   ShopSection.Special,   2500,   2, 1, false, false, 4.99m,  ShopTag.None),
            new ShopPack("pack_medium",  ShopSection.Special,   10000,  5, 2, false, false, 9.99m,  ShopTag.None),
            new ShopPack("pack_ultra",   ShopSection.Special,   25000,  10, 6, false, false, 24.99m, ShopTag.None),
            new ShopPack("coins_1K",     ShopSection.Gold,      1000,   0, 0, false, false, 1.99m,  ShopTag.None),
            new ShopPack("coins_5K",     ShopSection.Gold,      5000,   0, 0, false, false, 4.99m,  ShopTag.None),
            new ShopPack("coins_10K",    ShopSection.Gold,      10000,  0, 0, false, false, 8.99m,  ShopTag.None),
            new ShopPack("coins_25K",    ShopSection.Gold,      25000,  0, 0, false, false, 19.99m, ShopTag.None),   // GDD §6.1 (25/11)
            new ShopPack("coins_50K",    ShopSection.Gold,      50000,  0, 0, false, false, 34.99m, ShopTag.None),
            new ShopPack("coins_100K",   ShopSection.Gold,      100000, 0, 0, false, false, 69.99m, ShopTag.BestValue),
        };

        public static ShopPack Find(string id)
        {
            foreach (var p in All) if (p.Id == id) return p;
            return null;
        }

        /// <summary>The fallback price label until the store reports a localized one ("$5.99").</summary>
        public static string FallbackPrice(ShopPack pack) => "$" + pack.PriceUsd.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
    }
}
