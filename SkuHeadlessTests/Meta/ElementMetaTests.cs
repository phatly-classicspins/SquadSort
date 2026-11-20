using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>Element popups once each (Hidden at level 8, House at level 20 — GDD §2.2b), and the Win progress bar.</summary>
    public class ElementMetaTests
    {
        TestStore store; EconomyTuning tuning; LevelProgressService svc;

        [SetUp]
        public void SetUp()
        {
            store = new TestStore(); tuning = new EconomyTuning();
            svc = new LevelProgressService(store, new TestWallet(), tuning, new TestFacts());
        }

        [Test]
        public void Each_element_is_introduced_once_at_its_level()
        {
            Assert.That(svc.UnlockElementsFor(7), Is.Empty);
            Assert.That(svc.UnlockElementsFor(8), Is.EqualTo(new[] { ElementId.Hidden }));
            Assert.That(svc.UnlockElementsFor(8), Is.Empty, "replaying level 8 shows no popup");
            Assert.That(svc.UnlockElementsFor(20), Is.EqualTo(new[] { ElementId.House }));
        }

        [Test]
        public void An_older_save_past_the_levels_catches_up_without_popups()
        {
            Assert.That(svc.UnlockElementsFor(14), Is.Empty);
            Assert.That(svc.IsUnlocked(ElementId.Hidden), Is.True);
            Assert.That(svc.IsUnlocked(ElementId.House), Is.False);
        }

        [Test]
        public void A_failed_save_rolls_back_and_the_popup_comes_again()
        {
            store.Fail = true;
            Assert.That(svc.UnlockElementsFor(8), Is.Empty);
            Assert.That(svc.IsUnlocked(ElementId.Hidden), Is.False);
            store.Fail = false;
            Assert.That(svc.UnlockElementsFor(8), Is.EqualTo(new[] { ElementId.Hidden }));
        }

        [TestCase(1, 1, 7, ElementId.Hidden)]
        [TestCase(7, 7, 7, ElementId.Hidden)]
        [TestCase(8, 1, 12, ElementId.House)]
        [TestCase(19, 12, 12, ElementId.House)]
        public void Progress_bar_follows_the_gdd_formula(int won, int current, int total, ElementId next)
        {
            Assert.That(tuning.ElementProgress(won, out int c, out int t, out var n), Is.True);
            Assert.That((c, t, n), Is.EqualTo((current, total, next)));
        }

        [TestCase(20)]
        [TestCase(30)]
        public void Progress_bar_is_hidden_once_every_element_is_out(int won) =>
            Assert.That(tuning.ElementProgress(won, out _, out _, out _), Is.False);
    }
}
