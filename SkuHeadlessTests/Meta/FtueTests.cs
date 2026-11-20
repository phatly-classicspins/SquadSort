using Game.Application;
using NUnit.Framework;

namespace SqadSort.SkuHeadlessTests.Meta
{
    /// <summary>features/ftue.md: the level 1 guide is saved once done. (The terms dialog it shared a file with is gone,
    /// Phat 07-11-2026.)</summary>
    public class FtueTests
    {
        TestStore store; LevelProgressService svc;

        [SetUp]
        public void SetUp()
        {
            store = new TestStore();
            svc = new LevelProgressService(store, new TestWallet(), new EconomyTuning(), new TestFacts());
        }

        [Test]
        public void The_level_1_tutorial_is_saved_once_done()
        {
            Assert.That(svc.IsFtueDone("ftue.l1.done"), Is.False);
            svc.MarkFtueDone("ftue.l1.done");
            Assert.That(svc.IsFtueDone("ftue.l1.done"), Is.True);
        }
    }
}
