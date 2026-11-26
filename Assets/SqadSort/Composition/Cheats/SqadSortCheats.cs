using System;
using System.Collections.Generic;
using ClassicSpins.Cheats;
using ClassicSpins.PrototypeFramework.Application;
using ClassicSpins.PrototypeFramework.Presentation;
using Cysharp.Threading.Tasks;
using Game.Application;
using Game.Gen;
using Game.Presentation;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Game.Composition
{
    /// <summary>
    /// CardSlot's cheats (D-048), ported: SqadSort's tester menu (<c>com.classicspins.cheats</c>). Only Game.Composition sees the
    /// package. Everything is registered only where <see cref="CheatMenu.IsAvailable"/> (Editor, development build, or the
    /// <c>CS_CHEATS</c> define); <see cref="GameplayCheatHook"/> is always there because the Gameplay screen takes it.
    /// </summary>
    public static class CheatsInstaller
    {
        public static void Install(IContainerBuilder builder)
        {
            builder.Register<GameplayCheatHook>(Lifetime.Singleton);
            if (!CheatMenu.IsAvailable) return;
            builder.Register<ProgressCheats>(Lifetime.Singleton);
            builder.RegisterEntryPoint<SqadSortCheats>(Lifetime.Singleton);
        }
    }

    /// <summary>Builds the menu once at start-up (Root scope, lives as long as the app) and removes it on dispose.</summary>
    public sealed class SqadSortCheats : IStartable, IDisposable
    {
        private const int DefaultCoins = 1000, DefaultBoosters = 5;

        private readonly ProgressCheats _cheats;
        private readonly LevelProgressService _progress;
        private readonly GameplayCheatHook _gameplay;
        private readonly ISceneService _scenes;
        private readonly ILevelCatalog _levels;
        private readonly IClock _clock;
        private readonly EconomyTuning _tuning;
        private readonly ILog _log;
        private readonly Game.Infrastructure.UnityNetworkStatus _network;   // D-063
        private readonly IPurchaseService _purchases;                      // D-067: the Editor fake can be told to fail
        private readonly RemoveAdsPopup _removeAds;                        // D-067: show the Remove Ads popup on demand
        private readonly IReadOnlyList<IDevTool> _devTools;                // D-125: SDK debuggers (MAX Mediation / Creative)
        private readonly CheatRegistry _registry = new CheatRegistry();
        private CheatMenuView _menu;

        public SqadSortCheats(ProgressCheats cheats, LevelProgressService progress, GameplayCheatHook gameplay, ISceneService scenes,
            ILevelCatalog levels, IClock clock, EconomyTuning tuning, ILog log, Game.Infrastructure.UnityNetworkStatus network, IPurchaseService purchases,
            RemoveAdsPopup removeAds, IObjectResolver resolver)
        {
            _cheats = cheats; _progress = progress; _gameplay = gameplay; _scenes = scenes; _levels = levels; _clock = clock;
            _tuning = tuning; _log = log; _network = network; _purchases = purchases; _removeAds = removeAds;
            // no SDK module installed ⇒ nothing registered as IDevTool, and VContainer throws on an empty collection
            _devTools = resolver.TryResolve<IReadOnlyList<IDevTool>>(out var tools) ? tools : Array.Empty<IDevTool>();
        }

        private static string DevToolLabel(string id) => id switch
        {
            "ads-max.mediation-debugger" => "MAX Mediation Debugger",
            "ads-max.creative-debugger" => "MAX Creative Debugger",
            _ => id,
        };

        public void Start()
        {
            _registry.Group("State").Text(Summary);

            _registry.Group("Level")
                .Button("Win", () => Report(_gameplay.Win(), "Win"))
                .Button("Lose", () => Report(_gameplay.Lose(), "Lose"))
                .Number("Go to level", () => Math.Max(1, _progress.CurrentLevel), level => GoToLevel(level).Forget(), "Play")
                .Button("Reload Home", () => _scenes.LoadAsync(SceneKeys.Main, new MainParam(ColdBoot: false), SceneTransition.Replace).Forget())
                .Button("Skip ready", () => Changed(_cheats.PrimeSkip(_progress.CurrentLevel),
                    $"Level {_progress.CurrentLevel}: the next loss offers Skip Level (D-064)"));

            _registry.Group("Economy")
                .Number("+ coins", DefaultCoins, n => Changed(_cheats.AddCoins(n), $"+{n} coins"), "Add")
                .Number("+ each booster", DefaultBoosters, AddBoosters, "Add")
                .Button("Unlock all", () => Changed(_cheats.UnlockAll(), "Boosters and elements unlocked"));

            _registry.Group("Lives")
                .Button("Refill lives", () => Changed(_cheats.SetLives(_tuning.LivesMax, _clock.UtcNow), "Lives full"))
                .Button("Empty lives", () => Changed(_cheats.SetLives(0, _clock.UtcNow), "Lives 0 — the refill clock started now"));

            // D-063: try the No Internet popup without turning the device's network off
            _registry.Group("Network")
                .Toggle("Force offline", () => _network.ForceOffline, on => { _network.ForceOffline = on; _log.Info($"[Cheats] force offline {(on ? "on" : "off")}"); });

            // D-067: try Purchase failed and the Starter Pack popup without a real store. D-137: the offer buttons stay with the
            // real store (Unity IAP); only "Purchases fail" needs the Editor's fake.
            var shop = _registry.Group("Shop");
            if (_purchases is FakePurchaseService fake)
                shop.Toggle("Purchases fail", () => fake.ForcedOutcome == PurchaseResult.Failed,
                    on => { fake.ForcedOutcome = on ? PurchaseResult.Failed : (PurchaseResult?)null; _log.Info($"[Cheats] purchases fail {(on ? "on" : "off")}"); });
            shop.Button("Starter Pack again", () => Changed(_cheats.ResetStarterOffer(), $"Starter Pack opens on Home at level {_tuning.StarterPackUnlockLevel}+"))
                    .Button("Remove Ads popup", () => _removeAds.ShowAsync(System.Threading.CancellationToken.None).Forget())
                    .Button("Gold Brick ready", () => Changed(_cheats.GoldBrickReady(), $"Gold Brick Package! on the next level complete (level {_tuning.GoldBrickUnlockLevel}+) or app start"));

            // D-125 (Chipsaway feedback 10, 11): the SDK's own debuggers (MAX Mediation Debugger to check every network) and
            // a native crash, to see it reach Firebase Crashlytics (reopen the app: the report is sent on the next launch)
            var sdk = _registry.Group("SDK");
            foreach (var tool in _devTools)
            {
                var t = tool;
                sdk.Button(DevToolLabel(t.Id), () => { if (!t.Open()) _log.Warn($"[Cheats] {t.Id} is not available yet (SDK not initialized?)"); });
            }
            sdk.Button("Test crash (Crashlytics)", () =>
            {
                _log.Warn("[Cheats] forcing a native crash for Crashlytics (D-125)");
                UnityEngine.Diagnostics.Utils.ForceCrash(UnityEngine.Diagnostics.ForcedCrashCategory.Abort);
            });

            _registry.Group("Save")
                .Button("Reset tutorial", () => Changed(_cheats.ResetTutorial(), "Tutorial and terms will show again on level 1"))
                .Button("Reset rating", () => Changed(_cheats.ResetRating(), $"Rating prompt comes again after a win at level {_tuning.RatingPromptLevel}+"))
                .Button("Reset all", ResetAll);

            _menu = CheatMenu.Create(_registry);
            _log.Info("[Cheats] tester menu ready (D-048).");
        }

        public void Dispose()
        {
            if (_menu != null) Object.Destroy(_menu.gameObject);
            _menu = null;
        }

        private string Summary()
        {
            var now = _clock.UtcNow;
            int lives = _progress.Lives(now);
            string refill = lives < _tuning.LivesMax ? $" (next in {_progress.NextLifeIn(now):mm\\:ss})" : string.Empty;
            return $"Level {_progress.CurrentLevel} · cleared {_progress.HighestCleared} · {(_gameplay.InGameplay ? "in gameplay" : "home")}\n" +
                   $"Coins {_progress.Coins} · Lives {lives}/{_tuning.LivesMax}{refill}\n" +
                   $"Undo {_progress.Boosters(Booster.Undo)} · Shuffle {_progress.Boosters(Booster.Shuffle)} · Remove {_progress.Boosters(Booster.Remove)}";
        }

        private async UniTaskVoid GoToLevel(int level)
        {
            await UniTask.Yield();
            int count = _levels.Count;
            int set = _cheats.SetLevel(level, count);
            _cheats.SetLives(_tuning.LivesMax, _clock.UtcNow);   // a jump abandons the attempt; like Level Jump, it must not cost lives
            _registry.Status($"Level {set} of {count}");
            _log.Info($"[Cheats] go to level {set}.");
            await _scenes.LoadAsync(SceneKeys.Gameplay, new GameplayParam(set), SceneTransition.Replace);
        }

        private void AddBoosters(int n)
        {
            bool ok = true;
            foreach (Booster id in Enum.GetValues(typeof(Booster))) ok &= _cheats.AddBooster(id, n);
            Changed(ok, $"+{n} of each booster");
        }

        private void ResetAll()
        {
            if (!_cheats.ResetAll()) { _registry.Status("Reset failed: the save could not be written"); return; }
            _registry.Status("Progress reset — back to Home");
            _log.Info("[Cheats] all progress reset.");
            _scenes.LoadAsync(SceneKeys.Main, new MainParam(ColdBoot: true), SceneTransition.Replace).Forget();
        }

        // the save changed under the screen: Gameplay redraws in place; Home keeps its pills until "Reload Home" (reloading
        // it from here would race a "Go to level" load started right after)
        private void Changed(bool ok, string message)
        {
            if (!ok) { _registry.Status("Failed: the save could not be written"); return; }
            if (_gameplay.InGameplay) { _gameplay.Refresh(); _registry.Status(message); }
            else _registry.Status(message + " (Reload Home to see it)");
        }

        private void Report(bool ok, string what) =>
            _registry.Status(ok ? what : $"{what}: only during play, with no dialog open");
    }
}
