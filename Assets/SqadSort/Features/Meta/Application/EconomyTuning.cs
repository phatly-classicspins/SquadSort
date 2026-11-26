using System;
using Game.Domain;

namespace Game.Application
{
    /// <summary>
    /// The tunables of <c>docs/design/economy-sheet.md</c> §2 and GDD v2.0 §7. CR-012 stage B: Revive (free Removes)
    /// and RV Slot (more holding cells), coins (C1: start grant, win reward, ×2 by ad, revive price), plus
    /// interstitial pacing. The composition point may fill these from <c>IGameConfig</c>, so a balance change is a config
    /// change, never a code change (G20).
    /// </summary>
    public sealed class EconomyTuning
    {
        public int StartCoins = 100;            // GDD v2.0 §7, D-031
        public int WinReward = 40;              // CR-013: level_win_coin
        public int WinAdMultiplier = 2;         // the bonus multiplier when no needle decides it (the win bonus is the needle's, D-076)
        // D-076 (CR-013 H4, Money Sort level complete bonus wheel): the needle's segments left to right, its swing, and the pause
        // between stopping it and the rewarded ad
        public string WinBonusMultipliers = "2,3,5,3,2";   // level_win_ad_multipliers; read through WinBonusSegments()
        public double WinBonusArcDegrees = 84;
        public double WinBonusSwingsPerSecond = 0.8;
        public double WinBonusAdDelaySeconds = 1;
        public int RevivePrice = 900;           // CR-013: revive_base_cost, coins for the first revive of an attempt …
        public double RevivePriceGrowth = 1.0;  // … × this for each further one (revive_cost_growth; 1 = same price)
        public int ReviveRemoves = 2;           // R-22: poles a revive clears for free
        public int ReviveMaxAdsPerAttempt = 1;
        public int RetryBoosterMaxPerDay = 3;
        public int LevelLoopLastLevel = 40, LevelLoopStartLevel = 21;
        public int ReviveMaxPerAttempt = 3;     // GDD v2.0 §7 revive.max_per_level [XÁC NHẬN]
        public int RvSlotAmount = 5;            // R-23; D-119 (Ethan 11-11): 20 slots shown, 10 open, two RVs of +5 (D-100 was +3)
        public int RvSlotMaxPerAttempt = 2;
        // D-138, GDD 1.3 "Low slot warning" (rv_nudge_*): red blink at this many free slots or fewer; bubble on screen, least
        // time between two, most per attempt, attempts ignored in a row before only 1
        public int RvNudgeFreeSlots = 2;
        public double RvNudgeBubbleSeconds = 5, RvNudgeBubbleCooldownSeconds = 30;
        public int RvNudgeBubbleMaxPerAttempt = 2, RvNudgeIgnoreStreak = 3;
        public int LoseBoosterHintSeconds = 3;  // D-136, GDD 1.3 "Before Revive: booster hint" (lose_booster_hint_seconds): 0 = straight to Revive
        public int RvFreeUpToLevel = 3;         // D-129 (Ethan 12-11): levels 1..this start with every RV slot open, nothing locked behind an ad
        // C2 boosters (GDD v2.0 §4.1, §7, D-031): unlock level, coin price, how many one purchase / one ad gives (per
        // booster, CR-013: booster_{id}_coin_amount, booster_{id}_ad_amount), unlock gift
        public int UndoUnlockLevel = 5, ShuffleUnlockLevel = 11, AddContainerUnlockLevel = 15;   // SqadSort GDD §3
        // AddContainer* hold the Remove booster's values (Squad Sort GDD §1.5): the remote-config keys keep their
        // booster_add_container_* names until a codegen run renames them, and every key must match a field of its name.
        public int UndoPrice = 100, ShufflePrice = 80, AddContainerPrice = 120;
        public int UndoBuyAmount = 3, ShuffleBuyAmount = 3, AddContainerBuyAmount = 3;
        public int UndoAdAmount = 1, ShuffleAdAmount = 1, AddContainerAdAmount = 1;   // "▶ Free" in the buy dialog
        public int BoosterUnlockGift = 3;
        // D elements (GDD v2.0 §4.1): the level that introduces each — also the milestones of the Win progress bar (§4.2)
        public int HiddenLevel = 8, HouseLevel = 20;   // SqadSort GDD §2.2b-11/12
        // E lives (GDD v2.0 §4.3, §7 lives.max / lives.refill_minutes)
        public int LivesMax = 5;
        public int LivesRefillMinutes = 20;
        // D-062 (CR-013 H2, GDD "Refill lives"): Out of Lives refills with coins or with an ad
        public int LivesRefillCoinCost = 900;
        public int LivesRefillCoinAmount = 5;
        public int LivesRefillAdAmount = 1;
        // D-063 (Online game, Money Sort): offline players without Remove Ads are held by the No Internet popup; checked this often
        public bool RequireInternet = true;
        public int InternetCheckSeconds = 3;
        // D-064 Skip Level (CR-013 #17): offered from the 3rd loss in a row on a level; unlock level and coin price are placeholders (GDD TBD)
        public int SkipLevelUnlockLevel = 5;
        public int SkipLevelFailCount = 3;
        public int SkipLevelMaxPerDay = 3;
        public int SkipLevelCoinCost = 900;
        // D-067 Shop (features/shop.md §8): the Starter Pack window and the Remove Ads popup unlock
        public int StarterPackUnlockLevel = 12;
        public int StarterPackDurationHours = 72;
        public int RemoveAdsUnlockLevel = 15;
        // D-070 Gold Brick Package! / Limited Time Reward (GDD Card Slots 6, Block Sort 7.3): 200 coins + 1 Hand for a rewarded ad,
        // from level 10, claimable for 10 minutes once shown, then a 4-hour cooldown
        public int GoldBrickUnlockLevel = 10;
        public int GoldBrickCoins = 200;
        public int GoldBrickActiveMinutes = 10;
        public int GoldBrickCooldownHours = 4;
        // F legal links (features/ftue.md v2, D-042): placeholders — replace before release
        public string TermsUrl = "https://example.com/terms";
        public string PrivacyUrl = "https://example.com/privacy";
        // D-115 (P9 tech doc, Inter_level / Inter_break_time): an interstitial after each level-complete screen from level 12,
        // at least 180 s after the previous interstitial (rewarded ads do not restart the wait)
        public int InterstitialFirstLevel = 12;
        public int InterstitialEveryNWins = 1;
        public double InterstitialMinIntervalSeconds = 180;
        // D-061 / D-115 (Banner_level): the bottom banner shows during play from this level on; 0 = never (GameplayBanner).
        public int BannerFirstLevel = 3;
        // D-056 rating prompt (Money Sort flow, com.classicspins.rating): after a win of this level or later, once; 0 = off.
        // Store ids are empty until the store pages exist: then every rating ends on the thank-you without leaving the game.
        public int RatingPromptLevel = 15;
        public int RatingStoreMinStars = 4;
        public string RatingIosAppId = "";   // TODO(sdk-ids): SqadSort's App Store id once the app exists; empty = 4-5 stars only thank the player (was Chipsaway's, D-125)
        public string RatingAndroidPackage = "";   // empty = the build's own application identifier
        public string RatingFeedbackUrl = "https://brainbucks.com/contact";   // 1..(StoreMinStars-1) stars open this page
        public int LevelCount = 40;             // fallback only: the shipped catalog's count wins when the caller knows it

