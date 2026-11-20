using System;
using System.Collections.Generic;

namespace Game.Views
{
    /// <summary>How a Store card is drawn: the Remove Ads card, a pack card (Special), the Starter card or a Gold tile.</summary>
    public enum ShopCardKind { RemoveAds, Pack, Starter, Gold }

    /// <summary>One Store card, every text already localized. <c>Icon</c> / <c>Badge</c> name a sprite of the shop atlas;
    /// <c>Booster</c> and <c>Lives</c> are null when the pack has none; <c>Lines</c> are the Remove Ads benefits; <c>Timer</c> /
    /// <c>TimerLabel</c> the Starter countdown.</summary>
    public sealed class ShopCardVisual
    {
        public readonly string Id, Title, Icon, Coins, Booster, Lives, Price, Badge, BadgeText, IconLabel, Timer, TimerLabel;
        public readonly ShopCardKind Kind;
        public readonly string[] Lines;

        public ShopCardVisual(string id, ShopCardKind kind, string title, string icon, string coins, string booster, string lives,
            string price, string badge, string badgeText, string[] lines, string iconLabel, string timer = null, string timerLabel = null)
        {
            Id = id; Kind = kind; Title = title; Icon = icon; Coins = coins; Booster = booster; Lives = lives;
            Price = price; Badge = badge; BadgeText = badgeText; Lines = lines; IconLabel = iconLabel; Timer = timer; TimerLabel = timerLabel;
        }
    }

    /// <summary>A Store section: its header (null = none, as Money Sort's Remove Ads and Starter cards) and its cards.</summary>
    public sealed class ShopSectionVisual
    {
        public readonly string Header;
        public readonly ShopCardKind Kind;
        public readonly IReadOnlyList<ShopCardVisual> Cards;

        public ShopSectionVisual(string header, ShopCardKind kind, IReadOnlyList<ShopCardVisual> cards) { Header = header; Kind = kind; Cards = cards; }
    }
}
