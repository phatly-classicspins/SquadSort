using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;

namespace Game.Application
{
    /// <summary>
    /// One event of the studio's tracking plan, as the dashboards name it (D-061: Money Sort's plan, re-used and adjusted).
    /// The plan is a flat list of names and parameters, so one record carries any of them; <see cref="GameTracking"/> is the
    /// only place that builds one, which keeps every name in <see cref="TrackingNames"/>. Values are int, long, double, bool
    /// or string.
    /// </summary>
    public sealed record TrackedEvent(string Name, IReadOnlyList<KeyValuePair<string, object>> Params) : GameEvent
    {
        public override string WireName() => Name;

        public override void WriteParams(IEventParamWriter w) => EventParams.Write(w, Params);

        public object this[string param]
        {
            get
            {
                foreach (var p in Params) if (p.Key == param) return p.Value;
                return null;
            }
        }

        // the console provider logs ToString(): name and parameters, not the list's type name
        public override string ToString()
        {
            var sb = new StringBuilder(Name).Append(" {");
            for (int i = 0; i < Params.Count; i++)
                sb.Append(i == 0 ? " " : ", ").Append(Params[i].Key).Append(" = ").Append(Convert.ToString(Params[i].Value, CultureInfo.InvariantCulture));
            return sb.Append(" }").ToString();
        }
    }

    /// <summary>
    /// D-097: the user id and user properties analytics carries on every later event (Money Sort <c>InitializeUserProperties</c>).
    /// Not a plan event: <see cref="GameTracking"/> sends it once a session, before anything else, and only a provider's map that
    /// knows it (Firebase) turns it into user properties.
    /// </summary>
    public sealed record UserPropertiesRecorded(string UserId, IReadOnlyList<KeyValuePair<string, object>> Properties) : GameEvent
    {
        public override string WireName() => "user_properties";

        public override void WriteParams(IEventParamWriter w)
        {
            w.Write("user_id", UserId);
            EventParams.Write(w, Properties);
        }

        public override string ToString()
        {
            var sb = new StringBuilder("user_properties { user_id = ").Append(UserId);
            foreach (var p in Properties) sb.Append(", ").Append(p.Key).Append(" = ").Append(Convert.ToString(p.Value, CultureInfo.InvariantCulture));
            return sb.Append(" }").ToString();
        }
    }

    /// <summary>Writes a plan event's loosely typed parameters through the framework's typed writer.</summary>
    internal static class EventParams
    {
        public static void Write(IEventParamWriter w, IReadOnlyList<KeyValuePair<string, object>> values)
        {
            foreach (var p in values)
            {
                switch (p.Value)
                {
                    case null: break;
                    case bool b: w.Write(p.Key, b); break;
                    case int i: w.Write(p.Key, (long)i); break;
                    case long l: w.Write(p.Key, l); break;
                    case float f: w.Write(p.Key, (double)f); break;
                    case double d: w.Write(p.Key, d); break;
                    default: w.Write(p.Key, Convert.ToString(p.Value, CultureInfo.InvariantCulture)); break;
                }
            }
        }
    }

    /// <summary>The tracking plan's names (D-061). Money Sort's spelling is kept so the studio's dashboards read both games.</summary>
    public static class TrackingNames
    {
        public const string LevelStarted = "level_started", LevelCompleted = "level_completed", LevelFailed = "level_failed",
            LevelAbandoned = "level_abandoned", LevelSkipped = "level_skipped", LevelUp = "level_up", ScreenView = "screen_view",
            EarnCurrency = "earn_virtual_currency", SpendCurrency = "spend_virtual_currency",
            IaaStart = "IAA_start", IaaCompleted = "IAA_completed", IaaFailed = "IAA_failed",
            MetaStarted = "meta_started", MetaCompleted = "meta_completed", FeatureUnlocked = "feature_unlocked",
            OutOfLivesShown = "out_of_lives_shown", UiInteracted = "ui_interacted", AppBackgrounded = "app_backgrounded",
            // D-071 Shop / offers (Money Sort ShopService, GarageGift)
            StoreEntered = "store_entered", IapStart = "IAP_start", IapCompleted = "IAP_completed", IapFailed = "IAP_failed",
            IapRestored = "IAP_restored", RvPopupShown = "rv_popup_shown", RvPopupExpired = "rv_popup_expired";

