using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using VContainer;
using VContainer.Unity;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Composition;
using ClassicSpins.PrototypeFramework.Domain;
using Game.Application;
using Game.Infrastructure;
using ClassicSpins.PrototypeFramework.Infrastructure;
using ClassicSpins.PrototypeFramework.Presentation;
using Game.Gen;

namespace Game.Composition
{
    /// <summary>Starts the No Internet watcher's loop with the Root scope and stops it with the scope (CardSlot D-063).</summary>
    public sealed class OfflineWatcherEntry : IAsyncStartable
    {
        private readonly Game.Presentation.OfflineWatcher _watcher;

        public OfflineWatcherEntry(Game.Presentation.OfflineWatcher watcher) => _watcher = watcher;

        public UniTask StartAsync(System.Threading.CancellationToken cancellation) => _watcher.RunAsync(cancellation);
    }

    /// <summary>
    /// The SqadSort SKU's concrete FrameworkSettings — the SINGLE registrant of the SKU-owned config the
    /// package cannot know: the real FirstSceneConfig target and the bundled ConfigDefaults. Emitted
    /// once by the Setup Wizard (generate-once) — this file is yours now; grow it with the SKU.
    /// </summary>
    [CreateAssetMenu(fileName = "SqadSortFrameworkSettings", menuName = "SqadSort/Framework Settings")]
    public sealed class SqadSortFrameworkSettings : FrameworkSettings
    {
        [Tooltip("Master / Music / Sfx / UI buses with MasterVolume / MusicVolume / SfxVolume / UiVolume exposed: the Sound and Music toggles act on it.")]
        [SerializeField] private UnityEngine.Audio.AudioMixer _audioMixer;

        protected override void InstallGameConfig(IContainerBuilder builder)
        {
            // Mobile defaults to 30 FPS when targetFrameRate is -1; request 60 from startup (CardSlot does the same).
            UnityEngine.Application.targetFrameRate = 60;

            // Choose after UserDataLoaded, inside the loader: resolving this config during boot
            // construction must not inspect the save before the framework has loaded it.
            builder.Register(resolver => new FirstSceneConfig(
                SceneKeys.Main,
                (sceneService, ct) =>
                {
                    var progress = resolver.Resolve<LevelProgressService>();
                    if (progress.HighestCleared == 0 && progress.CurrentLevel == 1
                        && progress.Lives(resolver.Resolve<IClock>().UtcNow) > 0)
                    {
                        // Main normally owns these; the first-player route never loads Main.
                        resolver.Resolve<IGameTracking>().SessionStarted();
                        progress.EnsureStartCoins();
                        return sceneService.LoadAsync(SceneKeys.Gameplay, new GameplayParam(1),
                            SceneTransition.Replace, null, ct);
                    }

                    return sceneService.LoadAsync(SceneKeys.Main, new MainParam(ColdBoot: true),
                        SceneTransition.Replace, null, ct);
                }), Lifetime.Singleton);

            // The SKU owns the single ConfigDefaults registration (the framework registers none —
            // zero registrants means every Get falls back to 0; two is a VContainer duplicate conflict).
            // Every balance number is a GameConfigKeys entry (EconomyTuning's initializers are the defaults), plus the
            // pf-ads-max unit ids and rewarded flags — the same bundle CardSlot ships.
            builder.RegisterInstance(AdUnitConfig.With(EconomyConfig.Defaults()));

            InstallBoot(builder);
            InstallGame(builder);
            InstallAudio(builder);
        }

        /// <summary>
        /// The framework's FeelInstaller builds AudioService with no mixer, so the Sound / Music toggles would only be saved. The
        /// same AudioService with the SKU mixer wins by later registration (CardSlot's InstallAudio), as the IAudioService contract.
        /// </summary>
        private void InstallAudio(IContainerBuilder builder)
        {
            if (_audioMixer == null) return;   // no mixer assigned: keep the framework's mixer-less service
            var mixer = _audioMixer;
            builder.Register<IAudioService>(resolver => new AudioService(
                    resolver.Resolve<IAssetService>(),
                    resolver.Resolve<IUserData>(),
                    new ClassicSpins.PrototypeFramework.Application.BusSettingsCore(),
                    new ClassicSpins.PrototypeFramework.Application.DuckCore(),
                    resolver.Resolve<ILog>(),
                    mixer),
                Lifetime.Singleton);
        }

