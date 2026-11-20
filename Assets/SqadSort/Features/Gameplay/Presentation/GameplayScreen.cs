using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Domain;
using ClassicSpins.PrototypeFramework.Presentation;
using ClassicSpins.Rating;
using Cysharp.Threading.Tasks;
using Game.Application;
using Game.Domain;
using Game.Gen;
using Game.Views;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Presentation
{
    /// <summary>The "Are you sure?" answer (CardSlot's RestartConfirmChoice, the part its flows use).</summary>
    public enum RestartConfirmChoice { Back, Confirm }

    /// <summary>The booster purchase popup's answer (CardSlot's BoosterBuyChoice).</summary>
    public enum BoosterBuyChoice { Coins, Ad, Close }

    /// <summary>What the level complete popup ended with (CardSlot's WinChoice / WinPick).</summary>
    public enum WinChoice { Bonus, Next, Home }

    public readonly struct WinPick
    {
        public readonly WinChoice Choice;
        public readonly int Multiplier;
        public WinPick(WinChoice choice, int multiplier = 0) { Choice = choice; Multiplier = multiplier; }
    }

    /// <summary>
    /// The Gameplay screen controller, CardSlot's flows on SqadSort's board: the 3D board under this scene's WorldRoot, CardSlot's
    /// Money Sort HUD (<see cref="GameplayHudView"/>) on the Ui layer, and the same popups in the same order —
    /// <list type="bullet">
    /// <item>Intro: the Hard level banner, the booster unlock popup at its level, the element unlock popup.</item>
    /// <item>Win: the rating prompt, the level complete interstitial, Level Up (coin fly, bonus needle on a rewarded ad), one
    /// level complete offer (Starter Pack / Remove Ads / Gold Brick), then the next level in place.</item>
    /// <item>Lose (the plaza is full): the booster countdown, then Play On (SqadSort's Add Space: one more plaza cell, for coins
    /// or an ad) or the use-a-booster variant, then "Are you sure?" — Restart costs a life; after it the Skip Level or the
    /// Restart Booster offer.</item>
    /// <item>Boosters: a tile with none opens the purchase popup (coins, or an ad); Undo, Shuffle (armed until a tube is
    /// tapped) and Add Container spend one saved booster each.</item>
    /// </list>
    /// Every flow runs through <see cref="Guard"/>: one at a time, an exception sends the player Home. A plain ctor-injected
    /// controller — never a MonoBehaviour.
    /// </summary>
    public sealed class GameplayScreen : ScreenBase, IGameplayCheatTarget
    {
        private readonly GameplayParam _param;
        private readonly ISceneService _scenes;
        private readonly LevelProgressService _progress;
        private readonly GameplayCheatHook _cheats;   // tester menu (inert without it)
        private readonly IAssetService _assets;
        private readonly IRenderLayerRegistry _layers;
        private readonly IWorldViewport _viewport;
        private readonly ILevelCatalog _levels;
        private readonly IRandom _random;
        private readonly ScreenWorldRoot _worldRoot;
        private readonly ILocalizationService _loc;
        private readonly IClock _clock;
        private readonly EconomyTuning _tuning;
        private readonly SettingsPopup _settings;
        private readonly ShopPurchases _shop;
        private readonly PopupUi _popups;
        private readonly IAdsService _ads;
        private readonly AdPacing _adPacing;
        private readonly GameplayBanner _banner;
        private readonly GameFeedback _feedback;
        private readonly SqadSortRating _rating;
        private readonly IGameTracking _tracking;
        private readonly OfferSchedule _offers;
        private readonly StarterPackPopup _starter;
        private readonly RemoveAdsPopup _removeAds;
        private readonly GoldBrickFlow _goldBrick;
        private readonly NoAdsPopup _noAds;
        private readonly ILog _log;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private GameObject _boardPrefab, _boardInstance;
        private GameObject _hudPrefab, _hudInstance;
        private GameplayHudView _hud;
        private BoardPresenter _presenter;
        private int _level;
        private LevelDefinition _data;
        private LevelSession _session;
        private bool _busy;
        private int _shuffleChargesArmed = -1;   // the session's Shuffle count when the booster was armed; -1 = not armed
        private int _paidRevives, _reviveAds;    // this attempt's Add Space revives: the next paid one costs more (GDD §7)
        private UniTaskCompletionSource<int> _countdownPick;
        private double? _hintLeft;               // seconds the paused lose countdown still had while its booster plays

        // CardSlot's first-use booster guide, on the level that unlocks the booster: the spot on its tile, then (Shuffle) on a tube
        private enum BoosterGuideStep { None, Booster, Tube }
        private Booster _guidedBooster;
        private BoosterGuideStep _boosterGuideStep;
        private ElementTutorialUI _boosterTutorial;
        private int _boosterGuideTarget = -1;
        private bool _undoGuidePending;          // Undo has nothing to undo at the start: its guide waits for the first move

        /// <summary>The session's own count never limits a booster: the saved boosters do (CardSlot's bar).</summary>
        private const int SessionCharges = int.MaxValue / 2;
        private const float DialogBeat = 0.3f;   // the beat between the last move landing and a result popup (CardSlot)
        private const double AdRecheckSeconds = 0.5;
        private const string Rv = "RV", Inter = "inter";   // the tracking plan's iaa_type values

        public GameplayScreen(GameplayParam param, ISceneService scenes, LevelProgressService progress, GameplayCheatHook cheats,
            IAssetService assets, IRenderLayerRegistry layers, IWorldViewport viewport, ILevelCatalog levels, IRandom random,
            ScreenWorldRoot worldRoot, ILocalizationService loc, IClock clock, EconomyTuning tuning, SettingsPopup settings,
            ShopPurchases shop, PopupUi popups, IAdsService ads, AdPacing adPacing, GameplayBanner banner, GameFeedback feedback,
            SqadSortRating rating, IGameTracking tracking, OfferSchedule offers, StarterPackPopup starter, RemoveAdsPopup removeAds,
            GoldBrickFlow goldBrick, NoAdsPopup noAds, ILog log)
        {
            _param = param; _scenes = scenes; _progress = progress; _cheats = cheats; _assets = assets; _layers = layers;
            _viewport = viewport; _levels = levels; _random = random; _worldRoot = worldRoot; _loc = loc; _clock = clock;
            _tuning = tuning; _settings = settings; _shop = shop; _popups = popups; _ads = ads; _adPacing = adPacing;
            _banner = banner; _feedback = feedback; _rating = rating; _tracking = tracking; _offers = offers; _starter = starter;
            _removeAds = removeAds; _goldBrick = goldBrick; _noAds = noAds; _log = log;
        }

        public override async UniTask OnLoadAsync(CancellationToken ct)
        {
            _boardPrefab = await _assets.LoadAsync(AssetKeys.Board, ct);
            _boardInstance = Object.Instantiate(_boardPrefab, _worldRoot.Transform, false);
            _layers.Stamp(_boardInstance, RenderLayers.GamePlay);
            _presenter = new BoardPresenter(_boardInstance.GetComponent<BoardView>(), _viewport);
            _presenter.LevelEnded += OnLevelEnded;
            _presenter.Changed += Draw;
            _presenter.ShufflePickEnded += OnShuffleDone;

            // CardSlot's HUD: the Money Sort GameplayUI on the Ui layer
            _hudPrefab = await _assets.LoadAsync(AssetKeys.Gameplay.GameplayHud, ct);
            _hudInstance = Object.Instantiate(_hudPrefab, _layers.GetHost(RenderLayers.Ui), false);
            _layers.Stamp(_hudInstance, RenderLayers.Ui);
            _hud = _hudInstance.GetComponent<GameplayHudView>();
            _hud.RestartPressed += () => Guard(async () => await ConfirmRestart());
            _hud.PausePressed += () => Guard(Pause);
            _hud.ShopPressed += () => Guard(async () => { await _shop.OpenOverlayAsync(TrackingNames.ShopStore, _cts.Token); HideBannerIfAdsRemoved(); Draw(); });
            _hud.BoosterPressed += OnBoosterPressed;
        }

        public override void OnEnter()
        {
            _feedback.Music(AudioKeys.MusicGameplay);
            _tracking.ScreenViewed(TrackingNames.ActionPhase);
            _log.Info($"[GameplayScreen] entered. random seed={_random.Seed}, levels={_levels.Count}.");
            _cheats.Attach(this);
            TickLives(_cts.Token).Forget();
            Guard(() => Restart(_param.Level));
        }

        public override void OnExit()
        {
            _cheats.Detach(this);
            _banner.Hide();
            _cts.Cancel();
            _log.Info("[GameplayScreen] exited.");
        }

        public override void OnBackRequested()
        {
            if (_shop.CloseUI()) return;
            if (_popups.CloseTop()) return;
            if (_popupBack != null) { _popupBack(); return; }
            if (_busy) return;
            Guard(Pause);
        }

        public override UniTask OnUnloadAsync(CancellationToken ct)
        {
            if (_presenter != null)
            {
                _presenter.LevelEnded -= OnLevelEnded;
                _presenter.Changed -= Draw;
                _presenter.ShufflePickEnded -= OnShuffleDone;
                _presenter.Dispose();
            }

            HideBoosterTutorial();   // it lives on the shared Popup host, not under this screen
            if (_hudInstance != null) Object.Destroy(_hudInstance);
            if (_hudPrefab != null) _assets.Release(_hudPrefab);
            if (_boardInstance != null) Object.Destroy(_boardInstance);
            if (_boardPrefab != null) _assets.Release(_boardPrefab);
            _cts.Dispose();
            return UniTask.CompletedTask;
        }

        // ------------------------------------------------------------------ flow guard

        // one flow at a time (CardSlot's Guard): a second tap while a popup is up does nothing; a failure goes Home
        private void Guard(Func<UniTask> flow) => Run(flow).Forget();

        private async UniTaskVoid Run(Func<UniTask> flow)
        {
            if (_busy) return;
            _busy = true;
            try { await flow(); }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                _log.Warn($"[GameplayScreen] flow failed on level {_level}: {e.Message}");
                if (!_cts.IsCancellationRequested) GoHome();
            }
            finally { _busy = false; if (!_cts.IsCancellationRequested) Draw(); }
        }

        // ------------------------------------------------------------------ level start

        private void StartLevel(int level)
        {
            _level = level;
            _data = LevelParser.Parse(_levels.GetLevelText(level));
            var charges = new Dictionary<Booster, int>
            {
                [Booster.Undo] = SessionCharges,
                [Booster.Shuffle] = SessionCharges,
                [Booster.AddContainer] = SessionCharges,
            };
            _session = new LevelSession(level, _data, _random, charges);
            _shuffleChargesArmed = -1;
            _hintLeft = null;
            EndBoosterGuide();
            _paidRevives = 0;
            _reviveAds = 0;
            var start = _progress.BeginLevel(level);   // GDD §1.3: starting costs no life — leaving the attempt does (LeaveAttempt)
            _log.Info($"[GameplayScreen] level {level} \"{_data.Name}\" ({_data.Difficulty}), attempt {start.AttemptNo}.");
            _tracking.LevelStarted(level, start.AttemptNo, _tuning.ContentLevel(level, _levels.Count));
            bool bannerUp = _banner.LevelStarted(level);   // the bottom banner from Banner_level on
            _hud.ReserveBannerSpace(bannerUp);              // the booster bar moves up above it
            _presenter.Show(_session);                      // raises Changed → Draw
        }

        private async UniTask Restart(int level)
        {
            StartLevel(level);
            await Intro();
        }

        // the Hard banner, then the booster unlock popup at its configured level, then the element unlock popup (CardSlot)
        private async UniTask Intro()
        {
            if (_data.Difficulty != LevelDifficulty.Normal) await ShowHardLevelIntro(_cts.Token);
            foreach (var id in _progress.UnlockBoostersFor(_level))
            {
                // catch up unlocks/gifts silently; their introduction belongs only to the configured level
                if (_level != _tuning.UnlockLevel(id)) continue;
                Draw();
                if (!await ShowUnlock(id)) return;
            }

            foreach (var id in _progress.UnlockElementsFor(_level))
            {
                Draw();
                await ShowElementUnlockAsync(id, _cts.Token);
            }

            Draw();
            foreach (var id in new[] { Booster.Shuffle, Booster.AddContainer })
            {
                if (!CanGuideBooster(id)) continue;
                StartBoosterGuide(id);
                return;
            }
            _undoGuidePending = CanGuideBooster(Booster.Undo, needTarget: false);
        }

        private async UniTask ShowHardLevelIntro(CancellationToken ct)
        {
            var prefab = _hud.HardLevelIntro;
            if (prefab == null) return;
            var intro = Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
            try
            {
                _layers.Stamp(intro.gameObject, RenderLayers.Popup);
                const float fadeIn = 0.25f, hold = 0.65f, fadeOut = 0.35f;
                var start = Time.unscaledTime;
                float elapsed;
                do
                {
                    elapsed = Time.unscaledTime - start;
                    intro.alpha = Mathf.Clamp01(Mathf.Min(elapsed / fadeIn, (fadeIn + hold + fadeOut - elapsed) / fadeOut));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                } while (elapsed < fadeIn + hold + fadeOut);
            }
            finally
            {
                if (intro != null) Object.Destroy(intro.gameObject);
            }
        }

        // ------------------------------------------------------------------ HUD (CardSlot's Draw / Tiles / ShowLives)

        private void Draw()
        {
            if (_hud == null || _session == null) return;
            _hud.SetHud(_loc.Get(LocKeys.GameplayLevel, _level), Coins());
            ShowLives();
            _hud.SetBoosters(Tiles());
            if (_undoGuidePending && !_busy && _boosterGuideStep == BoosterGuideStep.None && CanGuideBooster(Booster.Undo))
            {
                _undoGuidePending = false;
                StartBoosterGuide(Booster.Undo);
            }
        }

        private string Coins() => _loc.Get(LocKeys.HudCoins, _progress.Coins);

        private BoosterTileVisual[] Tiles()
        {
            var tiles = new BoosterTileVisual[3];
            for (int i = 0; i < 3; i++)
            {
                var id = (Booster)i;
                int n = _progress.Boosters(id);
                bool open = _progress.IsUnlocked(id);
                tiles[i] = new BoosterTileVisual
                {
                    Name = _loc.Get(BoosterName(id)),
                    Locked = open ? null : _loc.Get(LocKeys.BoosterLocked, _tuning.UnlockLevel(id)),
                    Badge = open && n > 0 ? _loc.Get(LocKeys.BoosterCount, n) : null,
                    Price = open && n == 0 ? _loc.Get(LocKeys.BoosterPrice, _tuning.Price(id)) : null,
                    Active = id == Booster.Shuffle && _presenter.IsShufflePicking,
                };
            }

            return tiles;
        }

        // CardSlot's texts, as they are: Undo sits in the Hand tile, Add Container in the Magnet (Remove) tile
        private static LocKey BoosterName(Booster id) =>
            id == Booster.Undo ? LocKeys.BoosterHandName : id == Booster.Shuffle ? LocKeys.BoosterShuffleName : LocKeys.BoosterRemoveName;

        private static LocKey BoosterTip(Booster id) =>
            id == Booster.Undo ? LocKeys.BoosterHandTip : id == Booster.Shuffle ? LocKeys.BoosterShuffleTip : LocKeys.BoosterRemoveTip;

        private static LocKey ElementName(ElementId id) => id == ElementId.Hidden ? LocKeys.ElementHiddenStackName : LocKeys.ElementHiddenPoleName;

        private static LocKey ElementTip(ElementId id) => id == ElementId.Hidden ? LocKeys.ElementHiddenStackTip : LocKeys.ElementHiddenPoleTip;

        private void ShowLives()
        {
            var now = _clock.UtcNow;
            var unlimited = _progress.UnlimitedLivesLeft(now);
            if (unlimited > TimeSpan.Zero) { _hud.SetLives(_loc.Get(LocKeys.LivesUnlimited), MainScreen.UnlimitedTimer(_loc, unlimited)); return; }
            var left = _progress.NextLifeIn(now);
            _hud.SetLives(_loc.Get(LocKeys.LivesCount, _progress.Lives(now)),
                left <= TimeSpan.Zero ? _loc.Get(LocKeys.LivesFull) : _loc.Get(LocKeys.LivesTimer, (int)left.TotalMinutes, left.Seconds));
        }

        private async UniTaskVoid TickLives(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (await UniTask.Delay(TimeSpan.FromSeconds(1), ignoreTimeScale: true, cancellationToken: ct).SuppressCancellationThrow()) return;
                if (_hud != null) ShowLives();
            }
        }

        // ------------------------------------------------------------------ level end

        private void OnLevelEnded(BoardState state, float settleSeconds)
        {
            if (state == BoardState.Won) Guard(() => Won(settleSeconds));
            else if (state == BoardState.OutOfSpace) Guard(() => Lost(settleSeconds));
        }

        // let the last buddies land, then the beat before a result popup
        private UniTask AnimationsDone(float settleSeconds) =>
            UniTask.Delay(TimeSpan.FromSeconds(Math.Max(0f, settleSeconds) + DialogBeat), DelayType.UnscaledDeltaTime, cancellationToken: _cts.Token);

        // ------------------------------------------------------------------ win (CardSlot)

        /// <param name="skipped">The level was skipped (an ad after a lose-restart) and already counted as won; null = a real win.</param>
        /// <param name="afterAd">The skip was paid with a rewarded ad: no interstitial at this level end (Money Sort).</param>
        private async UniTask Won(float settleSeconds, WinInfo? skipped = null, bool afterAd = false)
        {
            var win = skipped ?? _progress.CompleteLevel(_level, _levels.Count);   // progress + the plain reward persisted first (G19)
            if (skipped != null) _tracking.LevelSkipped(_level, _level + 1);
            else _tracking.LevelCompleted(_level, _level + 1, win.Reward);
            await AnimationsDone(settleSeconds);
            _tracking.ScreenViewed(TrackingNames.EndingPhase);
            _banner.Hide();   // no banner on the level-complete screen (Money Sort)
            _feedback.Won();
            Draw();
            // the rating prompt comes before the win popup and owns this level end's interstitial: closed without rating ⇒ the
            // interstitial now; rated ⇒ none
            var rating = await Rating();
            bool interstitial = rating == RatingResult.Closed && !afterAd && await Interstitial();
            if (rating == RatingResult.NotShown && !afterAd) interstitial = await MaybeInterstitial(_level);
            int destination = win.Saved ? win.NextLevel : _level;
            bool nextHard = LevelParser.Parse(_levels.GetLevelText(Math.Min(Math.Max(1, destination), _levels.Count))).Difficulty != LevelDifficulty.Normal;
            var segments = _tuning.WinBonusSegments();
            bool bonus = win.Reward > 0;   // a skipped level pays nothing, so there is nothing to multiply
            bool adReady = bonus && await AdReady(AdPlacements.RewardedWinDouble);
            var next = win.Final ? _loc.Get(LocKeys.WinPlayAgain) : _loc.Get(LocKeys.GameplayLevel, win.NextLevel);

            async UniTask<bool> PayBonus(int multiplier)
            {
                long extra = (long)win.Reward * Math.Max(0, multiplier - 1);
                if (!await Rewarded(AdPlacements.RewardedWinDouble, TrackingNames.FreeCoins, Reward(TrackingNames.Coins, extra))) return false;
                _progress.GrantWinBonus(win.Reward, multiplier);
                return true;
            }

            var result = await ShowLevelUp(win.Reward, next, segments, adReady, nextHard, PayBonus);
            if (result.Choice == WinChoice.Home) { GoHome(); return; }
            Draw();
            await LevelCompleteOffer(interstitial);
            // a win costs nothing; only a stored 0 (unlimited lives ran out during the level) sends the player to Out of Lives on Home
            if (_progress.Lives(_clock.UtcNow) <= 0) { GoHome(TrackingNames.NextLevel); return; }
            await Restart(destination);
        }

        private async UniTask<bool> MaybeInterstitial(int levelJustWon)
        {
            if (!_adPacing.ShouldShowAfterWin(levelJustWon, _clock.UtcNow)) return false;
            return await Interstitial();
        }

        private UniTask<bool> Interstitial() => Interstitial(AdPlacements.InterstitialLevelEnd, TrackingNames.LevelComplete);

        // the revive offer was declined and Restart confirmed: the lose interstitial first, when the pacing allows it
        private async UniTask LoseInterstitial()
        {
            if (!_adPacing.ShouldShowAfterLose(_level, _clock.UtcNow)) return;
            try { await Interstitial(AdPlacements.InterstitialLevelFail, TrackingNames.LevelFail); }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                _log.Warn($"[GameplayScreen] lose interstitial failed on level {_level}: {e.Message}");   // never strand the player
            }
        }

        private async UniTask<bool> Interstitial(AdPlacement placement, string trackedAs)
        {
            if (_shop.RemoveAdsOwned) return false;          // Remove Ads owners never see an interstitial
            if (!await AdReady(placement)) return false;     // not ready: never block the player
            _tracking.AdStarted(Inter, trackedAs);
            var shown = await _ads.ShowAsync(placement, _cts.Token);
            if (shown == AdResult.Shown || shown == AdResult.Rewarded)
            {
                _adPacing.RecordInterstitialShown(_clock.UtcNow);
                _offers.InterstitialShown();   // counts toward the next Remove Ads popup
                _tracking.AdCompleted(Inter, trackedAs, null);
                return true;
            }

            _tracking.AdFailed(Inter, trackedAs);
            return false;
        }

        // at most one offer popup per level complete: the Starter Pack, else Remove Ads after an interstitial, else the Gold Brick
        private async UniTask LevelCompleteOffer(bool afterInterstitial)
        {
            if (_offers.TakeExpiredGoldBrick(_clock.UtcNow, out bool wasShown)) _tracking.RvPopupExpired(TrackingNames.GoldBrickRv, wasShown);
            switch (_offers.AtLevelComplete(_level, afterInterstitial, _shop.RemoveAdsOwned, _clock.UtcNow))
            {
                case Game.Application.LevelCompleteOffer.Starter:
                    await _starter.ShowAsync(_cts.Token);
                    break;
                case Game.Application.LevelCompleteOffer.RemoveAds:
                    await _removeAds.ShowAsync(_cts.Token);
                    break;
                case Game.Application.LevelCompleteOffer.GoldBrick:
                    await _goldBrick.ShowAsync(_cts.Token);
                    break;
            }

            Draw();   // a purchase changed the coins and boosters on the HUD
        }

        // the prompt is a courtesy: a failure inside it is logged and the level end goes on as if it never came
        private async UniTask<RatingResult> Rating()
        {
            try { return (await _rating.TryPromptAsync(_level, _cts.Token)).Result; }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                _log.Warn($"[GameplayScreen] rating prompt failed after level {_level}: {e.Message}");
                return RatingResult.NotShown;
            }
        }

        private async UniTask<WinPick> ShowLevelUp(int reward, string next, int[] segments, bool adReady, bool nextHard,
            Func<int, UniTask<bool>> payBonus)
        {
            var ct = _cts.Token;
            var prefab = await _assets.LoadAsync(AssetKeys.UI.LevelUpUI, ct);
            GameObject instance = null;
            LevelUpUI ui = null;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = lifetime.Token;
            var choice = new UniTaskCompletionSource<WinPick>();
            bool picked = false;
            var needle = reward > 0 ? WinNeedle(segments) : null;
            long walletShown = 0;   // what the popup's wallet shows; the coins count it up as they land
            Action back = () => Finish(WinChoice.Home);
            try
            {
                instance = Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                ui = instance.GetComponent<LevelUpUI>();
                ui.Init(null);
                FeatureMilestoneProgress feature = null;
                if (_tuning.FeatureProgress(_level, _levels.Count, out int current, out int total, out var booster, out var element))
                {
                    var icon = booster.HasValue
                        ? ui.FeatureBoosterIcon(MoneySortUiValue.Booster((int)booster.Value))
                        : ui.FeatureIcon((int)element.Value);
                    feature = new FeatureMilestoneProgress(current, total, icon, string.Empty);
                }

                ui.SetData(next, -1, null, (Action)(() => FlyThen(false, reward, WinChoice.Next, 0)), feature,
                    new LevelUpRewardOffer(reward, null, null, reward <= 0));
                ui.SetHardContinue(nextHard);
                ui.SetLabel("_coinRewardText", _loc.Get(LocKeys.HudCoins, reward));
                walletShown = Math.Max(0, _progress.Coins - Math.Max(0, reward));   // the reward is saved; it flies in on Next / Bonus
                ui.SetLabel("Wallet", _loc.Get(LocKeys.HudCoins, walletShown));
                ui.SetLabel("FeatureTitle", _loc.Get(LocKeys.WinNewFeature));
                var bonusButton = ui.Control("_rewardedCoinButton");
                bonusButton.gameObject.SetActive(needle != null);
                ui.SetExternalBonus(needle != null ? segments.Select(m => _loc.Get(LocKeys.WinSegment, m)).ToArray() : null,
                    needle != null ? BonusTotal() : _loc.Get(LocKeys.WinBonus), adReady, () => PickBonus().Forget());
                _popupBack = back;
                ui.Show();
                if (needle != null) { Swing().Forget(); WatchAd().Forget(); }
                var result = await choice.Task.AttachExternalCancellation(ct);
                lifetime.Cancel();
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(ct);
                return result;
            }
            finally
            {
                lifetime.Cancel();
                if (_popupBack == back) _popupBack = null;
                if (ui != null) ui.Animation?.Stop();
                if (instance != null) Object.Destroy(instance);
                _assets.Release(prefab);
            }

            void Finish(WinChoice result)
            {
                if (picked) return;
                picked = true;
                choice.TrySetResult(new WinPick(result));
            }

            // Money Sort's level complete coin fly: the coins leave the pressed button, the wallet counts up as they land
            void FlyThen(bool fromBonus, long amount, WinChoice result, int multiplier)
            {
                if (picked && !fromBonus) return;
                picked = true;
                if (amount <= 0 || ui == null) { choice.TrySetResult(new WinPick(result, multiplier)); return; }
                long from = walletShown, to = _progress.Coins;
                _feedback.Coins();
                ui.PlayCoinFly(fromBonus, (int)Math.Min(amount, int.MaxValue),
                    (landed, all) => ui.SetLabel("Wallet", _loc.Get(LocKeys.HudCoins, from + (to - from) * landed / Math.Max(1, all))),
                    () => { ui.SetLabel("Wallet", _loc.Get(LocKeys.HudCoins, to)); walletShown = to; choice.TrySetResult(new WinPick(result, multiplier)); });
            }

            string BonusTotal() => _loc.Get(LocKeys.HudCoins, (long)reward * needle.Multiplier);

            async UniTask Swing()
            {
                int shown = needle.Segment;
                while (!picked && !token.IsCancellationRequested)
                {
                    bool changed = needle.Segment != shown;
                    shown = needle.Segment;
                    ui.SetExternalNeedle(needle.Angle, changed ? BonusTotal() : null);
                    if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow()) return;
                    if (!picked) needle.Advance(Time.unscaledDeltaTime);
                }
            }

            // the Bonus button follows the ad fill while the popup is up
            async UniTask WatchAd()
            {
                while (!token.IsCancellationRequested)
                {
                    if (await UniTask.Delay(TimeSpan.FromSeconds(AdRecheckSeconds), DelayType.UnscaledDeltaTime,
                            cancellationToken: token).SuppressCancellationThrow()) return;
                    if (picked) continue;
                    bool ready = await _ads.IsReadyAsync(AdPlacements.RewardedWinDouble);
                    if (token.IsCancellationRequested || picked || ready == adReady) continue;
                    adReady = ready;
                    ui.SetExternalBonusEnabled(ready);
                }
            }

            async UniTask PickBonus()
            {
                if (picked || needle == null || !adReady) return;
                picked = true;
                int multiplier = needle.Stop();
                ui.SetExternalNeedle(needle.Angle, BonusTotal());
                ui.SetExternalBonusInteractable(false);
                if (await UniTask.Delay(TimeSpan.FromSeconds(_tuning.WinBonusAdDelaySeconds),
                    DelayType.UnscaledDeltaTime, cancellationToken: token).SuppressCancellationThrow()) return;
                if (!await payBonus(multiplier))
                {
                    // ad cancelled or failed: back to the swinging needle
                    if (token.IsCancellationRequested) return;
                    adReady = await AdReady(AdPlacements.RewardedWinDouble);
                    needle.Resume();
                    ui.SetExternalBonusInteractable(true);
                    ui.SetExternalBonusEnabled(adReady);
                    picked = false;
                    Swing().Forget();
                    return;
                }

                FlyThen(true, (long)reward * multiplier, WinChoice.Bonus, multiplier);
            }
        }

        // rule #14: the win bonus needle starts at a point of its swing drawn from a stream seeded here, and the seed is logged
        private WinBonusNeedle WinNeedle(int[] segments)
        {
            long seed = DateTime.UtcNow.Ticks;
            _log.Info($"[GameplayScreen] win bonus needle on level {_level} (seed {seed}).");
            return new WinBonusNeedle(segments, (float)_tuning.WinBonusArcDegrees, (float)_tuning.WinBonusSwingsPerSecond, new Pcg32(seed));
        }

        // ------------------------------------------------------------------ lose (CardSlot, on a full plaza)

        private bool CanRevive => _paidRevives + _reviveAds < _tuning.ReviveMaxPerAttempt && _session.Board.NextPlazaCell().HasValue;

        private bool AnyUnlocked() => Enum.GetValues(typeof(Booster)).Cast<Booster>().Any(_progress.IsUnlocked);

        private async UniTask Lost(float settleSeconds)
        {
            await AnimationsDone(settleSeconds);
            _tracking.ScreenViewed(TrackingNames.EndingPhase);
            _feedback.Lost();
            bool intro = true;   // "Oops! No more slots available" drops in once, when the board is lost
            // the booster hint: the bar counts lose_booster_hint_seconds down, a hand on one booster tile. A booster tapped meanwhile
            // plays and the clock only pauses; time out ⇒ the revive popup
            if (_tuning.LoseBoosterHintSeconds > 0 && AnyUnlocked())
            {
                intro = false;
                double left = _hintLeft ?? _tuning.LoseBoosterHintSeconds;
                _hintLeft = null;
                while (left > 0)
                {
                    var (pick, rest) = await LoseCountdown(left);
                    left = rest;
                    if (pick is not int booster) break;
                    _hintLeft = left;
                    if (await BoosterFromLose((Booster)booster)) return;
                    _hintLeft = null;
                }
            }

            while (true)
            {
                bool offer = CanRevive || AnyUnlocked();
                if (offer)
                {
                    bool adAllowed = _tuning.CanRewardedRevive(_reviveAds);
                    bool adReady = adAllowed && await AdReady(AdPlacements.RewardedRevive);
                    long price = _tuning.RevivePriceAfter(_paidRevives);
                    LoseChoice reviveChoice = CanRevive
                        ? await ShowPlayOnAsync(price, adAllowed, adReady, intro, _cts.Token)
                        : await ShowUseBoosterAsync(intro, _cts.Token);
                    intro = false;
                    switch (reviveChoice)
                    {
                        case LoseChoice.ReviveCoins:
                            if (_progress.Coins < price) { await OpenShopForCoins(TrackingNames.Revive); continue; }
                            if (!_progress.TrySpendCoins(price, TrackingNames.Revive)) continue;
                            _paidRevives++;
                            AddSpace();
                            return;
                        case LoseChoice.ReviveAd:
                            if (!_tuning.CanRewardedRevive(_reviveAds)) continue;
                            if (!await Rewarded(AdPlacements.RewardedRevive, TrackingNames.Revive, Reward("revive", 1))) continue;
                            _reviveAds++;
                            AddSpace();
                            return;
                        case LoseChoice.Booster0:
                        case LoseChoice.Booster1:
                        case LoseChoice.Booster2:
                            if (!await BoosterFromLose((Booster)(reviveChoice - LoseChoice.Booster0))) continue;
                            return;
                    }
                }

                // declined, or nothing left to offer: "Are you sure?"
                var choice = await Confirm(home: false);
                if (choice == null) return;
                if (choice.Value != RestartConfirmChoice.Confirm) continue;   // ✕: back to the offer (Money Sort)
                _tracking.LevelFailed();
                _progress.RecordLoss(_level);
                await LoseInterstitial();
                await LeaveAttempt(home: false, TrackingNames.LoseRestart);
                return;
            }
        }

        // Play On in SqadSort: one more plaza cell (GDD §2.4 Add Space), and the board goes on
        private void AddSpace()
        {
            GridPos? cell = _session.Board.NextPlazaCell();
            if (cell.HasValue) _session.AddSpace(cell.Value);
            _presenter.Show(_session);
        }

        // the bar counts down; an owned booster tapped meanwhile ends it with that tile's index and the time left. A tile with none
        // left opens its purchase popup and the clock stands until it closes
        private async UniTask<(int? pick, double left)> LoseCountdown(double seconds)
        {
            var pick = _countdownPick = new UniTaskCompletionSource<int>();
            try
            {
                int shown = (int)Math.Ceiling(seconds);
                _hud.ShowLoseCountdown(_loc.Get(LocKeys.LoseNoSlots), _loc.Get(LocKeys.LoseCountdown, shown));
                _hud.PointAtBooster(HintBooster());
                double left = seconds;
                while (left > 0)
                {
                    var t0 = _clock.UtcNow;
                    var (picked, index) = await UniTask.WhenAny(pick.Task,
                        UniTask.Delay(TimeSpan.FromSeconds(0.1), DelayType.UnscaledDeltaTime, cancellationToken: _cts.Token));
                    left -= Math.Max(0, (_clock.UtcNow - t0).TotalSeconds);
                    if (picked)
                    {
                        if (_progress.Boosters((Booster)index) > 0) return (index, Math.Max(left, 0.05));
                        _countdownPick = null;
                        await Buy((Booster)index);
                        pick = _countdownPick = new UniTaskCompletionSource<int>();
                        _hud.PointAtBooster(HintBooster());
                        continue;
                    }

                    int now = (int)Math.Ceiling(left);
                    if (now != shown && now > 0) { shown = now; _hud.SetLoseCountdown(_loc.Get(LocKeys.LoseCountdown, now)); }
                }

                return (null, 0);
            }
            finally
            {
                _countdownPick = null;
                _hud.HideLoseCountdown();
            }
        }

        // unlocked ones only: the first owned in the order Add Container → Undo → Shuffle (the ones that free the plaza first),
        // else the first unlocked in that order; -1 = none
        private int HintBooster()
        {
            var order = new[] { Booster.AddContainer, Booster.Undo, Booster.Shuffle };
            foreach (var id in order) if (_progress.IsUnlocked(id) && _progress.Boosters(id) > 0) return (int)id;
            foreach (var id in order) if (_progress.IsUnlocked(id)) return (int)id;
            return -1;
        }

        // the lose popup closed on a booster tile: use it or buy it. False when the board did not change (the offer comes back)
        private async UniTask<bool> BoosterFromLose(Booster id)
        {
            if (!_progress.IsUnlocked(id)) return false;
            if (_progress.Boosters(id) == 0) { await Buy(id); return false; }
            switch (id)
            {
                case Booster.Undo:
                    if (!_session.Undo() || !_progress.TryUseBooster(id)) return false;
                    _presenter.Show(_session);
                    return true;
                case Booster.AddContainer:
                    if (_session.AddContainer() < 0 || !_progress.TryUseBooster(id)) return false;
                    _presenter.Show(_session);
                    return true;
                default:
                    return false;   // a shuffle does not free a full plaza
            }
        }

        // No revive left but a booster unlocked: Out of Space without its coin / ad buttons, copies of the HUD booster tiles above
        // its scrim — a tile is the way on, No thanks / ✕ / Back declines
        private UniTask<LoseChoice> ShowUseBoosterAsync(bool intro, CancellationToken ct) =>
            _popups.ShowAsync<OutOfSpaceUI, LoseChoice>(AssetKeys.UI.OutOfSpaceUI, () => LoseChoice.Decline, (ui, h) =>
            {
                ui.SetData(new OutOfSpaceOffer(0, 0, null, null, () => h.Close(LoseChoice.Decline), intro));
                ui.SetLabel("@Card/Header/Title", _loc.Get(LocKeys.LoseTitle));
                ui.SetLabel("@Card/Message", _loc.Get(LocKeys.LoseUseBooster));
                ui.Control("coinButton")?.gameObject.SetActive(false);
                ui.Control("adButton")?.gameObject.SetActive(false);
                var tiles = _hud.CopyBoosterTiles((RectTransform)ui.transform);
                for (int i = 0; i < tiles.Length; i++)
                {
                    int index = i;
                    if (tiles[i] != null) tiles[i].onClick.AddListener(() => h.Close(LoseChoice.Booster0 + index));
                }
            }, ct);

        private async UniTask<LoseChoice> ShowPlayOnAsync(long price, bool adAllowed, bool adReady, bool intro, CancellationToken ct)
        {
            var prefab = await _assets.LoadAsync(AssetKeys.UI.OutOfSpaceUI, ct);
            GameObject instance = null;
            using var watching = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var choice = new UniTaskCompletionSource<LoseChoice>();
            Action back = () => choice.TrySetResult(LoseChoice.Decline);
            try
            {
                instance = Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                var ui = instance.GetComponent<OutOfSpaceUI>();
                _popupBack = back;
                ui.Init(null);
                ui.SetData(new OutOfSpaceOffer(0, 0, () => choice.TrySetResult(LoseChoice.ReviveCoins),
                    adAllowed ? () => choice.TrySetResult(LoseChoice.ReviveAd) : null, back, intro));
                ui.SetLabel("coinPriceText", _loc.Get(LocKeys.LoseRevivePrice, price));
                ui.SetLabel("@Card/Header/Title", _loc.Get(LocKeys.LosePlayOn));
                ui.SetLabel("@Card/Message", _loc.Get(LocKeys.LosePlayOnMessage).Replace("\\n", "\n"));
                ui.SetButtonLabel("adButton", _loc.Get(LocKeys.LosePlayOnFree));
                if (adAllowed)
                {
                    ui.SetAdAvailable(adReady);
                    WatchAd(watching.Token).Forget();
                }

                ui.Show();
                var result = await choice.Task.AttachExternalCancellation(ct);
                watching.Cancel();
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(ct);
                return result;
            }
            finally
            {
                watching.Cancel();
                if (_popupBack == back) _popupBack = null;
                if (instance != null) Object.Destroy(instance);
                _assets.Release(prefab);
            }

            async UniTask WatchAd(CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    if (await UniTask.Delay(TimeSpan.FromSeconds(AdRecheckSeconds), DelayType.UnscaledDeltaTime,
                            cancellationToken: token).SuppressCancellationThrow()) return;
                    bool ready = await _ads.IsReadyAsync(AdPlacements.RewardedRevive);
                    if (token.IsCancellationRequested || ready == adReady || instance == null) continue;
                    adReady = ready;
                    instance.GetComponent<OutOfSpaceUI>().SetAdAvailable(ready);
                }
            }
        }

        // ------------------------------------------------------------------ boosters

        private void OnBoosterPressed(int index)
        {
            // during the lose countdown a booster tile answers the countdown (the flow is busy, the board is lost)
            if (_countdownPick != null)
            {
                if (index >= 0 && index <= 2 && _progress.IsUnlocked((Booster)index)) _countdownPick.TrySetResult(index);
                return;
            }

            if (_session == null || index < 0 || index > 2) return;
            // the guide takes only its own tile, and only while it points at it
            if (_boosterGuideStep != BoosterGuideStep.None
                && (_boosterGuideStep != BoosterGuideStep.Booster || index != (int)_guidedBooster)) return;
            if (_presenter.IsShufflePicking) { _presenter.CancelShufflePick(); return; }   // the active tile again = cancel
            if (_busy || _session.Board.State != BoardState.Playing) return;
            var id = (Booster)index;
            if (!_progress.IsUnlocked(id)) return;                                         // a locked tile does nothing
            if (_progress.Boosters(id) == 0) { Guard(() => Buy(id)); return; }
            bool guided = _boosterGuideStep == BoosterGuideStep.Booster;

            bool used = false;
            switch (id)
            {
                case Booster.Undo:
                    used = _session.Undo() && _progress.TryUseBooster(id);
                    if (used) _presenter.Show(_session);   // rebuild from the restored state
                    break;
                case Booster.Shuffle:
                    // armed until a tube is tapped; the saved booster is spent when the shuffle happens (OnShuffleDone)
                    _shuffleChargesArmed = _session.Charges(Booster.Shuffle);
                    _presenter.BeginShufflePick();
                    used = true;
                    break;
                case Booster.AddContainer:
                    used = _session.AddContainer() >= 0 && _progress.TryUseBooster(id);
                    if (used) _presenter.Show(_session);
                    break;
            }

            if (guided && used)
            {
                if (id == Booster.Shuffle) { _boosterGuideStep = BoosterGuideStep.Tube; ShowShuffleTubeGuide(); }
                else CompleteBoosterGuide();
            }
            Draw();
        }

        // the armed Shuffle ended: used on a tube (the session spent one) or cancelled (nothing spent)
        private void OnShuffleDone()
        {
            bool used = _session != null && _shuffleChargesArmed >= 0 && _session.Charges(Booster.Shuffle) < _shuffleChargesArmed;
            _shuffleChargesArmed = -1;
            if (used) _progress.TryUseBooster(Booster.Shuffle);
            if (_boosterGuideStep == BoosterGuideStep.Tube)
            {
                if (used) CompleteBoosterGuide();
                else { _boosterGuideStep = BoosterGuideStep.Booster; ShowBoosterTileGuide(); }   // cancelled: back to the tile
            }
            Draw();
        }

        // ------------------------------------------------------------------ first-use booster guide (CardSlot)

        private static string BoosterGuideKey(Booster id) => "booster.guide." + id.ToString().ToLowerInvariant();

        /// <param name="needTarget">false: only whether the guide belongs to this level (Undo asks again after a move).</param>
        private bool CanGuideBooster(Booster id, bool needTarget = true)
        {
            if (_hud == null || _hud.ElementTutorial == null || _session == null || _progress.IsFtueDone(BoosterGuideKey(id))
                || !_progress.IsUnlocked(id) || _progress.Boosters(id) <= 0 || _level != _tuning.UnlockLevel(id)) return false;
            if (!needTarget) return true;
            var board = _session.Board;
            if (board.State != BoardState.Playing) return false;
            switch (id)
            {
                case Booster.Undo: return _session.CanUndo;
                case Booster.AddContainer: return board.CanAddSpareTube;
                default: return ShuffleTarget() >= 0;
            }
        }

        // the first open tube with two buddies or more to reorder
        private int ShuffleTarget()
        {
            var tubes = _session.Board.Tubes;
            for (int i = 0; i < tubes.Count; i++) if (tubes[i].IsActive && tubes[i].Count >= 2) return i;
            return -1;
        }

        private void StartBoosterGuide(Booster id)
        {
            _guidedBooster = id;
            _boosterGuideStep = BoosterGuideStep.Booster;
            ShowBoosterTileGuide();
        }

        private void CompleteBoosterGuide()
        {
            _progress.MarkFtueDone(BoosterGuideKey(_guidedBooster));
            _boosterGuideStep = BoosterGuideStep.None;
            HideBoosterTutorial();
        }

        // a level start or end drops a guide that was not finished; it comes back the next time this level is played
        private void EndBoosterGuide()
        {
            _boosterGuideStep = BoosterGuideStep.None;
            _undoGuidePending = false;
            _boosterGuideTarget = -1;
            HideBoosterTutorial();
        }

        private void ShowBoosterTileGuide()
        {
            var tile = _hud.BoosterTile((int)_guidedBooster);
            if (tile == null) return;
            var corners = new Vector3[4];
            tile.GetWorldCorners(corners);
            var cam = _layers.GetCamera(RenderLayers.Ui);
            var hint = _guidedBooster == Booster.Undo ? LocKeys.BoosterGuideHandTap
                : _guidedBooster == Booster.Shuffle ? LocKeys.BoosterGuideShuffleTap : LocKeys.BoosterGuideMagnetTap;
            ShowBoosterGuideTarget(PopupRect(cam.WorldToScreenPoint(corners[0]), cam.WorldToScreenPoint(corners[2])),
                _loc.Get(hint), topPanel: true, pointDown: true);
        }

        private void ShowShuffleTubeGuide()
        {
            _boosterGuideTarget = ShuffleTarget();
            if (_boosterGuideTarget < 0) return;
            // the tube's tap area (screen-aligned, on the board's own layer) is what the spot must uncover
            var hit = _boardInstance.GetComponentsInChildren<Collider2D>()
                .FirstOrDefault(c => c.transform.parent != null && c.transform.parent.name == $"Tube {_boosterGuideTarget}");
            if (hit == null) return;
            var cam = _layers.GetCamera(RenderLayers.GamePlay);
            var b = hit.bounds;
            ShowBoosterGuideTarget(PopupRect(cam.WorldToScreenPoint(b.min), cam.WorldToScreenPoint(b.max)),
                _loc.Get(LocKeys.BoosterGuideShuffleBody), topPanel: false);
        }

        // two screen points as a world rectangle on the Popup layer, where the guide is drawn
        private Rect PopupRect(Vector2 screenA, Vector2 screenB)
        {
            var host = (RectTransform)_layers.GetHost(RenderLayers.Popup);
            var cam = _layers.GetCamera(RenderLayers.Popup);
            RectTransformUtility.ScreenPointToWorldPointInRectangle(host, screenA, cam, out var a);
            RectTransformUtility.ScreenPointToWorldPointInRectangle(host, screenB, cam, out var b);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private void ShowBoosterGuideTarget(Rect target, string text, bool topPanel, bool pointDown = false)
        {
            HideBoosterTutorial();
            _boosterTutorial = Object.Instantiate(_hud.ElementTutorial, _layers.GetHost(RenderLayers.Popup), false);
            _layers.Stamp(_boosterTutorial.gameObject, RenderLayers.Popup);
            _boosterTutorial.Init(null);
            _boosterTutorial.SetGuidedTarget(target, text, topPanel, pointDown);
            _boosterTutorial.Show();
        }

        private void HideBoosterTutorial()
        {
            if (_boosterTutorial == null) return;
            _boosterTutorial.ReleaseInput();
            Object.Destroy(_boosterTutorial.gameObject);
            _boosterTutorial = null;
        }

        private async UniTask OpenShopForCoins(string placement)
        {
            await _shop.OpenOverlayAsync(placement, _cts.Token);
            HideBannerIfAdsRemoved();
            Draw();
        }

        // Remove Ads bought from a Store over the level takes the banner away now, not at the next level
        private void HideBannerIfAdsRemoved() { if (_shop.RemoveAdsOwned) _banner.Hide(); }

        private async UniTask Buy(Booster id)
        {
            while (true)
            {
                bool adReady = await AdReady(AdPlacements.RewardedBooster);
                int price = _tuning.Price(id);
                var result = await ShowBoosterPurchaseUI(id, price, adReady);
                if (result == BoosterBuyChoice.Close) return;
                if (result == BoosterBuyChoice.Coins)
                {
                    if (_progress.Coins < price) { await OpenShopForCoins(TrackingNames.Booster(id)); continue; }
                    if (!_progress.TryBuyBooster(id)) continue;   // short or not saved: nothing changed, offer again
                }
                else if (!await Rewarded(AdPlacements.RewardedBooster, TrackingNames.Booster(id), Reward(TrackingNames.Booster(id), _tuning.AdAmount(id)))
                         || !_progress.GrantBoosterFromAd(id)) continue;

                _feedback.BoosterReward();
                Draw();
                return;
            }
        }

        private async UniTask<BoosterBuyChoice> ShowBoosterPurchaseUI(Booster id, int price, bool adReady)
        {
            var prefab = await _assets.LoadAsync(AssetKeys.UI.BoosterPurchaseUI, _cts.Token);
            GameObject instance = null;
            BoosterPurchaseUI ui = null;
            using var watching = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            var choice = new UniTaskCompletionSource<BoosterBuyChoice>();
            Action back = () => choice.TrySetResult(BoosterBuyChoice.Close);
            try
            {
                instance = Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                ui = instance.GetComponent<BoosterPurchaseUI>();
                ui.Init(null);
                _popupBack = back;
                var type = MoneySortUiValue.Booster((int)id);
                ui.SetData(new BoosterPurchaseOffer(type, _loc.Get(BoosterName(id)).ToUpperInvariant(), _loc.Get(BoosterTip(id)),
                    price, _tuning.BuyAmount(id), (int)_progress.Coins, ui.Icon(type),
                    () => choice.TrySetResult(BoosterBuyChoice.Coins),
                    () => choice.TrySetResult(BoosterBuyChoice.Ad),
                    back, _tuning.AdAmount(id)));
                ui.SetLabel("_coinPriceText", _loc.Get(LocKeys.BoosterPrice, price));
                ui.SetLabel("_messageText", _progress.Coins >= price ? string.Empty : _loc.Get(LocKeys.BoosterBuyNotEnough));
                ui.SetAd("_adButton", adReady);
                WatchAd(watching.Token).Forget();
                ui.Show();
                var result = await choice.Task.AttachExternalCancellation(_cts.Token);
                watching.Cancel();
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(_cts.Token);
                return result;
            }
            finally
            {
                watching.Cancel();
                if (_popupBack == back) _popupBack = null;
                if (ui != null) ui.Animation?.Stop();
                if (instance != null) Object.Destroy(instance);
                _assets.Release(prefab);
            }

            async UniTask WatchAd(CancellationToken token)
            {
                while (!token.IsCancellationRequested)
                {
                    if (await UniTask.Delay(TimeSpan.FromSeconds(AdRecheckSeconds), DelayType.UnscaledDeltaTime,
                            cancellationToken: token).SuppressCancellationThrow()) return;
                    bool ready = await _ads.IsReadyAsync(AdPlacements.RewardedBooster);
                    if (token.IsCancellationRequested || ready == adReady || ui == null) continue;
                    adReady = ready;
                    ui.SetAd("_adButton", ready);
                }
            }
        }

        // the unlock popup at the first start of the booster's level; false if the screen went away under it
        private async UniTask<bool> ShowUnlock(Booster id)
        {
            try
            {
                await _popups.ShowAsync<BoosterUnlockUI, bool>(AssetKeys.UI.BoosterUnlockUI, () => true, (ui, h) =>
                {
                    var type = MoneySortUiValue.Booster((int)id);
                    string name = _loc.Get(BoosterName(id));
                    ui.SetData(new BoosterUnlockOffer(name, _loc.Get(BoosterTip(id)), ui.Icon(type), type, _hud.BoosterTile((int)id),
                        () => h.Close(true), false));
                    ui.SetButtonLabel("guideOk", _loc.Get(LocKeys.BoosterUnlockClaim));
                    ui.SetLabel("titleText", _loc.Get(LocKeys.BoosterUnlockTitle));
                    ui.SetLabel("boosterNameText", name);
                    ui.SetLabel("unlockedText", _loc.Get(LocKeys.UnlockUnlocked));
                    ui.SetButtonLabel("claimButton", _loc.Get(LocKeys.BoosterUnlockClaim));
                }, _cts.Token);
            }
            catch (OperationCanceledException) { return false; }

            _feedback.BoosterReward();   // the unlock gift
            return true;
        }

        private async UniTask ShowElementUnlockAsync(ElementId id, CancellationToken ct)
        {
            var prefab = await _assets.LoadAsync(AssetKeys.UI.ElementUnlockUI, ct);
            GameObject instance = null;
            var close = new UniTaskCompletionSource();
            Action back = () => close.TrySetResult();
            try
            {
                instance = Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                var ui = instance.GetComponent<ElementUnlockUI>();
                _popupBack = back;
                ui.Init(null);
                ui.SetData(new ElementUnlockOffer(_loc.Get(ElementName(id)), _loc.Get(ElementTip(id)), MoneySortElementType.HiddenMoney, back));
                ui.ShowIllustration((int)id);
                ui.Show();
                await close.Task.AttachExternalCancellation(ct);
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(ct);
            }
            finally
            {
                if (_popupBack == back) _popupBack = null;
                if (instance != null) Object.Destroy(instance);
                _assets.Release(prefab);
            }
        }

        // ------------------------------------------------------------------ rewarded ads

        // reward only on completion (G19); IAA_completed goes out before the caller grants
        private async UniTask<bool> Rewarded(AdPlacement placement, string trackedAs, string reward)
        {
            _tracking.AdStarted(Rv, trackedAs);
            var shown = await _ads.ShowAsync(placement, _cts.Token);
            if (shown == AdResult.Rewarded || shown == AdResult.Skipped) _adPacing.RecordRewardedClosed(_clock.UtcNow);
            if (shown != AdResult.Rewarded)
            {
                _tracking.AdFailed(Rv, trackedAs);
                if (shown == AdResult.Failed) await _noAds.ShowAsync(_cts.Token);   // Money Sort's ErrorUI: no ad could play
                return false;
            }

            _tracking.AdCompleted(Rv, trackedAs, reward);
            return true;
        }

        // the plan's reward parameter: a one-entry JSON object, e.g. {"coins":40}
        private static string Reward(string what, long amount) =>
            "{\"" + what + "\":" + amount.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";

        // IAdsService.IsReadyAsync takes no token: make sure the screen is still up when it answers
        private async UniTask<bool> AdReady(AdPlacement placement)
        {
            bool ready = await _ads.IsReadyAsync(placement);
            _cts.Token.ThrowIfCancellationRequested();
            return ready;
        }

        // ------------------------------------------------------------------ pause / restart (CardSlot)

        private Action _popupBack;   // Back on a hand-rolled popup (Level Up, Play On, purchase, element unlock)

        // Money Sort has no Pause panel: the pause button opens Settings, with Restart and Home under the toggles
        private async UniTask Pause()
        {
            while (true)
            {
                var choice = await _settings.ShowAsync(false, _cts.Token);
                if (choice == SettingsChoice.Close) return;
                bool left = choice == SettingsChoice.Restart ? await ConfirmRestart() : await ConfirmHome();
                if (left) return;   // ✕ on the confirm: back to Settings (Money Sort)
            }
        }

        // ↻ or Settings → Restart: "Are you sure?" then the restart, which costs the life. With no life left the button is Return Home.
        private async UniTask<bool> ConfirmRestart()
        {
            bool noLife = _progress.Lives(_clock.UtcNow) <= 0;
            if (await Confirm(home: noLife) != RestartConfirmChoice.Confirm) return false;
            await LeaveAttempt(home: noLife, noLife ? TrackingNames.ExitHome : TrackingNames.ManualRestart);
            return true;
        }

        // Settings → Home: "Are you sure?" with Return Home, which costs the life
        private async UniTask<bool> ConfirmHome()
        {
            if (await Confirm(home: true) != RestartConfirmChoice.Confirm) return false;
            await LeaveAttempt(home: true, TrackingNames.ExitHome);
            return true;
        }

        // Money Sort's LoseUI as the confirm; null when the screen went away under it
        private async UniTask<RestartConfirmChoice?> Confirm(bool home)
        {
            var prefab = await _assets.LoadAsync(AssetKeys.UI.LoseUI, _cts.Token);
            GameObject instance = null;
            LoseUI ui = null;
            var choice = new UniTaskCompletionSource<RestartConfirmChoice>();
            Action back = () => choice.TrySetResult(RestartConfirmChoice.Back);
            try
            {
                instance = Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                ui = instance.GetComponent<LoseUI>();
                ui.Init(null);
                _popupBack = back;
                ui.SetData(new LosePresentation(_loc.Get(LocKeys.LoseConfirmTitle), _loc.Get(LocKeys.LoseConfirmBody),
                    _loc.Get(home ? LocKeys.LoseReturnHome : LocKeys.LoseRestart),
                    () => choice.TrySetResult(RestartConfirmChoice.Confirm), back, home ? LoseButtonStyle.ReturnHome : LoseButtonStyle.Restart));
                ui.Show();
                var result = await choice.Task.AttachExternalCancellation(_cts.Token);
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(_cts.Token);
                return result;
            }
            catch (OperationCanceledException) { return null; }
            finally
            {
                if (_popupBack == back) _popupBack = null;
                if (ui != null) ui.Animation?.Stop();
                if (instance != null) Object.Destroy(instance);
                _assets.Release(prefab);
            }
        }

        // confirming a restart or Home is what costs the life; the last one goes Home, to Out of Lives there
        private async UniTask LeaveAttempt(bool home, string trigger)
        {
            var now = _clock.UtcNow;
            if (_progress.Lives(now) > 0 && !_progress.TrySpendLife(now))
                _log.Warn($"[GameplayScreen] the life for leaving level {_level} was not saved.");
            if (_progress.Lives(_clock.UtcNow) <= 0) { GoHome(trigger); return; }
            if (home) { GoHome(); return; }
            await Restart(_level);
            if (trigger == TrackingNames.LoseRestart) await OfferRetryAfterRestart();
        }

        // after a lose-restart, one help popup: Skip Level (an ad) first, else the Restart Booster (Money Sort)
        private async UniTask OfferRetryAfterRestart()
        {
            if (_progress.CanOfferSkipAfterRestart(_level, false, _clock.UtcNow) && await AdReady(AdPlacements.RewardedSkipLevel))
            {
                bool watch = await ShowRetryUI(AssetKeys.UI.SkipLevelUI, _loc.Get(LocKeys.SkipTitle), _loc.Get(LocKeys.SkipBody), _loc.Get(LocKeys.SkipAction));
                if (watch && await Rewarded(AdPlacements.RewardedSkipLevel, TrackingNames.SkipLevelPlacement, Reward("skip_level", 1)))
                    await Won(0f, _progress.SkipLevel(_level, _levels.Count, _clock.UtcNow), afterAd: true);
                return;
            }

            await OfferRestartBooster();
        }

        private async UniTask OfferRestartBooster()
        {
            if (!_progress.CanOfferRestartBooster(_level, _clock.UtcNow) || !await AdReady(AdPlacements.RewardedBooster)) return;
            if (!_progress.TryMarkRestartBoosterOffered(_level, _clock.UtcNow)) return;
            const int amount = 1;
            bool watch = await ShowRetryUI(AssetKeys.UI.RestartBoosterUI, _loc.Get(LocKeys.RestartBoosterTitle), _loc.Get(LocKeys.RestartBoosterBody),
                _loc.Get(LocKeys.RestartBoosterGet), new[] { _loc.Get(LocKeys.BoosterCount, amount) });
            if (!watch) return;
            if (await Rewarded(AdPlacements.RewardedBooster, TrackingNames.Booster(Booster.AddContainer),
                    Reward(TrackingNames.Booster(Booster.AddContainer), amount)) && _progress.GrantRestartBoosterFromAd(_clock.UtcNow))
            {
                _feedback.BoosterReward();
                Draw();
            }
        }

        // the authored Money Sort prompt (Skip Level / Restart Booster); its own root supplies the scrim and input surface
        private async UniTask<bool> ShowRetryUI(AssetKey<GameObject> key, string title, string body, string action, string[] rewards = null)
        {
            var prefab = await _assets.LoadAsync(key, _cts.Token);
            GameObject instance = null;
            RewardedAdPromptUI ui = null;
            var choice = new UniTaskCompletionSource<bool>();
            Action back = () => choice.TrySetResult(false);
            try
            {
                instance = Object.Instantiate(prefab, _layers.GetHost(RenderLayers.Popup), false);
                _layers.Stamp(instance, RenderLayers.Popup);
                ui = instance.GetComponent<RewardedAdPromptUI>();
                ui.Init(null);
                _popupBack = back;
                ui.SetData(new RewardedAdPromptOffer(() => choice.TrySetResult(true), back, rewardLabels: rewards));
                ui.SetLabel("@PromptCard/Title", title);
                ui.SetLabel("@PromptCard/Description", body);
                ui.SetButtonLabel("_noThanksButton", _loc.Get(LocKeys.LoseNoThanks));
                ui.SetButtonLabel("_watchAdButton", action);
                ui.SetAd("_watchAdButton", true);
                ui.Show();
                bool watchAd = await choice.Task.AttachExternalCancellation(_cts.Token);
                var hidden = new UniTaskCompletionSource();
                ui.HideWithCompletion(() => hidden.TrySetResult());
                await hidden.Task.AttachExternalCancellation(_cts.Token);
                return watchAd;
            }
            finally
            {
                if (_popupBack == back) _popupBack = null;
                if (ui != null) ui.Animation?.Stop();
                if (instance != null) Object.Destroy(instance);
                _assets.Release(prefab);
            }
        }

        private void GoHome(string outOfLives = null) =>
            _scenes.LoadAsync(SceneKeys.Main, new MainParam(ColdBoot: false, outOfLives), SceneTransition.Replace).Forget();

        // ------------------------------------------------------------------ tester menu

        public bool CheatWin()
        {
            if (_busy || _session == null || _session.Board.State != BoardState.Playing) return false;
            _log.Info($"[GameplayScreen] cheat: level {_level} won.");
            Guard(() => Won(0f));
            return true;
        }

        public bool CheatLose()
        {
            if (_busy || _session == null || _session.Board.State != BoardState.Playing) return false;
            _log.Info($"[GameplayScreen] cheat: level {_level} lost.");
            Guard(() => Lost(0f));
            return true;
        }

        public void CheatRefresh() => Draw();
    }
}