        // D-071: the Store's store_id, a purchase made outside a placement (restore), the remove-ads feature, unlimited lives
        public const string ShopStore = "shop", RestorePlacement = "restore", InfiniteLives = "infinite_lives";

        // D-097 user properties (Money Sort's names); media_source stays "unknown" until an attribution SDK reports one
        public const string UserId = "user_id", RetainedDays = "retained_days", MediaSource = "media_source", UnknownSource = "unknown";

        // currencies (virtual_currency_name)
        public const string Coins = "coins", Lives = "lives";

        // screens (screen_name)
        public const string ActionPhase = "action_phase", EndingPhase = "ending_phase", Home = "home", OutOfLivesPopup = "out_of_lives_popup";

        // placements
        public const string InstallGame = "install_game", LevelUpPlacement = "level_up", FreeCoins = "ap_free_coins", Revive = "ep_revive",
            RvSlotPlay = "ap_rv_slot", RvSlotLose = "ep_rv_slot", OutOfLivesRv = "out_of_lives_rv", LevelComplete = "level_complete", LevelFail = "level_fail",
            UnlockGift = "unlock_gift", OutOfLivesRefill = "out_of_lives_refill", OutOfLivesCoins = "out_of_lives_coins",
            SkipLevelPlacement = "ep_skip_level",
            HomeShop = "home_shop", StarterPopup = "starter_pack_popup", RemoveAdsPopup = "remove_ad_popup",   // D-067 Store opened from
            GoldBrickRv = "gold_block_reward", GoldBrickReward = "garage_reward";   // D-070 Gold Brick Package! (Money Sort names)

        // out_of_lives_shown trigger
        public const string TapPlay = "tap_play", LoseRestart = "lose_restart", ManualRestart = "manual_restart", NextLevel = "next_level", ExitHome = "exit_home";

        // ui_interacted ui_name
        public const string WatchAd = "watch_ad", Close = "close", BuyCoins = "buy_coins";

        /// <summary>A booster as a currency and as a feature: <c>ap_booster_undo</c> / <c>_shuffle</c> / <c>_remove</c>.</summary>
        public static string Booster(Game.Application.Booster id) =>
            id == Game.Application.Booster.Undo ? "ap_booster_undo"
            : id == Game.Application.Booster.Shuffle ? "ap_booster_shuffle" : "ap_booster_remove";

        /// <summary>An element as a feature, the name it has in the save.</summary>
        public static string Element(ElementId id) =>
            id == ElementId.Hidden ? "element_hidden" : "element_house";
    }