        /// <summary>
        /// Session-long meta shared by Home and Gameplay, ported from CardSlot: progress and settings persist through the
        /// framework's <c>IUserData</c>; coins through its wallet; lives, boosters, elements, offers and ad pacing through
        /// <see cref="LevelProgressService"/> and friends. The popups open directly on the Popup layer (<see cref="Game.Presentation.PopupUi"/>,
        /// Money Sort's way — no framework dialog), and the dim behind any framework dialog takes the token scrim (rule #17).
        /// Shop / IAP and the rating prompt join later.
        /// </summary>
        private static void InstallGame(IContainerBuilder builder)
        {
            // read once, after boot's RemoteFetch node merged the remote layer
            builder.Register(c => EconomyConfig.Read(c.Resolve<IGameConfig>()), Lifetime.Singleton);
            // save models: defaults the framework's FileUserData loads into
            builder.Register<ProgressModel>(Lifetime.Singleton).As<IUserModel>();
            builder.Register<SettingsModel>(Lifetime.Singleton).As<IUserModel>();
            builder.Register<UserDataProgressStore>(Lifetime.Singleton).As<IProgressStore>();
            // progression facts and gameplay's reports → the tracking plan; one instance holds the open attempt.
            // isTest marks Editor / development-build ads as test ads; retained_days reads the framework clock.
            builder.Register(c =>
                {
                    var clock = c.Resolve<IClock>();
                    return new GameTracking(c.Resolve<IAnalytics>(), c.Resolve<IWalletService>(), c.Resolve<IProgressStore>(), Debug.isDebugBuild,
                        () => clock.UtcNow.UtcDateTime);
                },
                Lifetime.Singleton).As<IGameTracking>().As<IProgressFacts>();
            builder.Register<LevelProgressService>(Lifetime.Singleton);
            builder.Register<TrackingLifecycle>(Lifetime.Singleton).As<IAppPhaseHandler>();   // app_backgrounded, quit = abandoned
            builder.Register<AdPacing>(Lifetime.Singleton);
            builder.Register<GameplayBanner>(Lifetime.Singleton);   // the bottom banner during play (IBannerAds: framework fake or pf-ads-max)
            builder.Register<OfferSchedule>(Lifetime.Singleton);   // when the offer popups show (session state: Root)
            // offline players without Remove Ads wait behind the No Internet popup (Root: it covers every screen)
            builder.Register<UnityNetworkStatus>(Lifetime.Singleton).AsSelf().As<INetworkStatus>();
            builder.Register<OnlineGate>(Lifetime.Singleton);
            builder.Register<Game.Presentation.PopupUi>(Lifetime.Singleton);   // the direct Money Sort popups and their Back
            var scrim = Game.Views.DesignTokens.Scrim;
            builder.RegisterInstance(new DialogDimStyle(scrim.r, scrim.g, scrim.b, opacity: scrim.a));
            // haptics with Money Sort's strengths: the framework's HapticService on the SKU's device backend (wins by later registration)
            builder.Register<SqadSortHapticService>(Lifetime.Singleton).As<IHapticService>();
            builder.Register<Game.Presentation.GameFeedback>(Lifetime.Singleton);   // session-long, so the music carries across screens
            // the store's catalog: the bundled products.json, ids normalized for the platform's store before the IAP module fetches them
            builder.Register<PlatformProductCatalog>(c => new PlatformProductCatalog(
                new BakedCatalogSource(c.Resolve<ILog>()), ShopCatalog.AndroidStore), Lifetime.Singleton).As<ICatalogSource>();
            // the rating prompt — the engine side of com.classicspins.rating, and focus from the kernel's lifecycle hook
            builder.Register<ClassicSpins.Rating.UnityRatingHost>(Lifetime.Singleton).As<ClassicSpins.Rating.IRatingHost>();
            builder.Register<Game.Presentation.RatingFocus>(Lifetime.Singleton).AsSelf().As<IAppPhaseHandler>();
            builder.Register<Game.Presentation.NoAdsPopup>(Lifetime.Singleton);    // Money Sort's ErrorUI: over Gameplay and the Root popups
            builder.Register<Game.Presentation.SettingsPopup>(Lifetime.Singleton);
            builder.Register<Game.Presentation.OfflineWatcher>(Lifetime.Singleton).AsSelf().As<IAppPhaseHandler>();
            builder.RegisterEntryPoint<OfflineWatcherEntry>(Lifetime.Singleton);
            // the Store and its popups (Root: opened over Home and Gameplay); the store itself is the framework's IPurchaseService
            builder.Register<Game.Presentation.ShopPurchases>(Lifetime.Singleton);
            builder.Register<Game.Presentation.GoldBrickFlow>(Lifetime.Singleton);
            builder.Register<Game.Presentation.StarterPackPopup>(Lifetime.Singleton);
            builder.Register<Game.Presentation.RemoveAdsPopup>(Lifetime.Singleton);
            // the level catalog at Root too: the tester menu's "Go to level" reads its count (the screens keep their own)
            builder.Register<ResourcesLevelCatalog>(Lifetime.Singleton).As<ILevelCatalog>();
            CheatsInstaller.Install(builder);   // tester menu (Editor / development build / CS_CHEATS only), as CardSlot D-048
        }

