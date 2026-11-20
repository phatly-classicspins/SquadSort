using System;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace Game.Application
{
    /// <summary>
    /// Tester-only changes to the save (D-048): jump to a level, add coins / boosters, set lives, reset the tutorial or the
    /// whole profile. Registered only where the cheat menu is available. Like <see cref="LevelProgressService"/>, every
    /// change is persisted, and a storage failure restores the previous state (G18) and returns false.
    /// </summary>
    public sealed class ProgressCheats
    {
        private readonly IProgressStore _store;
        private readonly IWalletService _wallet;
        private readonly EconomyTuning _tuning;

        public ProgressCheats(IProgressStore store, IWalletService wallet, EconomyTuning tuning)
        {
            _store = store; _wallet = wallet; _tuning = tuning;
        }

        /// <summary>Make <paramref name="level"/> the current level as if every level before it was just cleared (clamped to
        /// 1…<paramref name="levelCount"/>). Returns the level set.</summary>
        public int SetLevel(int level, int levelCount)
        {
            int last = levelCount > 0 ? levelCount : _tuning.LevelCount;
            int target = Math.Max(1, Math.Min(level, last));
            Transact(p => { p.CurrentLevel = target; p.HighestCleared = target - 1; p.Attempts = 0; });
            return target;
        }

        public bool AddCoins(long amount)
        {
            if (amount <= 0) return true;
            _wallet.Grant(SqadSortResources.Coin, amount, GrantSource.Compensation);
            if (Transact(_ => { })) return true;
            _wallet.TrySpend(SqadSortResources.Coin, amount, GrantSource.Compensation);
            return false;
        }

        /// <summary>Add <paramref name="amount"/> of one booster (negative takes away, never below zero).</summary>
        public bool AddBooster(Booster id, int amount) => Transact(p =>
        {
            if (id == Booster.Undo) p.BoosterUndo = Math.Max(0, p.BoosterUndo + amount);
            else if (id == Booster.Shuffle) p.BoosterShuffle = Math.Max(0, p.BoosterShuffle + amount);
            else p.BoosterAddContainer = Math.Max(0, p.BoosterAddContainer + amount);
        });

        /// <summary>Unlock every booster and element without their popups or gifts.</summary>
        public bool UnlockAll() => Transact(p =>
        {
            foreach (var name in new[] { "booster_undo", "booster_shuffle", "booster_add_container" })
                if (!p.BoostersUnlocked.Contains(name)) p.BoostersUnlocked.Add(name);
            foreach (var name in new[] { "element_hidden", "element_house" })
                if (!p.ElementsUnlocked.Contains(name)) p.ElementsUnlocked.Add(name);
        });

        /// <summary>Set the lives to <paramref name="lives"/> (0…max). Below max the refill clock starts at <paramref name="now"/>.</summary>
        public bool SetLives(int lives, DateTimeOffset now)
        {
            int n = Math.Max(0, Math.Min(_tuning.LivesMax, lives));
            return Transact(p =>
            {
                p.Lives = n;
                p.LivesRefillFromUtc = n >= _tuning.LivesMax ? 0 : now.ToUnixTimeSeconds();
            });
        }

        /// <summary>Show the level 1 tutorial again.</summary>
        public bool ResetTutorial() => Transact(p =>
        {
            p.FtueCompleted.Clear();
        });

        /// <summary>D-056: the rating prompt may be offered again (after the next win at or above its level).</summary>
        public bool ResetRating() => Transact(p => p.RatingPromptShown = false);

        /// <summary>D-064: as if <paramref name="level"/> had just been lost one time short of the Skip offer, with today's skips
        /// unused — the next loss there shows the Skip row.</summary>
        /// <summary>D-067: forget the Starter Pack — not bought, window not open — so Home at level 12+ opens it again.</summary>
        public bool ResetStarterOffer() => Transact(p => { p.StarterOfferFromUtc = 0; p.PacksBought.Remove(ShopCatalog.StarterId); });

        /// <summary>D-070: end the Gold Brick cooldown and any open window, so the next level complete (level 10+) or app start offers it.</summary>
        public bool GoldBrickReady() => Transact(p => { p.GoldBrickActiveEndsUtc = 0; p.GoldBrickNextAvailableUtc = 0; p.GoldBrickShown = false; });

        public bool PrimeSkip(int level) => Transact(p =>
        {
            p.SkipFailLevel = level;
            p.SkipFailCount = Math.Max(0, _tuning.SkipLevelFailCount - 1);
            p.SkipDay = 0; p.SkipsToday = 0;
        });

        /// <summary>Back to a fresh install: default progress and no coins (the start coins come again on the next screen).</summary>
        public bool ResetAll()
        {
            long coins = _wallet.Balance(SqadSortResources.Coin);
            if (coins > 0 && !_wallet.TrySpend(SqadSortResources.Coin, coins, GrantSource.Compensation)) return false;
            if (Transact(p => p.CopyFrom(new ProgressModel()))) return true;
            if (coins > 0) _wallet.Grant(SqadSortResources.Coin, coins, GrantSource.Compensation);
            return false;
        }

        private bool Transact(Action<ProgressModel> change)
        {
            var before = _store.Progress.Clone();
            try
            {
                change(_store.Progress);
                _store.Save();
                return true;
            }
            catch (Exception)
            {
                _store.Progress.CopyFrom(before);
                return false;
            }
        }
    }
}