    /// <summary>
    /// What gameplay reports to analytics (D-061). Gameplay never calls <see cref="IAnalytics"/> itself (AR-25): it states what
    /// happened here and <see cref="GameTracking"/> turns it into the plan's events. Currency movements and unlocks come from
    /// <see cref="LevelProgressService"/> through <see cref="IProgressFacts"/>, once they are saved.
    /// </summary>
    public interface IGameTracking
    {
        /// <summary>An attempt began (a new board). An attempt still open is abandoned first.</summary>
        void LevelStarted(int level, int launch, int contentLevel = 0);
        /// <summary>A tap that moved cards (<c>level_counter</c>).</summary>
        void Tapped();
        void LevelCompleted(int level, int nextLevel, int reward);
        /// <summary>D-064: the level was skipped from the lose dialog (it counts as done) — sent instead of <see cref="LevelCompleted"/>.</summary>
        void LevelSkipped(int level, int nextLevel);
        /// <summary>The attempt ended on the lose dialog (Retry or Home).</summary>
        void LevelFailed();
        /// <summary>The attempt was left: restart, Home from pause, the screen went away. Nothing when none is open.</summary>
        void LevelAbandoned();
        void ScreenViewed(string screen);
        /// <summary>The first look at the save this process: an attempt the app was killed in becomes <c>level_abandoned</c>,
        /// and the first launch of the install reports its loading flow once.</summary>
        void SessionStarted();
        void AdStarted(string iaaType, string placement);
        void AdCompleted(string iaaType, string placement, string reward);
        void AdFailed(string iaaType, string placement);
        void OutOfLivesShown(string trigger, long nextLifeSeconds, long refillPrice, bool canAfford, bool rvAvailable);
        void UiTapped(string name, string location);
        void MetaStarted(string id, string name);
        void MetaCompleted(string id, string name);
        /// <summary>Once per trip to the background (a quit after a pause is the same trip).</summary>
        void AppBackgrounded(int lives, long nextLifeSeconds);
        void AppResumed();
        /// <summary>D-071 store_entered: the Store or an offer popup showed these packs (store product ids).</summary>
        void StoreEntered(string storeId, string storeName, string placement, IReadOnlyList<string> storeItems);
        /// <summary>D-071 IAP_start / IAP_completed / IAP_failed: one purchase from <paramref name="placement"/> (where the Store or popup
        /// was opened from).</summary>
        void IapStarted(ShopPack pack, string productName, string placement);
        void IapCompleted(ShopPack pack, string productName, string placement);
        void IapFailed(ShopPack pack, string productName, string placement, string reason);
        void IapRestored(ShopPack pack, string productName);
        /// <summary>D-071 rv_popup_shown / rv_popup_expired: a rewarded-ad offer popup (the Gold Brick Package!).</summary>
        void RvPopupShown(string placement, int failCount, bool rvAvailable);
        void RvPopupExpired(string placement, bool wasShown);
    }

    /// <summary>
    /// D-061: the background / quit side of the plan, fed by the kernel's single lifecycle hook (AD-16): <c>app_backgrounded</c> once
    /// per trip, and a quit leaves the open attempt as <c>level_abandoned</c>. Runs before the save and telemetry phases, so both carry it.
    /// </summary>
    public sealed class TrackingLifecycle : IAppPhaseHandler
    {
        private readonly IGameTracking _tracking;
        private readonly LevelProgressService _progress;
        private readonly IClock _clock;

        public TrackingLifecycle(IGameTracking tracking, LevelProgressService progress, IClock clock)
        {
            _tracking = tracking; _progress = progress; _clock = clock;
        }

        public AppLifecyclePhase Phase => AppLifecyclePhase.PresentationFlush;

        public void OnPause(bool paused)
        {
            if (paused) Backgrounded();
            else _tracking.AppResumed();
        }

        public void OnFocus(bool focused) { }   // a system dialog takes focus without the app leaving: not a background

        public void OnQuit()
        {
            Backgrounded();
            _tracking.LevelAbandoned();
        }

        private void Backgrounded()
        {
            var now = _clock.UtcNow;
            _tracking.AppBackgrounded(_progress.Lives(now), (long)_progress.NextLifeIn(now).TotalSeconds);
        }
    }

    /// <summary>
    /// The one place the tracking plan is spelled (D-061, from Money Sort's <c>TrackingEvents</c>): builds each event with its
    /// parameters and sends it through the framework's consent-gated fan-out. Session-long. The open attempt is mirrored in the
    /// save so an attempt the app was killed in is still reported, as <c>level_abandoned</c>, at the next launch; that
    /// bookkeeping never fails a caller (a save error only loses the mirror).
    /// </summary>
    public sealed class GameTracking : IGameTracking, IProgressFacts
    {
        private readonly IAnalytics _analytics;
        private readonly IWalletService _wallet;
        private readonly IProgressStore _store;
        private readonly bool _isTest;
        private readonly Func<DateTime> _utcNow;
        private readonly Func<string> _newUserId;

        private bool _sessionStarted, _backgrounded;
        private int _level, _launch, _counter, _levelsPlayedSession, _contentLevel;
        private bool _attemptOpen;
        private string _screen = TrackingNames.Home;

