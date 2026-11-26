using System;
using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    public class RemoteConfigCompletionTests
    {
        [TestCase(40,40)][TestCase(41,21)][TestCase(42,22)][TestCase(60,40)][TestCase(61,21)]
        public void Display_progress_maps_to_loop_content(int shown,int content)
            => Assert.That(new EconomyTuning().ContentLevel(shown,40),Is.EqualTo(content));

        [Test] public void Newly_shipped_files_override_the_old_loop()
            => Assert.That(new EconomyTuning().ContentLevel(45,50),Is.EqualTo(45));

        [Test] public void Remote_loop_range_is_used_and_invalid_ranges_do_not_loop()
        {
            var t=new EconomyTuning {LevelLoopLastLevel=10,LevelLoopStartLevel=6};
            Assert.That(t.ContentLevel(11,10),Is.EqualTo(6));
            Assert.That(t.ContentLevel(16,10),Is.EqualTo(6));
            t.LevelLoopStartLevel=11;
            Assert.That(t.CanLoopLevels(10),Is.False);
            Assert.That(t.ContentLevel(11,10),Is.EqualTo(10));
        }

        [Test] public void Wins_and_skips_keep_display_progress_after_the_catalog()
        {
            var store=new TestStore();
            var service=new LevelProgressService(store,new TestWallet(),new EconomyTuning(),new TestFacts());
            Assert.That(service.CompleteLevel(40,40).NextLevel,Is.EqualTo(41));
            Assert.That(service.CompleteLevel(41,40).NextLevel,Is.EqualTo(42));
            Assert.That(service.HomeLevel(40),Is.EqualTo(42));
            Assert.That(service.SkipLevel(42,40,DateTimeOffset.UnixEpoch).NextLevel,Is.EqualTo(43));
            Assert.That(store.Progress.HighestCleared,Is.EqualTo(42));
        }

        [Test] public void Retry_ad_cap_is_persisted_resets_by_local_day_and_rolls_back()
        {
            var store=new TestStore();var tuning=new EconomyTuning {RetryBoosterMaxPerDay=2};
            var svc=new LevelProgressService(store,new TestWallet(),tuning,new TestFacts());
            var now=new DateTimeOffset(2026,11,12,12,0,0,TimeSpan.FromHours(7));
            Assert.That(svc.GrantRestartBoosterFromAd(now),Is.True);
            store.Progress.CopyFrom(store.Progress.Clone());
            store.Fail=true;
            Assert.That(svc.GrantRestartBoosterFromAd(now),Is.False);
            Assert.That(svc.RetryBoostersLeft(now),Is.EqualTo(1));
            Assert.That(svc.Boosters(Booster.Remove),Is.EqualTo(1));
            store.Fail=false;
            Assert.That(svc.GrantRestartBoosterFromAd(now),Is.True);
            Assert.That(svc.GrantRestartBoosterFromAd(now),Is.False);
            Assert.That(svc.RetryBoostersLeft(now.AddDays(1)),Is.EqualTo(2));
            Assert.That(svc.GrantRestartBoosterFromAd(now.AddDays(1)),Is.True);
        }

        [Test] public void Revive_ad_limit_is_separate_from_paid_revives()
        {
            var t=new EconomyTuning {ReviveMaxAdsPerAttempt=1};
            Assert.That(t.CanRewardedRevive(0),Is.True);
            Assert.That(t.CanRewardedRevive(1),Is.False);
            t.ReviveMaxAdsPerAttempt=2;
            Assert.That(t.CanRewardedRevive(1),Is.True);
            t.ReviveMaxAdsPerAttempt=0;
            Assert.That(t.CanRewardedRevive(0),Is.False);
        }
    }
}