        /// <summary>Coins for the revive after <paramref name="paidBefore"/> paid ones this attempt: base × growth^n, rounded
        /// (900, 900, 900 … at growth 1; 100, 200, 400 … at growth 2). Never below 1 (a free paid revive is a bug) and
        /// saturates instead of overflowing.</summary>
        /// <summary>D-076: the needle's multipliers, left to right, from <see cref="WinBonusMultipliers"/> ("2,3,5,3,2"). A value that is not
        /// a whole number ≥ 1 is skipped; nothing usable left ⇒ the one segment <see cref="WinAdMultiplier"/> (a bad remote value never
        /// takes the bonus away).</summary>
        public int[] WinBonusSegments()
        {
            var list = new System.Collections.Generic.List<int>();
            string s = WinBonusMultipliers ?? string.Empty;
            int start = 0;
            for (int i = 0; i <= s.Length; i++)
            {
                if (i < s.Length && s[i] != ',') continue;
                if (int.TryParse(s.Substring(start, i - start).Trim(), System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out int m) && m >= 1)
                    list.Add(m);
                start = i + 1;
            }
            return list.Count > 0 ? list.ToArray() : new[] { Math.Max(1, WinAdMultiplier) };
        }

        public bool CanRewardedRevive(int used) => Math.Max(0, used) < ReviveMaxAdsPerAttempt;

        public bool CanLoopLevels(int catalogCount) => catalogCount >= LevelLoopLastLevel
            && LevelLoopStartLevel >= 1 && LevelLoopStartLevel <= LevelLoopLastLevel;