        /// <param name="isTest">Marks IAA_completed as a test ad (Editor and development builds).</param>
        /// <param name="utcNow">D-097: today for <c>retained_days</c> (the composition passes the framework clock; default: system UTC).</param>
        /// <param name="newUserId">D-097: makes the install's user id once (default: a new GUID).</param>
        public GameTracking(IAnalytics analytics, IWalletService wallet, IProgressStore store, bool isTest,
            Func<DateTime> utcNow = null, Func<string> newUserId = null)
        {
            _analytics = analytics; _wallet = wallet; _store = store; _isTest = isTest;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _newUserId = newUserId ?? (() => Guid.NewGuid().ToString());
        }

        private long Balance => _wallet.Balance(SqadSortResources.Coin);

        // ── levels ──────────────────────────────────────────────────────────────────────────────────

        public void LevelStarted(int level, int launch, int contentLevel = 0)
        {
            SessionStarted();
            LevelAbandoned();
            _level = level;
            _contentLevel = Math.Max(0, contentLevel);
            _launch = Math.Max(1, launch);
            _counter = 0;
            _attemptOpen = true;
            _levelsPlayedSession++;
            Mirror();
            Send(TrackingNames.LevelStarted, LevelParams(TrackingNames.LevelStarted));
            ScreenViewed(TrackingNames.ActionPhase);
        }

        public void Tapped()
        {
            if (_attemptOpen) _counter++;
        }

        public void LevelCompleted(int level, int nextLevel, int reward)
        {
            if (!_attemptOpen || level != _level) { _level = level; _launch = Math.Max(1, _launch); }
            var p = LevelParams(TrackingNames.LevelCompleted);
            p.Add(Param("reward", "{\"" + TrackingNames.Coins + "\":" + reward.ToString(CultureInfo.InvariantCulture) + "}"));
            Send(TrackingNames.LevelCompleted, p);
            Send(TrackingNames.LevelUp, new List<KeyValuePair<string, object>> { Param("level", nextLevel), Param("character", "main_level") });
            Close();
        }

        // Money Sort's level_skipped: the level fields, no tap count, and actual_level = where the player goes on
        public void LevelSkipped(int level, int nextLevel)
        {
            if (!_attemptOpen || level != _level) { _level = level; _launch = Math.Max(1, _launch); }
            var p = LevelParams(TrackingNames.LevelStarted);   // the started shape: no level_counter
            p.Add(Param("actual_level", nextLevel));
            Send(TrackingNames.LevelSkipped, p);
            Send(TrackingNames.LevelUp, new List<KeyValuePair<string, object>> { Param("level", nextLevel), Param("character", "main_level") });
            Close();
        }

        public void LevelFailed() => End(TrackingNames.LevelFailed);
        public void LevelAbandoned() => End(TrackingNames.LevelAbandoned);

        private void End(string name)
        {
            if (!_attemptOpen) return;
            Send(name, LevelParams(name));
            Close();
        }

        private void Close()
        {
            _attemptOpen = false;
            Mirror();
        }

        private List<KeyValuePair<string, object>> LevelParams(string name) => LevelParams(name, _level, _launch, _counter);

        private List<KeyValuePair<string, object>> LevelParams(string name, int level, int launch, int counter)
        {
            var p = new List<KeyValuePair<string, object>>
            {
                Param("level_id", level), Param("level_name", "null"), Param("level_type", "main"),
                Param("level_launch", Math.Max(1, launch)), Param("balance", Balance),
            };
            if (level == _level && _contentLevel > 0) p.Add(Param("content_level_id", _contentLevel));
            if (name != TrackingNames.LevelStarted) p.Add(Param("level_counter", counter));
            return p;
        }

        // ── session ─────────────────────────────────────────────────────────────────────────────────

        public void SessionStarted()
        {
            if (_sessionStarted) return;
            _sessionStarted = true;
            UserProperties();
            var s = _store.Progress;
            if (s.TrackedAttemptLevel > 0)   // the app died with an attempt open
            {
                Send(TrackingNames.LevelAbandoned, LevelParams(TrackingNames.LevelAbandoned, s.TrackedAttemptLevel, s.TrackedAttemptLaunch, s.TrackedAttemptCounter));
                Mirror();
            }
            if (!s.FirstLoadingTracked)
            {
                Send(TrackingNames.MetaStarted, MetaParams("first_loading", "start"));
                foreach (var step in new[] { "init_adjust", "init_firebase", "finish" })
                    Send(TrackingNames.MetaCompleted, MetaParams("first_loading", step));
                s.FirstLoadingTracked = true;
                TrySave();
            }
        }

