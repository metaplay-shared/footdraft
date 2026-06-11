// FOOTDRAFT — Phase 6 live-form unit tests.

using NUnit.Framework;

namespace Game.Logic.Tests
{
    /// <summary>
    /// Unit tests for the live "form sync" die-tier bump (the buff/nerf applied to a player's match die when an
    /// operator marks them in-form).
    /// </summary>
    [TestFixture]
    public class FormTests
    {
        [Test]
        public void BumpDieTierClimbsAndClampsUp()
        {
            Assert.That(FormUtil.BumpDieTier(6, 1), Is.EqualTo(8));
            Assert.That(FormUtil.BumpDieTier(8, 1), Is.EqualTo(10));
            Assert.That(FormUtil.BumpDieTier(10, 1), Is.EqualTo(10), "clamped at d10");
            Assert.That(FormUtil.BumpDieTier(6, 2), Is.EqualTo(10));
            Assert.That(FormUtil.BumpDieTier(6, 5), Is.EqualTo(10), "clamped");
        }

        [Test]
        public void BumpDieTierClampsDownAndIgnoresUnknown()
        {
            Assert.That(FormUtil.BumpDieTier(10, -1), Is.EqualTo(8));
            Assert.That(FormUtil.BumpDieTier(8, -1), Is.EqualTo(6));
            Assert.That(FormUtil.BumpDieTier(6, -1), Is.EqualTo(6), "clamped at d6");
            Assert.That(FormUtil.BumpDieTier(10, -9), Is.EqualTo(6));
            Assert.That(FormUtil.BumpDieTier(7, 1), Is.EqualTo(7), "unknown die size unchanged");
        }

        [Test]
        public void ZeroDeltaIsNoOp()
        {
            Assert.That(FormUtil.BumpDieTier(8, 0), Is.EqualTo(8));
        }
    }
}