        /// <summary>Keep displayed progress increasing; select a shipped file for the repeated content.</summary>
        public int ContentLevel(int displayedLevel, int catalogCount)
        {
            int level = Math.Max(1, displayedLevel), count = Math.Max(1, catalogCount);
            // Newly shipped levels take precedence over old loop content.
            if (level <= count) return level;
            if (!CanLoopLevels(count)) return count;
            return LevelLoopStartLevel + (level - LevelLoopLastLevel - 1)
                % (LevelLoopLastLevel - LevelLoopStartLevel + 1);
        }

        public long RevivePriceAfter(int paidBefore)
        {
            double growth = double.IsNaN(RevivePriceGrowth) ? 1.0 : Math.Max(1.0, RevivePriceGrowth);
            double price = Math.Max(1, RevivePrice) * Math.Pow(growth, Math.Max(0, paidBefore));
            return price >= long.MaxValue ? long.MaxValue : Math.Max(1L, (long)Math.Round(price, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// Pull every value into the range the game can run on, so a bad remote value (0, negative) degrades to the nearest
        /// safe one instead of making revives free or lives impossible (G20). Returns this tuning.
        /// </summary>
        public EconomyTuning Sanitize()
        {
            StartCoins = Math.Max(0, StartCoins);
            WinReward = Math.Max(0, WinReward);
            WinAdMultiplier = Math.Max(1, WinAdMultiplier);
            WinBonusArcDegrees = double.IsNaN(WinBonusArcDegrees) ? 84 : Math.Max(10, WinBonusArcDegrees);   // the needle caps it at 90
            WinBonusSwingsPerSecond = double.IsNaN(WinBonusSwingsPerSecond) ? 0.8 : Math.Max(0.05, WinBonusSwingsPerSecond);
            WinBonusAdDelaySeconds = double.IsNaN(WinBonusAdDelaySeconds) ? 1 : Math.Max(0, WinBonusAdDelaySeconds);
            RevivePrice = Math.Max(1, RevivePrice);
            RevivePriceGrowth = double.IsNaN(RevivePriceGrowth) ? 1.0 : Math.Max(1.0, RevivePriceGrowth);
            ReviveRemoves = Math.Max(0, ReviveRemoves);
            ReviveMaxPerAttempt = Math.Max(0, ReviveMaxPerAttempt);
            ReviveMaxAdsPerAttempt = Math.Max(0, ReviveMaxAdsPerAttempt);
            RetryBoosterMaxPerDay = Math.Max(0, RetryBoosterMaxPerDay);
            LevelLoopLastLevel = Math.Max(1, LevelLoopLastLevel);
            LevelLoopStartLevel = Math.Max(1, LevelLoopStartLevel);
            RvSlotAmount = Math.Max(0, RvSlotAmount);
            RvSlotMaxPerAttempt = Math.Max(0, RvSlotMaxPerAttempt);
            RvNudgeFreeSlots = Math.Max(0, RvNudgeFreeSlots);
            RvNudgeBubbleSeconds = double.IsNaN(RvNudgeBubbleSeconds) ? 5 : Math.Max(0.5, RvNudgeBubbleSeconds);
            RvNudgeBubbleCooldownSeconds = double.IsNaN(RvNudgeBubbleCooldownSeconds) ? 30 : Math.Max(0, RvNudgeBubbleCooldownSeconds);
            RvNudgeBubbleMaxPerAttempt = Math.Max(0, RvNudgeBubbleMaxPerAttempt);
            RvNudgeIgnoreStreak = Math.Max(1, RvNudgeIgnoreStreak);
            RvFreeUpToLevel = Math.Max(0, RvFreeUpToLevel);
            LoseBoosterHintSeconds = Math.Max(0, LoseBoosterHintSeconds);
            UndoPrice = Math.Max(1, UndoPrice);
            ShufflePrice = Math.Max(1, ShufflePrice);
            AddContainerPrice = Math.Max(1, AddContainerPrice);
            UndoBuyAmount = Math.Max(1, UndoBuyAmount);
            ShuffleBuyAmount = Math.Max(1, ShuffleBuyAmount);
            AddContainerBuyAmount = Math.Max(1, AddContainerBuyAmount);
            UndoAdAmount = Math.Max(1, UndoAdAmount);
            ShuffleAdAmount = Math.Max(1, ShuffleAdAmount);
            AddContainerAdAmount = Math.Max(1, AddContainerAdAmount);
            BoosterUnlockGift = Math.Max(0, BoosterUnlockGift);
            LivesMax = Math.Max(1, LivesMax);
            LivesRefillMinutes = Math.Max(1, LivesRefillMinutes);
            LivesRefillCoinCost = Math.Max(1, LivesRefillCoinCost);
            LivesRefillCoinAmount = Math.Max(1, LivesRefillCoinAmount);
            LivesRefillAdAmount = Math.Max(1, LivesRefillAdAmount);
            InternetCheckSeconds = Math.Max(1, InternetCheckSeconds);
            SkipLevelUnlockLevel = Math.Max(1, SkipLevelUnlockLevel);
            SkipLevelFailCount = Math.Max(1, SkipLevelFailCount);
            SkipLevelMaxPerDay = Math.Max(0, SkipLevelMaxPerDay);
            SkipLevelCoinCost = Math.Max(1, SkipLevelCoinCost);
            StarterPackUnlockLevel = Math.Max(1, StarterPackUnlockLevel);
            StarterPackDurationHours = Math.Max(1, StarterPackDurationHours);
            RemoveAdsUnlockLevel = Math.Max(1, RemoveAdsUnlockLevel);
            GoldBrickUnlockLevel = Math.Max(1, GoldBrickUnlockLevel);
            GoldBrickCoins = Math.Max(0, GoldBrickCoins);
            GoldBrickActiveMinutes = Math.Max(1, GoldBrickActiveMinutes);
            GoldBrickCooldownHours = Math.Max(0, GoldBrickCooldownHours);
            InterstitialEveryNWins = Math.Max(1, InterstitialEveryNWins);
            InterstitialMinIntervalSeconds = Math.Max(0, InterstitialMinIntervalSeconds);
            BannerFirstLevel = Math.Max(0, BannerFirstLevel);
            LevelCount = Math.Max(1, LevelCount);
            return this;
        }

        public int UnlockLevel(Booster id) => id == Booster.Undo ? UndoUnlockLevel : id == Booster.Shuffle ? ShuffleUnlockLevel : AddContainerUnlockLevel;
        public int Price(Booster id) => id == Booster.Undo ? UndoPrice : id == Booster.Shuffle ? ShufflePrice : AddContainerPrice;
        public int BuyAmount(Booster id) => id == Booster.Undo ? UndoBuyAmount : id == Booster.Shuffle ? ShuffleBuyAmount : AddContainerBuyAmount;
        public int AdAmount(Booster id) => id == Booster.Undo ? UndoAdAmount : id == Booster.Shuffle ? ShuffleAdAmount : AddContainerAdAmount;
        public int UnlockLevel(ElementId id) => id == ElementId.Hidden ? HiddenLevel : HouseLevel;

        /// <summary>
        /// GDD v2.0 §4.2: the Win progress bar after winning <paramref name="wonLevel"/> — next = the smallest element
        /// level above it, prev = the largest at or below it (1 if none); total = next − prev, current = won − prev + 1.
        /// False when no element is left to introduce (the bar is hidden).
        /// </summary>
        public bool ElementProgress(int wonLevel, out int current, out int total, out ElementId next)
        {
            current = total = 0; next = default;
            int prev = 1, nextLevel = int.MaxValue;
            foreach (ElementId id in System.Enum.GetValues(typeof(ElementId)))
            {
                int at = UnlockLevel(id);
                if (at > wonLevel && at < nextLevel) { nextLevel = at; next = id; }
                if (at <= wonLevel && at > prev) prev = at;
            }
            if (nextLevel == int.MaxValue) return false;
            total = nextLevel - prev;
            current = wonLevel - prev + 1;
            return true;
        }

        public bool FeatureProgress(int wonLevel, int lastLevel, out int current, out int total,
            out Booster? booster, out ElementId? element)
        {
            int previous = 1;
            int next = int.MaxValue;
            Booster? nextBooster = null;
            ElementId? nextElement = null;

            // The level intro presents boosters before elements when both unlock together.
            foreach (Booster id in Enum.GetValues(typeof(Booster)))
                Consider(UnlockLevel(id), id, null);
            foreach (ElementId id in Enum.GetValues(typeof(ElementId)))
                Consider(UnlockLevel(id), null, id);

            booster = nextBooster;
            element = nextElement;
            current = next == int.MaxValue ? 0 : wonLevel - previous + 1;
            total = next == int.MaxValue ? 0 : next - previous;
            return next != int.MaxValue;

            void Consider(int level, Booster? candidateBooster, ElementId? candidateElement)
            {
                if (level <= wonLevel)
                    previous = Math.Max(previous, level);
                else if (level <= lastLevel && level < next)
                {
                    next = level;
                    nextBooster = candidateBooster;
                    nextElement = candidateElement;
                }
            }
        }

        /// <summary>D-129: the first <see cref="RvFreeUpToLevel"/> levels open every RV slot up front — no ad, nothing locked.</summary>
        public bool RvSlotsFree(int level) => level >= 1 && level <= RvFreeUpToLevel;
    }
}
