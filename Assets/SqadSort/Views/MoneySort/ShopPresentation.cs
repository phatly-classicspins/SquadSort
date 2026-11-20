using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Views
{
    public sealed class ShopOfferView
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string Price;
        public readonly MoneySortShopOfferGroup Group;
        public readonly MoneySortShopReward Reward;
        public readonly Sprite Icon;
        // False for one-time offers already bought; popups still show them, disabled.
        public readonly bool Purchasable;
        public readonly Action Buy;
        // Seconds left on a time-limited offer (Starter Pack); null when the offer never expires.
        public readonly Func<int> RemainingSeconds;

        public ShopOfferView(
            string id,
            string title,
            string price,
            MoneySortShopOfferGroup group,
            MoneySortShopReward reward,
            Sprite icon,
            bool purchasable,
            Action buy,
            Func<int> remainingSeconds = null)
        {
            RemainingSeconds = remainingSeconds;
            Id = id;
            Title = title;
            Price = price;
            Group = group;
            Reward = reward ?? new MoneySortShopReward();
            Icon = icon;
            Purchasable = purchasable;
            Buy = buy;
        }
    }

    public sealed class ShopPresentation
    {
        public readonly IReadOnlyList<ShopOfferView> Offers;
        public readonly Action Close;
        public readonly Action Restore;
        // Opened from gameplay (not as the Home tab): full screen with its own resource bar and close button.
        public readonly bool Overlay;

        public ShopPresentation(IReadOnlyList<ShopOfferView> offers, Action close, Action restore,
            bool overlay = false)
        {
            Offers = offers ?? Array.Empty<ShopOfferView>();
            Close = close;
            Restore = restore;
            Overlay = overlay;
        }
    }

    // Single-offer popups (Starter Pack, Remove Ads). RemainingSeconds is null for offers
    // without a countdown; when it reaches zero the popup closes itself.
    public sealed class IapOfferPopupPresentation
    {
        public readonly ShopOfferView Offer;
        public readonly string Title;
        public readonly string Description;
        public readonly Func<int> RemainingSeconds;
        public readonly Action Close;

        public IapOfferPopupPresentation(
            ShopOfferView offer,
            string title,
            string description,
            Func<int> remainingSeconds,
            Action close)
        {
            Offer = offer;
            Title = string.IsNullOrEmpty(title) ? offer?.Title : title;
            Description = description ?? string.Empty;
            RemainingSeconds = remainingSeconds;
            Close = close;
        }

        public bool Purchasable => Offer != null && Offer.Purchasable;
    }
}
