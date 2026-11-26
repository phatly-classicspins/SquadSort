using System;
using Game.Application;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>
    /// Old save shapes, loaded the way the framework's <c>FileUserData</c> loads them (<c>JsonConvert.PopulateObject</c>
    /// into the live default model). Every <see cref="ProgressModel"/> change so far has been additive with a safe
    /// default, which needs no migrator (the framework's WalletModel precedent; <c>SaveSchema.CurrentVersion</c> stays 1).
    /// These fixtures are what proves it: a field renamed, removed or given a different meaning reddens one of them, and
    /// that change needs a real migrator — which means bumping the framework's <c>SaveSchema</c> (a spine change, rule #2).
    /// The fixture JSON is frozen: never edit one, add a new one for a new shape.
    /// </summary>
    public class SaveFixtureTests
    {
        // step 5 (5caed5c): the first shipped shape, with the old Undo / AddSlot boosters
        const string Step5 = @"{
            ""CurrentLevel"": 7, ""HighestCleared"": 6, ""Attempts"": 2, ""StartCoinsGranted"": true,
            ""UnlockedBoosters"": [""Undo""], ""FtueCompleted"": [""ftue.l1""], ""WinsSinceInterstitial"": 1 }";

        // CR-012 D (df0fb73): boosters and elements, but no lives or terms yet
        const string BeforeLives = @"{
            ""CurrentLevel"": 13, ""HighestCleared"": 12, ""Attempts"": 0, ""FtueCompleted"": [], ""WinsSinceInterstitial"": 0,
            ""StartCoinsGranted"": true, ""BoosterUndo"": 2, ""BoosterShuffle"": 0, ""BoosterAddContainer"": 1,
            ""BoostersUnlocked"": [""booster_undo"", ""booster_shuffle"", ""booster_add_container""],
            ""ElementsUnlocked"": [""element_hidden"", ""element_house""] }";

        static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        static (TestStore store, LevelProgressService svc) Load(string json)
        {
            var store = new TestStore();
            JsonConvert.PopulateObject(json, store.Progress);   // exactly FileUserData.ApplyPayload
            return (store, new LevelProgressService(store, new TestWallet(), new EconomyTuning(), new TestFacts()));
        }

        [Test]
        public void A_step_5_save_keeps_its_progress_and_catches_up_on_boosters()
        {
            var (store, svc) = Load(Step5);
            var p = store.Progress;
            Assert.That((p.CurrentLevel, p.HighestCleared, p.Attempts, p.WinsSinceInterstitial), Is.EqualTo((7, 6, 2, 1)));
            Assert.That(p.FtueCompleted, Is.EqualTo(new[] { "ftue.l1" }), "a list is filled, never doubled");
            Assert.That(p.BoostersUnlocked, Is.Empty, "the old Undo / AddSlot unlocks are not today's boosters and are dropped");
            Assert.That(svc.EnsureStartCoins(), Is.True);
            Assert.That(svc.Coins, Is.EqualTo(0), "the start coins were already granted back then (the wallet kept them)");
            Assert.That(svc.UnlockBoostersFor(p.CurrentLevel), Is.EqualTo(new[] { Booster.Undo }), "level 7: Hand is caught up");
            Assert.That(svc.Boosters(Booster.Undo), Is.EqualTo(3), "with its unlock gift, once");
            Assert.That(svc.Lives(Now), Is.EqualTo(5), "no lives field yet: the save reads as full");
        }

        [Test]
        public void A_save_from_before_lives_reads_as_full_and_keeps_its_boosters()
        {
            var (store, svc) = Load(BeforeLives);
            Assert.That(svc.Lives(Now), Is.EqualTo(5));
            Assert.That(svc.NextLifeIn(Now), Is.EqualTo(TimeSpan.Zero));
            Assert.That((svc.Boosters(Booster.Undo), svc.Boosters(Booster.Remove)), Is.EqualTo((2, 1)));
            Assert.That(svc.UnlockBoostersFor(13), Is.Empty, "already unlocked: no second gift");
            Assert.That(store.Progress.ElementsUnlocked, Has.Count.EqualTo(2));
        }

        // pause-settings.md: each toggle is saved and reads back after a restart; an install with no settings yet hears everything
        [Test]
        public void Settings_round_trip_and_a_missing_file_means_everything_on()
        {
            var saved = JObject.FromObject(new SettingsModel { Sound = false, Music = true, Haptics = false });   // FileUserData.BuildPayload
            var loaded = new SettingsModel();
            JsonConvert.PopulateObject(saved.ToString(), loaded);                                               // FileUserData.ApplyPayload
            Assert.That((loaded.Sound, loaded.Music, loaded.Haptics), Is.EqualTo((false, true, false)));
            var fresh = new SettingsModel();
            JsonConvert.PopulateObject("{}", fresh);
            Assert.That((fresh.Sound, fresh.Music, fresh.Haptics), Is.EqualTo((true, true, true)));
        }

        [Test]
        public void Todays_shape_round_trips_through_the_save_envelope()
        {
            var p = new ProgressModel
            {
                CurrentLevel = 18, HighestCleared = 17, Attempts = 3, WinsSinceInterstitial = 1, StartCoinsGranted = true,
                BoosterUndo = 1, BoosterShuffle = 2, BoosterAddContainer = 3, Lives = 2, LivesRefillFromUtc = 1_799_999_000,
            };
            p.FtueCompleted.Add("ftue.l1.done");
            p.BoostersUnlocked.Add("booster_undo");
            p.ElementsUnlocked.Add("element_locked");
            var saved = JObject.FromObject(p);                  // exactly FileUserData.BuildPayload
            var (store, _) = Load(saved.ToString());
            Assert.That(JToken.DeepEquals(JObject.FromObject(store.Progress), saved), Is.True,
                "every field survives a save and a load (a field the serializer cannot write would be lost)");
        }
    }
}