        // D-097 (Money Sort InitializeUserProperties): the install's id and day are made once and saved; retained_days = whole UTC
        // days since that day (never negative: a clock set back reads 0)
        private void UserProperties()
        {
            var s = _store.Progress;
            var today = _utcNow().Date;
            bool changed = false;
            if (string.IsNullOrEmpty(s.AnalyticsUserId)) { s.AnalyticsUserId = _newUserId(); changed = true; }
            if (!TryDay(s.AnalyticsInstallDayUtc, out var installDay))
            {
                installDay = today;
                s.AnalyticsInstallDayUtc = today.Year * 10000 + today.Month * 100 + today.Day;
                changed = true;
            }
            if (changed) TrySave();
            _analytics.Track(new UserPropertiesRecorded(s.AnalyticsUserId, new List<KeyValuePair<string, object>>
            {
                Param(TrackingNames.UserId, s.AnalyticsUserId),
                Param(TrackingNames.RetainedDays, Math.Max(0, (today - installDay).Days)),
                Param(TrackingNames.MediaSource, TrackingNames.UnknownSource),
            }));
        }

        private static bool TryDay(int yyyymmdd, out DateTime day)
        {
            day = default;
            int y = yyyymmdd / 10000, m = yyyymmdd / 100 % 100, d = yyyymmdd % 100;
            if (y < 2000 || m < 1 || m > 12 || d < 1 || d > DateTime.DaysInMonth(y, m)) return false;
            day = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
            return true;
        }

        public void ScreenViewed(string screen)
        {
            _screen = screen;
            Send(TrackingNames.ScreenView, new List<KeyValuePair<string, object>> { Param("screen_class", "SqadSort"), Param("screen_name", screen) });
        }

        public void AppBackgrounded(int lives, long nextLifeSeconds)
        {
            if (_backgrounded) return;
            _backgrounded = true;
            Mirror();   // the tap count so far, in case this background is the last thing the process does
            Send(TrackingNames.AppBackgrounded, new List<KeyValuePair<string, object>>
            {
                Param("screen_name", _screen), Param("level_id", _level), Param("current_lives", lives),
                Param("next_life_seconds", nextLifeSeconds), Param("levels_played_session", _levelsPlayedSession), Param("balance", Balance),
            });
        }

        public void AppResumed() => _backgrounded = false;

        // ── ads ─────────────────────────────────────────────────────────────────────────────────────

        public void AdStarted(string iaaType, string placement) => Send(TrackingNames.IaaStart, IaaParams(iaaType, placement));

        public void AdCompleted(string iaaType, string placement, string reward)
        {
            var p = IaaParams(iaaType, placement);
            if (!string.IsNullOrEmpty(reward)) p.Add(Param("reward", reward));
            if (_isTest) p.Add(Param("isTest", true));
            Send(TrackingNames.IaaCompleted, p);
        }

        // ── shop and offers (D-071, Money Sort ShopService.Track / TrackStoreEntered, GarageGift) ───────────────────────────────

        public void StoreEntered(string storeId, string storeName, string placement, IReadOnlyList<string> storeItems)
        {
            var items = new StringBuilder("[");
            for (int i = 0; i < storeItems.Count; i++) items.Append(i == 0 ? "" : ",").Append('"').Append(storeItems[i]).Append('"');
            Send(TrackingNames.StoreEntered, new List<KeyValuePair<string, object>>
            {
                Param("store_id", storeId), Param("store_name", storeName), Param("store_type", "iap"), Param("store_items", items.Append(']').ToString()),
                Param("placement", placement), Param("additional_data", "null"),
            });
        }

