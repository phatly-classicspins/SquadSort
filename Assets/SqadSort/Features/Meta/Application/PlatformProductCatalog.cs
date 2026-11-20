using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Application;

namespace Game.Application
{
    /// <summary>Google Play IDs are lowercase; preserve the existing spelling on the other stores.</summary>
    public sealed class PlatformProductCatalog : ICatalogSource
    {
        private readonly ICatalogSource _source;
        private readonly bool _android;

        public PlatformProductCatalog(ICatalogSource source, bool android)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _android = android;
        }

        public static string StoreId(string id, bool android) => android ? id.ToLowerInvariant() : id;

        public IReadOnlyList<ProductInfo> LoadCatalog()
        {
            var source = _source.LoadCatalog();
            if (!_android) return source;
            var products = new ProductInfo[source.Count];
            for (int i = 0; i < source.Count; i++)
                products[i] = source[i] with { Id = StoreId(source[i].Id, true) };
            return products;
        }
    }
}