        /// <summary>
        /// The SKU's own boot graph additions: show the Loading scene, hold boot on a test node, then let
        /// the first-scene load put Gameplay or Main up and take the Loading scene back down.
        /// </summary>
        /// <remarks>
        /// Boot nodes are collected by the framework's <c>BootRunInstaller</c> from every registered
        /// <see cref="IBootNode"/>, and SKU installers run last, so these land in the graph with no
        /// framework-code edit. The order is expressed purely as capability edges (AD-4):
        /// <c>AssetReady → LoadingScene → TestWait → FirstScene</c>. The decorator adds exactly ONE
        /// edge in front of the framework's FirstScene node — <c>GameBootCaps.TestWaitDone</c> — and
        /// nothing else; see the comment at the override for why the monetization caps are not there.
        /// </remarks>
        private static void InstallBoot(IContainerBuilder builder)
        {
            // AsSelf() keeps `Resolve<LoadingSceneHost>()` working (naming any contract with As<> would
            // otherwise drop the concrete one). As<IDisposable>() is the LAST-RESORT backstop only: it
            // fires when the Root scope is actually disposed, which for a session-long RootLifetimeScope
            // means play-mode exit — it catches a leaked handle, NOT an aborted boot. The abort case is
            // closed by the entry-point exception handler below.
            builder.Register<LoadingSceneHost>(Lifetime.Singleton).AsSelf().As<IDisposable>();
            builder.Register<LoadingSceneNode>(Lifetime.Singleton).As<IBootNode>();
            builder.Register<TestWaitNode>(Lifetime.Singleton).As<IBootNode>();

            // The boot-abort teardown, for real. A Required boot node failing makes BootFlow throw
            // BootAbortedException; it propagates out of BootstrapEntryPoint.StartAsync into VContainer's
            // entry-point dispatcher, which does NOT dispose the container — and RootLifetimeScope lives
            // for the whole session, so IDisposable alone would not run until play-mode exit, when the
            // scene is unloading anyway. Without this hook an opaque, raycast-blocking overlay sits over
            // the aborted boot with no way down.
            //
            // What VContainer does instead: it hands the faulted IAsyncStartable task to the registered
            // entry-point exception handler (UniTask's Forget(handler) switches to the main thread before
            // invoking it, and the container is still alive), so this is the hook that fires AT abort
            // time. It needs no framework change — the framework registers no handler, and SKU installers
            // run last, so this registration wins by later-registration.
            //
            // Winning means SHADOWING VContainer's own Debug.LogException default. That is what the
            // re-log below is for: delete it and every entry-point exception in the SKU vanishes.
            IObjectResolver rootResolver = null;
            builder.RegisterBuildCallback(resolver => rootResolver = resolver);
            builder.RegisterEntryPointExceptionHandler(ex =>
            {
                Debug.LogException(ex); // MANDATORY: this handler replaces VContainer's default logger
                try
                {
                    // TryResolve, never Resolve: an abort before LoadingScene ran leaves nothing to hide,
                    // and a throw from inside this handler is only republished as an unobserved task
                    // exception — a swallowed teardown failure would be invisible.
                    if (rootResolver != null && rootResolver.TryResolve<LoadingSceneHost>(out var host))
                        host.HideAsync().Forget();
                }
                catch (Exception teardown)
                {
                    Debug.LogWarning("[Boot] Loading-scene teardown after an entry-point exception failed: "
                        + teardown.Message);
                }
            });

            // The AD-17 seam: swap the framework FirstScene node for the same node behind a
            // TestWaitDone edge, with the Loading-scene teardown appended. The load itself is untouched —
            // it still runs the FirstSceneConfig delegate registered above.
            //
            // Deliberately ONLY TestWaitDone — never the framework's AdsReady / AnalyticsReady caps. The
            // framework's AdsInitNode documents that "FirstScene never waits" on AdsReady, and AdsInit
            // itself requires the ConsentResolved cap: an AdsReady edge here would make FirstScene a
            // TRANSITIVE dependent of ConsentResolved, so the mid-session ConsentRerunDriver's
            // RerunFromAsync(ConsentResolved) — whose rerun set is transitively closed — would hard-reload
            // Main under the player. It would also kill boot with a BootGraphException in any SKU that
            // drops the Ads/Analytics nodes. Add SKU caps here; never framework monetization caps.
            builder.Register(resolver => BootNodeOverride.Replace(
                    "FirstScene",
                    new GatedFirstSceneNode(
                        new FirstSceneNode(
                            resolver.Resolve<ISceneService>(),
                            resolver.Resolve<FirstSceneConfig>(),
                            resolver.TryResolve<ILog>(out var log) ? log : null),
                        resolver.Resolve<LoadingSceneHost>(),
                        GameBootCaps.TestWaitDone)),
                Lifetime.Singleton);
        }
    }
}