        public void IapStarted(ShopPack pack, string productName, string placement)
        {
            var p = IapParams(pack, productName);
            p.Add(Param("placement", placement ?? string.Empty));
            p.Add(Param("real_currency_name", "USD"));
            p.Add(Param("value", (double)pack.PriceUsd));   // the catalog's USD list price, as Money Sort
            Send(TrackingNames.IapStart, p);
        }

        public void IapCompleted(ShopPack pack, string productName, string placement)
        {
            var p = IapResult(pack, productName, placement, "success");
            p.Add(Param("reward", RewardItems(pack)));
            p.Add(Param("isTestPurchase", _isTest));
            Send(TrackingNames.IapCompleted, p);
        }

        public void IapFailed(ShopPack pack, string productName, string placement, string reason) =>
            Send(TrackingNames.IapFailed, IapResult(pack, productName, placement, reason));

        public void IapRestored(ShopPack pack, string productName) =>
            Send(TrackingNames.IapRestored, IapResult(pack, productName, TrackingNames.RestorePlacement, "success"));

        public void RvPopupShown(string placement, int failCount, bool rvAvailable) => Send(TrackingNames.RvPopupShown, new List<KeyValuePair<string, object>>
        {
            Param("placement", placement), Param("level_id", _level), Param("fail_count", failCount), Param("rv_available", rvAvailable), Param("balance", Balance),
        });

        public void RvPopupExpired(string placement, bool wasShown) => Send(TrackingNames.RvPopupExpired, new List<KeyValuePair<string, object>>
        {
            Param("placement", placement), Param("level_id", _level), Param("was_shown", wasShown), Param("balance", Balance),
        });

        private List<KeyValuePair<string, object>> IapParams(ShopPack pack, string productName) => new List<KeyValuePair<string, object>>
        {
            Param("product_id", pack.StoreId), Param("product_name", productName ?? pack.Id), Param("balance", Balance), Param("additional_data", "null"),
        };

        // item_category is the store the purchase was made in: the Store for Home's Shop, else the placement itself (Money Sort GetStoreId)
        private List<KeyValuePair<string, object>> IapResult(ShopPack pack, string productName, string placement, string receiptStatus)
        {
            var p = IapParams(pack, productName);
            p.Add(Param("item_category", placement == TrackingNames.HomeShop ? TrackingNames.ShopStore : placement ?? string.Empty));
            p.Add(Param("receiptStatus", receiptStatus));
            return p;
        }

        // [{"itemID":"coins","Name":"Coins","Type":"soft"}, …] for everything in the pack (Money Sort DescribeRewardItems)
        private static string RewardItems(ShopPack pack)
        {
            var items = new List<string>();
            void Add(bool included, string id, string name, string type)
            {
                if (included) items.Add("{\"itemID\":\"" + id + "\",\"Name\":\"" + name + "\",\"Type\":\"" + type + "\"}");
            }
            Add(pack.Coins > 0, TrackingNames.Coins, "Coins", "soft");
            Add(pack.BoosterEach > 0, TrackingNames.Booster(Booster.Undo), "Undo", "soft");
            Add(pack.BoosterEach > 0, TrackingNames.Booster(Booster.Shuffle), "Shuffle", "soft");
            Add(pack.BoosterEach > 0, TrackingNames.Booster(Booster.Remove), "Remove", "soft");
            Add(pack.UnlimitedLivesHours > 0, TrackingNames.InfiniteLives, "Infinite Lives", "soft");
            Add(pack.RemoveAds, "remove_ads", "Remove Ads", "feature");
            return "[" + string.Join(",", items) + "]";
        }

        public void AdFailed(string iaaType, string placement)
        {
            var p = IaaParams(iaaType, placement);
            p.Add(Param("receiptStatus", "not_completed"));
            Send(TrackingNames.IaaFailed, p);
        }

        // value: the ad's revenue — 0 until a mediation SDK reports one
        private List<KeyValuePair<string, object>> IaaParams(string iaaType, string placement) => new List<KeyValuePair<string, object>>
        {
            Param("iaa_type", iaaType), Param("level_id", _level), Param("placement", placement),
            Param("real_currency_name", "USD"), Param("value", 0d), Param("balance", Balance),
        };

