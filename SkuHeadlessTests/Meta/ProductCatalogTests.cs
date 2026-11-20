using System.IO;
using System.Linq;
using Game.Application;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>D-133: the bundled products.json holds the store product ids of GDD 6.1 (<c>squad.sort.</c> prefix), one per
    /// Shop pack, and Remove Ads is the only non-consumable.</summary>
    public class ProductCatalogTests
    {
        static JArray Products() =>
            (JArray)JObject.Parse(File.ReadAllText(RepoLayout.Path("Assets", "SqadSort", "Resources", "Config", "products.json")))["products"];

        [Test]
        public void Every_shop_pack_has_its_store_product()
        {
            var ids = Products().Select(p => (string)p["id"]).ToList();
            Assert.That(ids, Is.EquivalentTo(ShopCatalog.All.Select(p => p.StoreId)));
            Assert.That(ids, Has.All.StartWith("squad.sort."));
        }

        [Test]
        public void Remove_ads_is_the_only_non_consumable_and_grants_the_entitlement()
        {
            var products = Products();
            var nonConsumable = products.Where(p => (string)p["type"] == "NonConsumable").Select(p => (string)p["id"]);
            Assert.That(nonConsumable, Is.EqualTo(new[] { "squad.sort.remove_ads" }));
            var entitlement = products.Single(p => (string)p["id"] == "squad.sort.remove_ads")["grants"].Single(g => (string)g["kind"] == "entitlement");
            Assert.That((string)entitlement["id"], Is.EqualTo("remove_ads"));
        }

        [Test]
        public void Every_pack_carries_its_coins_as_the_store_grant()
        {
            // QA IAP-01: the IAP module applies products.json grants for every order, also one that arrives late (Ask-to-Buy,
            // a pending payment, the app killed after paying). The coins live there, so they are never lost; the amount must
            // stay the Shop's (ShopCatalog), which LevelProgressService.GrantPurchase no longer grants itself.
            var products = Products();
            foreach (var pack in ShopCatalog.All)
            {
                var coins = products.Single(p => (string)p["id"] == pack.StoreId)["grants"]
                    .Where(g => (string)g["kind"] == "coin").ToList();
                Assert.That(coins.Count, Is.EqualTo(1), pack.Id);
                Assert.That(((string)coins[0]["id"], (long)coins[0]["amount"]), Is.EqualTo((SqadSortResources.Coin.Value, pack.Coins)), pack.Id);
            }
        }
    }
}
