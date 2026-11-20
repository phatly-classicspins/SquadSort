using System;
using System.Threading;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Presentation;
using Cysharp.Threading.Tasks;
using Game.Application;
using Game.Domain;
using Game.Gen;
using Game.Views;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The Main (Home) screen controller: shows the current level, coins and lives (CR-012 E), and Play loads Gameplay
    /// (features/level-progression.md) — through Out of Lives when there is none (features/lives.md). Plain ctor-injected
    /// class; the <see cref="HomeView"/> renders.
    /// </summary>
    public sealed class MainScreen : ScreenBase
    {
        private readonly MainParam _param;
        private readonly IAssetService _assets;
        private readonly IRenderLayerRegistry _layers;
        private readonly ISceneService _scenes;
        private readonly ILocalizationService _loc;
        private readonly LevelProgressService _progress;
        private readonly ILevelCatalog _levels;
        private readonly LifeGate _lifeGate;
        private readonly IClock _clock;
        private readonly GameFeedback _feedback;   // CR-012 G
        private readonly IGameTracking _tracking;  // D-061
        private readonly PopupUi _popups;          // Back to the direct Money Sort popups (07-11-2026)
        private readonly SettingsPopup _settings;
        private readonly StarterPackPopup _starter; // D-067
        private readonly RemoveAdsPopup _removeAds;
        private readonly OfferSchedule _offers;     // D-069: the session-start Starter Pack, the Home offer icons
        private readonly ShopPurchases _shop;       // D-069: Remove Ads owned, the Starter countdown
        private readonly GoldBrickFlow _goldBrick;  // D-070
        private readonly ILog _log;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private GameObject _viewPrefab, _viewInstance;
        private HomeView _view;
        private bool _busy;
        private bool _shopTabBuying;
        private UniTaskCompletionSource _shopTabClose;
        private int _levelCount;

        public MainScreen(MainParam param, IAssetService assets, IRenderLayerRegistry layers, ISceneService scenes,
            ILocalizationService loc, LevelProgressService progress, ILevelCatalog levels, LifeGate lifeGate, IClock clock, GameFeedback feedback,
            IGameTracking tracking, PopupUi popups, SettingsPopup settings, StarterPackPopup starter, RemoveAdsPopup removeAds, OfferSchedule offers, ShopPurchases shop, GoldBrickFlow goldBrick, ILog log)
        {
            _param = param; _assets = assets; _layers = layers; _scenes = scenes; _loc = loc; _progress = progress;
            _levels = levels; _lifeGate = lifeGate; _clock = clock; _feedback = feedback; _tracking = tracking; _popups = popups; _settings = settings; _starter = starter; _removeAds = removeAds; _offers = offers; _shop = shop; _goldBrick = goldBrick; _log = log;
        }

        public override async UniTask OnLoadAsync(CancellationToken ct)
        {
            _levelCount = _levels.Count;
            _viewPrefab = await _assets.LoadAsync(AssetKeys.Home.HomeView, ct);
            _viewInstance = UnityEngine.Object.Instantiate(_viewPrefab, _layers.GetHost(RenderLayers.Ui), false);
            _layers.Stamp(_viewInstance, RenderLayers.Ui);
            _view = _viewInstance.GetComponent<HomeView>();
            _view.PlayPressed += Play;
            _view.SettingsPressed += () => HomeSettings().Forget();
            _view.TabPressed += TabPressed;
            _view.StarterPressed += () => Offer(_starter.ShowAsync).Forget();   // D-069
            _view.RemoveAdsPressed += () => Offer(_removeAds.ShowAsync).Forget();
            _view.GiftPressed += () => OfferGift().Forget();   // D-070
            if (_levelCount > 0)
            {
                var level = LevelParser.Parse(_levels.GetLevelText(_progress.HomeLevel(_levelCount)));
                _view.SetHardPlay(level.Difficulty != LevelDifficulty.Normal);
            }
            _tracking.SessionStarted();                                     // D-061: a killed attempt, the first loading flow
            _progress.EnsureStartCoins();                                   // CR-012 C1: once per install
            ShowHome();
            _view.SetFooter(HomeTab.Home, _loc.Get(LocKeys.HomeTabShop), _loc.Get(LocKeys.HomeTabHome));
        }

        private async UniTaskVoid HomeSettings()
        {
            if(_busy && (_shopTabClose == null || _shopTabBuying))return;_busy=true;if(_shopTabClose!=null)_shopTabBuying=true;
            try{await _settings.ShowAsync(true,_cts.Token);}
            finally{_shopTabBuying=false;_busy=_shopTabClose != null;}
        }

        private void ShowHome()
        {
            if (_view == null) return;
            int level = _progress.HomeLevel(_levelCount);
            bool allCleared = !_progress.LevelsLoop(_levelCount) && _progress.HighestCleared >= _levelCount;
            _view.Show(_loc.Get(LocKeys.HomeTitle), _loc.Get(LocKeys.HomeTagline), _loc.Get(LocKeys.HomePlay, level),
                allCleared ? _loc.Get(LocKeys.HomeMoreSoon) : null, _loc.Get(LocKeys.HudCoins, _progress.Coins));
            ShowLives();
            ShowOffers();
        }

        public override void OnEnter()
        {
            _feedback.Music(AudioKeys.MusicHome);
            _tracking.ScreenViewed(TrackingNames.Home);
            _log.Info("[MainScreen] entered.");
            TickLives(_cts.Token).Forget();
            // D-069 / D-070 (GDD Block Sort 7.2 "new session"): at app start one offer — the Starter Pack while its 72-hour window lasts
            // (the window opens on a level complete screen, GameplayScreen), else the Gold Brick Package! when its cooldown is over
            ReportExpiredGift();
            if (_param.OutOfLives != null) { OutOfLivesAsync(_param.OutOfLives).Forget(); return; }   // the last life just went (Money Sort)
            if (!_param.ColdBoot) return;
            var offer = _offers.AtSessionStart(_progress, _progress.HighestCleared, _clock.UtcNow);
            if (offer == LevelCompleteOffer.Starter) Offer(_starter.ShowAsync).Forget();
            else if (offer == LevelCompleteOffer.GoldBrick) OfferGift().Forget();
        }

        public override void OnExit() => _log.Info("[MainScreen] exited.");

        public override UniTask OnUnloadAsync(CancellationToken ct)
        {
            _cts.Cancel();
            if (_viewInstance != null) UnityEngine.Object.Destroy(_viewInstance);
            if (_viewPrefab != null) _assets.Release(_viewPrefab);
            _viewInstance = null; _viewPrefab = null; _view = null;
            return UniTask.CompletedTask;
        }

        // D-069 (GDD Block Sort 6.3 / 6.4): the Home offer icons — Remove Ads after its first popup until bought, the Starter Pack with
        // its countdown while the window lasts
        private void ShowOffers()
        {
            if (_view == null) return;
            bool starter = _progress.StarterOfferLeft(_clock.UtcNow) > TimeSpan.Zero;
            _view.SetOffers(_offers.RemoveAdsIconVisible(_shop.RemoveAdsOwned), _loc.Get(LocKeys.PackStarterPack), starter ? _shop.StarterCountdown() : null);
            ReportExpiredGift();
            var gift = _offers.GoldBrickLeft(_clock.UtcNow);   // D-070: the badge while a gift waits to be claimed
            _view.SetGift(gift > TimeSpan.Zero ? _loc.Get(LocKeys.GoldbrickTimer, (int)gift.TotalMinutes, gift.Seconds) : null);
        }

        // the lives pill: the count, then "Full" or the time to the next life (mock-up home-lives-v04); while unlimited lives run,
        // ∞ and the time they have left (GDD §1.3: "2d", "12h 23m", "15:12")
        private void ShowLives()
        {
            if (_view == null) return;
            var now = _clock.UtcNow;
            var unlimited = _progress.UnlimitedLivesLeft(now);
            if (unlimited > TimeSpan.Zero) { _view.SetLives(_loc.Get(LocKeys.LivesUnlimited), UnlimitedTimer(_loc, unlimited)); return; }
            int lives = _progress.Lives(now);
            var left = _progress.NextLifeIn(now);
            _view.SetLives(_loc.Get(LocKeys.LivesCount, lives),
                left <= TimeSpan.Zero ? _loc.Get(LocKeys.LivesFull) : _loc.Get(LocKeys.LivesTimer, (int)left.TotalMinutes, left.Seconds));
        }

        /// <summary>GDD §1.3 unlimited lives timer: whole days from a day up, hours and minutes from an hour up, else mm:ss.</summary>
        public static string UnlimitedTimer(ILocalizationService loc, TimeSpan left) =>
            left.TotalDays >= 1 ? loc.Get(LocKeys.LivesUnlimitedDays, (int)left.TotalDays)
            : left.TotalHours >= 1 ? loc.Get(LocKeys.LivesUnlimitedHours, (int)left.TotalHours, left.Minutes)
            : loc.Get(LocKeys.LivesTimer, (int)left.TotalMinutes, left.Seconds);

        // Money Sort: Out of Lives on Home; a refill (or a life coming back) leaves the player on Home — Play starts the level
        public override void OnBackRequested()
        {
            if (_shop.CloseUI()) return;
            if (_popups.CloseTop()) return;
            if (_shopTabClose != null)
            {
                if (!_shopTabBuying) _shopTabClose.TrySetResult();
                return;
            }
            _lifeGate.CloseUI();
        }

        private async UniTaskVoid OutOfLivesAsync(string trigger)
        {
            _busy = true;
            try
            {
                await _lifeGate.EnsureAsync(trigger, _cts.Token);
                ShowLives();
                ShowOffers();
            }
            catch (OperationCanceledException) { }
            finally { _busy = false; }
        }

        private async UniTaskVoid TickLives(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true, cancellationToken: ct).SuppressCancellationThrow()) return;
                ShowLives();
                ShowOffers();
            }
        }

        // the Home footer (Money Sort): Home is this screen; the Shop tab lights while the Store (D-067) is open over it
        private void TabPressed(HomeTab tab)
        {
            if (tab == HomeTab.Shop) ShopTabAsync().Forget();
            else if (_shopTabClose != null && !_shopTabBuying) _shopTabClose.TrySetResult();   // the shared footer's Home tab
        }

        // The footer switches pages using the authored ShopUI, without joining the dialog stack.
        private async UniTask ShopTabAsync()
        {
            if (_busy) return;
            _busy = true;
            GameObject prefab = null, instance = null;
            ShopUI ui = null;
            using var ticking = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            try
            {
                // One footer for both tabs: the Shop page goes inside Home, under Home's footer, so the footer never moves — only its
                // lit tab changes. Home stays as it is while the page loads (no blank frame), then the page fades in over it.
                prefab = await _assets.LoadAsync(AssetKeys.UI.ShopUI, _cts.Token);
                instance = UnityEngine.Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Ui), false);
                _layers.Stamp(instance, RenderLayers.Ui);
                _view.MountPage(instance.transform);
                ui = instance.GetComponent<ShopUI>();
                ui.SetLayout(0f, 1f);
                ui.Init(null);
                ui.UseHostFooter();
                _shopTabClose = new UniTaskCompletionSource();
                DrawShop();
                ui.Show();
                _view.SetFooterTab(HomeTab.Shop);
                await FadeShop(true);
                _view.SetFooter(HomeTab.Shop, _loc.Get(LocKeys.HomeTabShop), _loc.Get(LocKeys.HomeTabHome));   // Home's content goes under the page
                _shop.StoreEntered(TrackingNames.ShopStore, "Shop", TrackingNames.HomeShop,
                    _progress.ShopPacks(_clock.UtcNow, _shop.RemoveAdsOwned));
                TickShop().Forget();
                await _shopTabClose.Task.AttachExternalCancellation(_cts.Token);
                ticking.Cancel();
                // back to Home: Home's content is put back underneath first, then the page fades out to show it
                _view.SetFooter(HomeTab.Home, _loc.Get(LocKeys.HomeTabShop), _loc.Get(LocKeys.HomeTabHome));
                ShowHome();
                await FadeShop(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { _log.Warn("[Home Shop tab] " + e.Message); }
            finally
            {
                ticking.Cancel();
                _shopTabClose = null;
                _shopTabBuying = false;
                if (ui != null) ui.Animation?.Stop();
                if (instance != null) UnityEngine.Object.Destroy(instance);
                ui = null;
                if (prefab != null) _assets.Release(prefab);
                _busy = false;
                if (_view != null && !_cts.IsCancellationRequested)
                {
                    _view.SetFooter(HomeTab.Home, _loc.Get(LocKeys.HomeTabShop), _loc.Get(LocKeys.HomeTabHome));
                    ShowHome();
                }
            }

            async UniTask FadeShop(bool shown)
            {
                var faded = new UniTaskCompletionSource();
                ui.FadePage(shown, () => faded.TrySetResult());
                await faded.Task.AttachExternalCancellation(_cts.Token);
            }
            void CloseShop() { if (!_shopTabBuying) _shopTabClose?.TrySetResult(); }
            void DrawShop()
            {
                if (ui == null) return;
                ui.SetSettings(() => HomeSettings().Forget());
                ui.SetMoney((int)_progress.Coins);
                var (lives, next) = _shop.LivesPill();
                ui.SetLabel("_livesText", lives);
                ui.SetLabel("_lifeTimerText", next);
                ui.SetFooterLabels(_loc.Get(LocKeys.HomeTabShop), _loc.Get(LocKeys.HomeTabHome));
                ui.BindCardSlot(_shop.Sections(), id => RunPurchase(async ct =>
                {
                    var pack = ShopCatalog.Find(id);
                    if (pack != null) await _shop.BuyAsync(pack, TrackingNames.HomeShop, ct);
                }), CloseShop, () => RunPurchase(_shop.RestoreAsync), overlay: false);
                _view?.SetCoins(_loc.Get(LocKeys.HudCoins, _progress.Coins));
            }
            void RunPurchase(Func<CancellationToken, UniTask> work)
            {
                if (_shopTabBuying || ticking.IsCancellationRequested) return;
                _shopTabBuying = true;
                Purchase().Forget();
                async UniTask Purchase()
                {
                    try { await work(ticking.Token); }
                    catch (OperationCanceledException) { }
                    catch (Exception e) { _log.Warn("[Home Shop purchase] " + e.Message); }
                    finally { _shopTabBuying = false; if (ui != null) DrawShop(); }
                }
            }
            async UniTask TickShop()
            {
                bool starter = _progress.StarterOfferLeft(_clock.UtcNow) > TimeSpan.Zero;
                while (!ticking.IsCancellationRequested)
                {
                    if (await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true,
                        cancellationToken: ticking.Token).SuppressCancellationThrow()) return;
                    if (ui == null || _shopTabBuying) continue;
                    ui.SetMoney((int)_progress.Coins);
                    var (count, next) = _shop.LivesPill();
                    ui.SetLabel("_livesText", count);
                    ui.SetLabel("_lifeTimerText", next);
                    bool now = _progress.StarterOfferLeft(_clock.UtcNow) > TimeSpan.Zero;
                    if (now != starter) { starter = now; DrawShop(); }
                    else if (now) ui.SetLabel("_timerText", _shop.StarterCountdown());
                }
            }
        }
        // D-071 (Money Sort CheckGarageGiftExpired): a gift window that ran out unclaimed is reported once
        private void ReportExpiredGift()
        {
            if (_offers.TakeExpiredGoldBrick(_clock.UtcNow, out bool wasShown)) _tracking.RvPopupExpired(TrackingNames.GoldBrickRv, wasShown);
        }

        // D-070: the Gold Brick Package! over Home (app start, or its badge); afterwards Home shows the new balance
        private async UniTaskVoid OfferGift()
        {
            if (_busy) return;
            _busy = true;
            try { await _goldBrick.ShowAsync(_cts.Token); }
            catch (OperationCanceledException) { }
            finally { _busy = false; ShowHome(); }
        }

        // a Store dialog over Home; afterwards Home shows the new balance and lives
        private async UniTaskVoid Offer(Func<CancellationToken, UniTask<bool>> show, bool lightShopTab = false)
        {
            if (_busy) return;
            _busy = true;
            try
            {
                if (lightShopTab) _view?.SetFooter(HomeTab.Shop, _loc.Get(LocKeys.HomeTabShop), _loc.Get(LocKeys.HomeTabHome));
                await show(_cts.Token);
            }
            catch (OperationCanceledException) { }
            finally
            {
                _busy = false;
                _view?.SetFooter(HomeTab.Home, _loc.Get(LocKeys.HomeTabShop), _loc.Get(LocKeys.HomeTabHome));
                ShowHome();
            }
        }

        private void Play()
        {
            if (_busy) return;
            StartLevel();
        }

        // CR-012 E / Money Sort: no life ⇒ Out of Lives, and the player stays on Home after it; starting a level costs nothing
        private void StartLevel()
        {
            if (_progress.Lives(_clock.UtcNow) <= 0) { OutOfLivesAsync(TrackingNames.TapPlay).Forget(); return; }
            _scenes.LoadAsync(SceneKeys.Gameplay, new GameplayParam(_progress.CurrentLevel), SceneTransition.Replace).Forget();
        }
    }
}
