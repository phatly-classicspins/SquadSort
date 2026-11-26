using System;
using System.Collections.Generic;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace Game.Application
{
    /// <summary>What happened when a level was started.</summary>
    public readonly struct LevelStartInfo
    {
        public readonly int Level, AttemptNo;
        public LevelStartInfo(int level, int attemptNo) { Level = level; AttemptNo = attemptNo; }
    }

    /// <summary>The outcome of a win, already persisted when returned (G19). <see cref="Reward"/> coins are
    /// already in the wallet.</summary>
    public readonly struct WinInfo
    {
        public readonly bool Saved;
        public readonly int Level, NextLevel, Reward;
        public readonly bool Replay, Final;
        public WinInfo(bool saved, int level, int nextLevel, int reward, bool replay, bool final)
        {
            Saved = saved; Level = level; NextLevel = nextLevel; Reward = reward; Replay = replay; Final = final;
        }
    }

    /// <summary>
    /// Level progression and coins (features/level-progression.md, GDD v2.0 §5, §7; CR-012 C1). Every change is
    /// one transaction: mutate in memory (and the wallet), persist, and on a storage failure restore the previous
    /// state so nothing is shown that was not saved (G18, G19). Coins move only through <see cref="IWalletService"/>.
    /// </summary>
    public sealed class LevelProgressService
    {
        private readonly IProgressStore _store;
        private readonly IWalletService _wallet;
        private readonly EconomyTuning _tuning;
        private readonly IProgressFacts _facts;

        public LevelProgressService(IProgressStore store, IWalletService wallet, EconomyTuning tuning, IProgressFacts facts)
        {
            _store = store; _wallet = wallet; _tuning = tuning; _facts = facts;
        }

        public int CurrentLevel => _store.Progress.CurrentLevel;
        public int HomeLevel(int catalogCount) => _tuning.CanLoopLevels(catalogCount) ? CurrentLevel : Math.Min(CurrentLevel, catalogCount);
        public bool LevelsLoop(int catalogCount) => _tuning.CanLoopLevels(catalogCount);
        public int HighestCleared => _store.Progress.HighestCleared;
        public long Coins => _wallet.Balance(SqadSortResources.Coin);

        /// <summary>Grant the starting coins once per install. False if saving failed (rolled back).</summary>
        public bool EnsureStartCoins()
        {
            if (_store.Progress.StartCoinsGranted) return true;
            if (!Transact(p => p.StartCoinsGranted = true, _tuning.StartCoins)) return false;
            _facts.Earned(TrackingNames.Coins, _tuning.StartCoins, TrackingNames.InstallGame);
            return true;
        }

        /// <summary>Count the attempt.</summary>
        public LevelStartInfo BeginLevel(int level)
        {
            Transact(p => p.Attempts++, 0);
            return new LevelStartInfo(level, _store.Progress.Attempts);
        }

        /// <summary>Advance and pay the win reward; persisted before returning (the dialog shows saved values).
        /// <paramref name="levelCount"/> is the shipped catalog's count when the caller knows it (0 = the tuning's fallback),
        /// so Next never points past the last level that exists. Replaying an old level never moves the saved
        /// current level back.</summary>
        public WinInfo CompleteLevel(int level, int levelCount = 0) => Complete(level, levelCount, skippedAt: null);

        // a win, or a skip (D-064: it counts as done and still pays the win coins, and uses one of today's skips)
        private WinInfo Complete(int level, int levelCount, DateTimeOffset? skippedAt)
        {
            int last = levelCount > 0 ? levelCount : _tuning.LevelCount;
            bool replay = level <= _store.Progress.HighestCleared;
            bool loops = _tuning.CanLoopLevels(last);
            int next = loops ? Math.Min(level, int.MaxValue - 1) + 1 : Math.Min(level + 1, last);
            int reward = _tuning.WinReward;
            bool ok = Transact(m =>
            {
                m.HighestCleared = Math.Max(m.HighestCleared, level);
                m.CurrentLevel = loops ? Math.Max(m.CurrentLevel, next) : Math.Min(Math.Max(m.CurrentLevel, next), last);
                m.Attempts = 0;
                m.WinsSinceInterstitial++;
                m.SkipFailCount = 0;   // D-064: a win or a skip ends the losing streak
                if (skippedAt is DateTimeOffset at)
                {
                    int day = Day(at);
                    m.SkipsToday = (m.SkipDay == day ? m.SkipsToday : 0) + 1;
                    m.SkipDay = day;
                }
            }, reward);
            if (ok && !replay) _facts.FirstClear(level);   // D-057: only once saved, and never for a replay
            if (ok) _facts.Earned(TrackingNames.Coins, reward, TrackingNames.LevelUpPlacement);
            return new WinInfo(ok, level, ok ? next : level, ok ? reward : 0, replay, !loops && level >= last);
        }

        // ── Skip Level (D-064, features/skip-level.md) ────────────────────────────────────────────────

        /// <summary>An attempt at <paramref name="level"/> ended lost (Retry or Home from the lose dialog): one more in the streak. A loss
        /// on another level starts a new streak. Best effort — a failed save only loses the count.</summary>
        public void RecordLoss(int level) => Transact(p =>
        {
            if (p.SkipFailLevel == level) p.SkipFailCount++;
            else { p.SkipFailLevel = level; p.SkipFailCount = 1; }
        }, 0);

        /// <summary>Skips still allowed today (<c>skip_level_max_per_day</c>, local date).</summary>
        public int SkipsLeft(DateTimeOffset now)
        {
            var p = _store.Progress;
            int used = p.SkipDay == Day(now) ? p.SkipsToday : 0;
            return Math.Max(0, _tuning.SkipLevelMaxPerDay - used);
        }

        /// <summary>Whether the lose dialog now ending an attempt at <paramref name="level"/> offers Skip: this loss is at least the
        /// <c>skip_level_fail_count</c>-th in a row there, the level is unlocked for it, it is neither a tutorial level nor one that
        /// introduces a booster or an element, and a skip is left today.</summary>
        public bool CanOfferSkip(int level, bool tutorialLevel, DateTimeOffset now)
        {
            if (tutorialLevel || level < _tuning.SkipLevelUnlockLevel || IntroducesSomething(level) || SkipsLeft(now) <= 0) return false;
            var p = _store.Progress;
            int before = p.SkipFailLevel == level ? p.SkipFailCount : 0;
            return before + 1 >= _tuning.SkipLevelFailCount;
        }

        // MoneySort's restart help offer: after two losses, once per level, only after Magnet (Remove) unlock.
        /// <summary>Skip on a fresh retry, after the loss has already been recorded.</summary>
        public bool CanOfferSkipAfterRestart(int level, bool tutorialLevel, DateTimeOffset now) =>
            CanOfferSkip(level, tutorialLevel, now) && _store.Progress.SkipFailLevel == level &&
            _store.Progress.SkipFailCount >= _tuning.SkipLevelFailCount;
        public bool CanOfferRestartBooster(int level, DateTimeOffset now = default) => RetryBoostersLeft(now) > 0 && IsUnlocked(Booster.Remove) &&
            _store.Progress.SkipFailLevel == level && _store.Progress.SkipFailCount >= 2 &&
            _store.Progress.RestartBoosterOfferedLevel != level;

        public bool TryMarkRestartBoosterOffered(int level, DateTimeOffset now = default) => CanOfferRestartBooster(level, now) &&
            Transact(p => p.RestartBoosterOfferedLevel = level, 0);
        public int SkipLevelCoinCost => _tuning.SkipLevelCoinCost;
        public bool CanAffordSkip => Coins >= _tuning.SkipLevelCoinCost;

        /// <summary>Skip after a completed rewarded ad (G19): the level counts as won.</summary>
        public WinInfo SkipLevel(int level, int levelCount, DateTimeOffset now) => Complete(level, levelCount, now);

        /// <summary>Skip for <c>skip_level_coin_cost</c> coins: pay, then count the level as won — false, and nothing spent or saved, when
        /// short or when saving failed (G18).</summary>
        public bool TrySkipWithCoins(int level, int levelCount, DateTimeOffset now, out WinInfo win)
        {
            win = default;
            long price = _tuning.SkipLevelCoinCost;
            if (!_wallet.TrySpend(SqadSortResources.Coin, price, GrantSource.Reward)) return false;
            win = Complete(level, levelCount, now);
            if (!win.Saved)
            {
                _wallet.Grant(SqadSortResources.Coin, price, GrantSource.Compensation);
                return false;
            }
            _facts.Spent(TrackingNames.Coins, price, TrackingNames.SkipLevelPlacement);
            return true;
        }

        private bool IntroducesSomething(int level)
        {
            foreach (Booster id in Enum.GetValues(typeof(Booster))) if (_tuning.UnlockLevel(id) == level) return true;
            foreach (ElementId id in Enum.GetValues(typeof(ElementId))) if (_tuning.UnlockLevel(id) == level) return true;
            return false;
        }

        // the player's calendar day (a new day gives the daily skips back)
        private static int Day(DateTimeOffset now)
        {
            var local = now.ToLocalTime();
            return local.Year * 10000 + local.Month * 100 + local.Day;
        }

        /// <summary>The win bonus after a completed rewarded ad: the extra (N − 1) × reward, N = the multiplier the needle stopped on
        /// (D-076), or <see cref="EconomyTuning.WinAdMultiplier"/> when none is given. False if saving failed.</summary>
        public bool GrantWinBonus(int reward, int multiplier = 0)
        {
            int n = multiplier >= 1 ? multiplier : _tuning.WinAdMultiplier;
            long extra = (long)Math.Max(0, reward) * Math.Max(0, n - 1);
            if (!Transact(_ => { }, extra)) return false;
            _facts.Earned(TrackingNames.Coins, extra, TrackingNames.FreeCoins);
            return true;
        }

        /// <summary>Spend coins on <paramref name="placement"/> (D-061, e.g. <see cref="TrackingNames.Revive"/>). False — and
        /// nothing spent — if the balance is short or saving failed.</summary>
        public bool TrySpendCoins(long amount, string placement)
        {
            if (amount <= 0) return true;
            if (!_wallet.TrySpend(SqadSortResources.Coin, amount, GrantSource.Reward)) return false;
            try { _store.Save(); _facts.Spent(TrackingNames.Coins, amount, placement); return true; }
            catch (Exception)
            {
                _wallet.Grant(SqadSortResources.Coin, amount, GrantSource.Compensation);
                return false;
            }
        }

        // ── boosters (CR-012 C2, features/boosters.md v2) ─────────────────────────────────────────────

        public int Boosters(Booster id) => Count(_store.Progress, id);
        public bool IsUnlocked(Booster id) => _store.Progress.BoostersUnlocked.Contains(Name(id));

        /// <summary>Unlock every booster whose level <paramref name="level"/> has reached and give its gift, once each
        /// (a save from before C2 past level 5 gets them at its next start). Returns the ones unlocked now, in bar
        /// order; empty if none, or if saving failed (rolled back, they come again next start).</summary>
        public Booster[] UnlockBoostersFor(int level)
        {
            var now = new System.Collections.Generic.List<Booster>();
            foreach (Booster id in Enum.GetValues(typeof(Booster)))
                if (level >= _tuning.UnlockLevel(id) && !IsUnlocked(id)) now.Add(id);
            if (now.Count == 0) return Array.Empty<Booster>();
            bool ok = Transact(p =>
            {
                foreach (var id in now) { p.BoostersUnlocked.Add(Name(id)); Add(p, id, _tuning.BoosterUnlockGift); }
            }, 0);
            if (!ok) return Array.Empty<Booster>();
            foreach (var id in now)
            {
                _facts.FeatureUnlocked(TrackingNames.Booster(id));
                _facts.Earned(TrackingNames.Booster(id), _tuning.BoosterUnlockGift, TrackingNames.UnlockGift);
            }
            return now.ToArray();
        }

        // ── lives (CR-012 E, features/lives.md) ───────────────────────────────────────────────────────
        // GDD §1.3 / Money Sort (Phat, 04-11-2026): a life goes only when the player confirms leaving an attempt — restart after a
        // loss, restart by hand, or Home. Starting, winning, reviving, skipping and a killed app cost nothing. `now` comes from the
        // caller's IClock.

        /// <summary>Lives right now, after any refill since the last look (persisted when it changed). D-067: the maximum while
        /// unlimited lives run, so nothing asks for one.</summary>
        public int Lives(DateTimeOffset now)
        {
            RefreshLives(now);
            return UnlimitedLivesLeft(now) > TimeSpan.Zero ? Math.Max(_tuning.LivesMax, _store.Progress.Lives) : _store.Progress.Lives;
        }

        /// <summary>D-067 (features/shop.md §5): time left on unlimited lives from IAP packs; zero when none.</summary>
        public TimeSpan UnlimitedLivesLeft(DateTimeOffset now) =>
            TimeSpan.FromSeconds(Math.Max(0, _store.Progress.UnlimitedLivesUntilUtc - now.ToUnixTimeSeconds()));

        /// <summary>Time until the next life; zero when full.</summary>
        public TimeSpan NextLifeIn(DateTimeOffset now)
        {
            RefreshLives(now);
            var p = _store.Progress;
            if (p.Lives >= _tuning.LivesMax || p.LivesRefillFromUtc == 0) return TimeSpan.Zero;
            long left = p.LivesRefillFromUtc + RefillSeconds - now.ToUnixTimeSeconds();
            return TimeSpan.FromSeconds(Math.Max(0, left));
        }

        /// <summary>Take the life that leaving an attempt costs. False — nothing taken — when there is none or saving failed.
        /// D-067: free while unlimited lives run.</summary>
        public bool TrySpendLife(DateTimeOffset now)
        {
            if (UnlimitedLivesLeft(now) > TimeSpan.Zero) return true;
            if (Lives(now) <= 0) return false;
            long t = now.ToUnixTimeSeconds();
            return Transact(p =>
            {
                p.Lives--;
                if (p.LivesRefillFromUtc == 0) p.LivesRefillFromUtc = t;   // was full: the next one starts counting now
            }, 0);
        }

        /// <summary>The rewarded ad on Out of Lives (G19: only after the ad reported completion): <see cref="EconomyTuning.LivesRefillAdAmount"/>.</summary>
        public bool GrantLifeFromAd(DateTimeOffset now)
        {
            int before = Lives(now);
            if (!GainLife(now, _tuning.LivesRefillAdAmount)) return false;
            _facts.Earned(TrackingNames.Lives, _store.Progress.Lives - before, TrackingNames.OutOfLivesRv);   // what the save received (capped)
            return true;
        }

        /// <summary>D-062: the coin refill on Out of Lives — <see cref="EconomyTuning.LivesRefillCoinCost"/> coins for
        /// <see cref="EconomyTuning.LivesRefillCoinAmount"/> lives (never above the maximum), as one transaction: false — no coins
        /// spent, no life given — if the balance is short or saving failed (G18).</summary>
        public bool TryRefillLivesWithCoins(DateTimeOffset now)
        {
            int before = Lives(now);
            long price = _tuning.LivesRefillCoinCost;
            if (!_wallet.TrySpend(SqadSortResources.Coin, price, GrantSource.Reward)) return false;
            var snapshot = _store.Progress.Clone();
            try
            {
                AddLives(_store.Progress, _tuning.LivesRefillCoinAmount);
                _store.Save();
            }
            catch (Exception)
            {
                _store.Progress.CopyFrom(snapshot);
                _wallet.Grant(SqadSortResources.Coin, price, GrantSource.Compensation);
                return false;
            }
            _facts.Spent(TrackingNames.Coins, price, TrackingNames.OutOfLivesRefill);
            _facts.Earned(TrackingNames.Lives, _store.Progress.Lives - before, TrackingNames.OutOfLivesCoins);
            return true;
        }

        /// <summary>D-062: what the coin refill costs, and whether the wallet covers it now.</summary>
        public int LivesRefillCoinCost => _tuning.LivesRefillCoinCost;
        public bool CanAffordLivesRefill => Coins >= _tuning.LivesRefillCoinCost;

        private bool GainLife(DateTimeOffset now, int amount = 1)
        {
            RefreshLives(now);
            return Transact(p => AddLives(p, amount), 0);
        }

        private void AddLives(ProgressModel p, int amount)
        {
            p.Lives = Math.Min(_tuning.LivesMax, p.Lives + Math.Max(0, amount));
            if (p.Lives >= _tuning.LivesMax) p.LivesRefillFromUtc = 0;   // full: the clock stops
        }

        private long RefillSeconds => Math.Max(1, _tuning.LivesRefillMinutes) * 60L;

        // whole periods since the mark become lives; the mark moves by what was counted. A clock that went back before the
        // mark restarts it at now without adding anything (GDD v2.0 §10).
        private void RefreshLives(DateTimeOffset now)
        {
            var p = _store.Progress;
            int lives = p.Lives; long mark = p.LivesRefillFromUtc, t = now.ToUnixTimeSeconds();
            if (lives >= _tuning.LivesMax) { lives = _tuning.LivesMax; mark = 0; }
            else if (mark == 0 || t < mark) mark = t;
            else
            {
                long gained = (t - mark) / RefillSeconds;
                if (gained > 0)
                {
                    lives = (int)Math.Min(_tuning.LivesMax, lives + gained);
                    mark = lives >= _tuning.LivesMax ? 0 : mark + gained * RefillSeconds;
                }
            }
            if (lives != p.Lives || mark != p.LivesRefillFromUtc)
                Transact(m => { m.Lives = lives; m.LivesRefillFromUtc = mark; }, 0);   // a failed save keeps the old values; next look tries again
        }

        // ── elements (CR-012 D, features/elements.md) ─────────────────────────────────────────────────

        public bool IsUnlocked(ElementId id) => _store.Progress.ElementsUnlocked.Contains(Name(id));

        /// <summary>Record every element whose level <paramref name="level"/> has reached. Returns the one introduced by
        /// this very level (its popup shows); elements of earlier levels — an older save — are recorded silently.
        /// Empty if none, or if saving failed (rolled back, it comes again next start).</summary>
        public ElementId[] UnlockElementsFor(int level)
        {
            var now = new System.Collections.Generic.List<ElementId>();
            foreach (ElementId id in Enum.GetValues(typeof(ElementId)))
                if (level >= _tuning.UnlockLevel(id) && !IsUnlocked(id)) now.Add(id);
            if (now.Count == 0) return Array.Empty<ElementId>();
            if (!Transact(p => { foreach (var id in now) p.ElementsUnlocked.Add(Name(id)); }, 0)) return Array.Empty<ElementId>();
            foreach (var id in now) _facts.FeatureUnlocked(TrackingNames.Element(id));
            return now.FindAll(id => _tuning.UnlockLevel(id) == level).ToArray();
        }

        private static string Name(ElementId id) => id == ElementId.Hidden ? "element_hidden" : "element_house";

        /// <summary>Use one: false — nothing taken — if there is none or saving failed.</summary>
        public bool TryUseBooster(Booster id)
        {
            if (Boosters(id) <= 0) return false;
            if (!Transact(p => Add(p, id, -1), 0)) return false;
            _facts.Spent(TrackingNames.Booster(id), 1, TrackingNames.ActionPhase);
            return true;
        }

        /// <summary>Buy <see cref="EconomyTuning.BuyAmount"/> for its coin price, as one transaction: false — no coins
        /// spent, no booster given — if the balance is short or saving failed (G18).</summary>
        public bool TryBuyBooster(Booster id)
        {
            long price = _tuning.Price(id);
            if (!_wallet.TrySpend(SqadSortResources.Coin, price, GrantSource.Reward)) return false;
            var before = _store.Progress.Clone();
            try
            {
                Add(_store.Progress, id, _tuning.BuyAmount(id));
                _store.Save();
                _facts.Spent(TrackingNames.Coins, price, TrackingNames.Booster(id));          // Money Sort: the coins are spent "on" the booster
                _facts.Earned(TrackingNames.Booster(id), _tuning.BuyAmount(id), TrackingNames.ActionPhase);
                return true;
            }
            catch (Exception)
            {
                _store.Progress.CopyFrom(before);
                _wallet.Grant(SqadSortResources.Coin, price, GrantSource.Compensation);
                return false;
            }
        }

        /// <summary>"▶ Free" after a completed rewarded ad (G19). False if saving failed.</summary>
        public bool GrantBoosterFromAd(Booster id)
        {
            if (!Transact(p => Add(p, id, _tuning.AdAmount(id)), 0)) return false;
            _facts.Earned(TrackingNames.Booster(id), _tuning.AdAmount(id), TrackingNames.ActionPhase);
            return true;
        }

        /// <summary>The restart mockup offers exactly one Magnet, after a completed rewarded ad.</summary>
        public int RetryBoostersLeft(DateTimeOffset now)
        {
            int used = _store.Progress.RetryBoosterDay == Day(now) ? _store.Progress.RetryBoostersToday : 0;
            return Math.Max(0, _tuning.RetryBoosterMaxPerDay - used);
        }

        public bool GrantRestartBoosterFromAd(DateTimeOffset now = default)
        {
            if (RetryBoostersLeft(now) <= 0) return false;
            if (!Transact(p => {
                Add(p, Booster.Remove, 1);
                p.RetryBoostersToday = (p.RetryBoosterDay == Day(now) ? p.RetryBoostersToday : 0) + 1;
                p.RetryBoosterDay = Day(now);
            }, 0)) return false;
            _facts.Earned(TrackingNames.Booster(Booster.Remove), 1, TrackingNames.ActionPhase);
            return true;
        }
        private static string Name(Booster id) => id == Booster.Undo ? "booster_undo" : id == Booster.Shuffle ? "booster_shuffle" : "booster_add_container";

        private static int Count(ProgressModel p, Booster id) =>
            id == Booster.Undo ? p.BoosterUndo : id == Booster.Shuffle ? p.BoosterShuffle : p.BoosterAddContainer;

        private static void Add(ProgressModel p, Booster id, int n)
        {
            if (id == Booster.Undo) p.BoosterUndo = Math.Max(0, p.BoosterUndo + n);
            else if (id == Booster.Shuffle) p.BoosterShuffle = Math.Max(0, p.BoosterShuffle + n);
            else p.BoosterAddContainer = Math.Max(0, p.BoosterAddContainer + n);
        }

        // ── shop (D-067, features/shop.md) ────────────────────────────────────────────────────────────

        /// <summary>The packs on sale now, in Store order: a one-time pack goes once bought; the Starter Pack shows only inside
        /// its window (§3). <paramref name="removeAdsOwned"/> comes from the entitlement (a restore can set it without a save).</summary>
        public IReadOnlyList<ShopPack> ShopPacks(DateTimeOffset now, bool removeAdsOwned)
        {
            var list = new List<ShopPack>();
            foreach (var pack in ShopCatalog.All)
            {
                if (pack.OneTime && _store.Progress.PacksBought.Contains(pack.Id)) continue;
                if (pack.RemoveAds && removeAdsOwned) continue;
                if (pack.Id == ShopCatalog.StarterId && StarterOfferLeft(now) <= TimeSpan.Zero) continue;
                list.Add(pack);
            }
            return list;
        }

        /// <summary>Time left in the Starter Pack window; zero when not open, over, or bought.</summary>
        public TimeSpan StarterOfferLeft(DateTimeOffset now)
        {
            var p = _store.Progress;
            if (p.StarterOfferFromUtc == 0 || p.PacksBought.Contains(ShopCatalog.StarterId)) return TimeSpan.Zero;
            long end = p.StarterOfferFromUtc + _tuning.StarterPackDurationHours * 3600L;
            return TimeSpan.FromSeconds(Math.Max(0, end - now.ToUnixTimeSeconds()));
        }

        /// <summary>Grant a bought pack (§2 "Mua thành công"): each booster, unlimited lives (added to any time left, §5), and
        /// record a one-time pack — as one transaction, saved before the Rewards! popup shows. False — nothing granted — if
        /// saving failed. QA IAP-01: the pack's <b>coins</b> (and the <c>remove_ads</c> entitlement) are the store's grant in
        /// products.json, applied by the IAP module for every order — also one that arrives late (Ask-to-Buy, a pending
        /// payment, the app killed after paying) — so they are not granted here; <c>earn_virtual_currency</c> still reports
        /// them with the Shop placement.</summary>
        public bool GrantPurchase(ShopPack pack, DateTimeOffset now, string placement)
        {
            long t = now.ToUnixTimeSeconds();
            bool ok = Transact(p =>
            {
                foreach (Booster id in Enum.GetValues(typeof(Booster))) Add(p, id, pack.BoosterEach);
                if (pack.UnlimitedLivesHours > 0)
                    p.UnlimitedLivesUntilUtc = Math.Max(p.UnlimitedLivesUntilUtc, t) + pack.UnlimitedLivesHours * 3600L;
                if (pack.OneTime && !p.PacksBought.Contains(pack.Id)) p.PacksBought.Add(pack.Id);
            }, 0);
            if (!ok) return false;
            if (pack.Coins > 0) _facts.Earned(TrackingNames.Coins, pack.Coins, placement);
            if (pack.BoosterEach > 0)
                foreach (Booster id in Enum.GetValues(typeof(Booster))) _facts.Earned(TrackingNames.Booster(id), pack.BoosterEach, placement);
            return true;
        }

        /// <summary>D-070 Gold Brick Package!: the gift after its rewarded ad (G19) — coins and boosters in one save. False if saving failed.</summary>
        public bool GrantGift(long coins, Booster booster, int boosters)
        {
            if (!Transact(p => Add(p, booster, boosters), coins)) return false;
            if (coins > 0) _facts.Earned(TrackingNames.Coins, coins, TrackingNames.GoldBrickReward);
            if (boosters > 0) _facts.Earned(TrackingNames.Booster(booster), boosters, TrackingNames.GoldBrickReward);
            return true;
        }

        public bool MarkFtueDone(string step)
        {
            if (_store.Progress.FtueCompleted.Contains(step)) return true;
            return Transact(p => p.FtueCompleted.Add(step), 0);
        }

        public bool IsFtueDone(string step) => _store.Progress.FtueCompleted.Contains(step);

        /// <summary>D-056: the rating prompt was offered already.</summary>
        public bool RatingPromptShown => _store.Progress.RatingPromptShown;

        /// <summary>Record the offer before the prompt shows; false — not recorded, so the prompt is skipped — if saving failed (G18).</summary>
        public bool MarkRatingPromptShown() => RatingPromptShown || Transact(p => p.RatingPromptShown = true, 0);

        // mutate progress (+ optionally grant coins) and persist, or restore both
        private bool Transact(Action<ProgressModel> change, long coins)
        {
            var before = _store.Progress.Clone();
            bool granted = false;
            try
            {
                change(_store.Progress);
                if (coins > 0) { _wallet.Grant(SqadSortResources.Coin, coins, GrantSource.Reward); granted = true; }
                _store.Save();
                return true;
            }
            catch (Exception)
            {
                _store.Progress.CopyFrom(before);
                if (granted) _wallet.TrySpend(SqadSortResources.Coin, coins, GrantSource.Compensation);
                return false;
            }
        }
    }
}
