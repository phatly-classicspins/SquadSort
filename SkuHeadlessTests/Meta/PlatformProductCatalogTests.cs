using System.Collections.Generic;
using System.Linq;
using ClassicSpins.PrototypeFramework.Application;
using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    public class PlatformProductCatalogTests
    {
        private sealed class Source : ICatalogSource
        {
            public IReadOnlyList<ProductInfo> Products;
            public IReadOnlyList<ProductInfo> LoadCatalog() => Products;
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Every_pack_keeps_rewards_and_matches_the_platform_store_id(bool android)
        {
            var products = ShopCatalog.All.Select(p => new ProductInfo(
                ShopCatalog.StorePrefix + p.Id,
                new[] { new GrantSpec("coin", "coin", p.Coins) },
                Disabled: true, LocalizedPrice: "$4.99", Available: true,
                Kind: p.RemoveAds ? ProductKind.NonConsumable : ProductKind.Consumable)).ToArray();
            var mapped = new PlatformProductCatalog(new Source { Products = products }, android).LoadCatalog();
            Assert.That(mapped.Select(p => p.Id).Distinct().Count(), Is.EqualTo(products.Length));
            for (int i = 0; i < products.Length; i++)
            {
                var expectedId = android ? products[i].Id.ToLowerInvariant() : products[i].Id;
                Assert.That(mapped[i].Id, Is.EqualTo(expectedId));
                Assert.That(mapped[i], Is.EqualTo(products[i] with { Id = expectedId }));
                Assert.That(products[i].Id, Is.EqualTo(ShopCatalog.StorePrefix + ShopCatalog.All[i].Id));
            }
        }

        [TestCase("squad.sort.coins_5K", "squad.sort.coins_5k")]
        [TestCase("Squad.Sort.NEW_Pack", "squad.sort.new_pack")]
        public void Android_normalizes_the_entire_id_and_ios_preserves_it(string id, string androidId)
        {
            Assert.That(PlatformProductCatalog.StoreId(id, true), Is.EqualTo(androidId));
            Assert.That(PlatformProductCatalog.StoreId(id, false), Is.EqualTo(id));
        }
    }
}