        // ── popups and meta flows ───────────────────────────────────────────────────────────────────

        public void OutOfLivesShown(string trigger, long nextLifeSeconds, long refillPrice, bool canAfford, bool rvAvailable)
        {
            _screen = TrackingNames.OutOfLivesPopup;
            Send(TrackingNames.OutOfLivesShown, new List<KeyValuePair<string, object>>
            {
                Param("level_id", _level), Param("trigger", trigger), Param("next_life_seconds", nextLifeSeconds),
                Param("refill_price", refillPrice), Param("can_afford", canAfford), Param("rv_available", rvAvailable),
            });
        }

        public void UiTapped(string name, string location) => Send(TrackingNames.UiInteracted, new List<KeyValuePair<string, object>>
        {
            Param("ui_action", "tap"), Param("ui_name", name), Param("ui_location", location), Param("ui_type", "button"),
        });

        public void MetaStarted(string id, string name) => Send(TrackingNames.MetaStarted, MetaParams(id, name));
        public void MetaCompleted(string id, string name) => Send(TrackingNames.MetaCompleted, MetaParams(id, name));

        private static List<KeyValuePair<string, object>> MetaParams(string id, string name) => new List<KeyValuePair<string, object>>
        {
            Param("meta_id", id), Param("meta_name", name), Param("meta_type", "flow"),
        };

        // ── IProgressFacts: what progression reports once saved ────────────────────────────────────

        public void FirstClear(int level) => _analytics.Track(new LevelWon(level));   // D-057: Adjust's level_up_N

        public void Earned(string currency, long value, string placement) => Currency(TrackingNames.EarnCurrency, currency, value, placement);
        public void Spent(string currency, long value, string placement) => Currency(TrackingNames.SpendCurrency, currency, value, placement);

        public void FeatureUnlocked(string feature) => Send(TrackingNames.FeatureUnlocked, new List<KeyValuePair<string, object>>
        {
            Param("level_id", _level), Param("feature_name", feature), Param("feature_type", "main"), Param("additional_data", ""),
        });

        private void Currency(string name, string currency, long value, string placement)
        {
            if (value <= 0) return;
            Send(name, new List<KeyValuePair<string, object>>
            {
                Param("virtual_currency_type", "soft"), Param("virtual_currency_name", currency), Param("value", value),
                Param("placement", placement), Param("level_id", _level), Param("balance", BalanceOf(currency)),
            });
        }

        // the balance of the currency that moved, after the move
        private long BalanceOf(string currency)
        {
            var p = _store.Progress;
            if (currency == TrackingNames.Coins) return Balance;
            if (currency == TrackingNames.Lives) return p.Lives;
            if (currency == TrackingNames.Booster(Booster.Undo)) return p.BoosterUndo;
            if (currency == TrackingNames.Booster(Booster.Shuffle)) return p.BoosterShuffle;
            if (currency == TrackingNames.Booster(Booster.Remove)) return p.BoosterAddContainer;
            return 0;
        }

        // ── helpers ─────────────────────────────────────────────────────────────────────────────────

        private void Send(string name, List<KeyValuePair<string, object>> p) => _analytics.Track(new TrackedEvent(name, p));

        private static KeyValuePair<string, object> Param(string key, object value) => new KeyValuePair<string, object>(key, value);

        // the open attempt as the save knows it (0 = none)
        private void Mirror()
        {
            var s = _store.Progress;
            int level = _attemptOpen ? _level : 0, launch = _attemptOpen ? _launch : 0, counter = _attemptOpen ? _counter : 0;
            if (s.TrackedAttemptLevel == level && s.TrackedAttemptLaunch == launch && s.TrackedAttemptCounter == counter) return;
            s.TrackedAttemptLevel = level; s.TrackedAttemptLaunch = launch; s.TrackedAttemptCounter = counter;
            TrySave();
        }

        private void TrySave()
        {
            try { _store.Save(); }
            catch (Exception) { /* analytics bookkeeping only: the next save carries it */ }
        }
    }
}
