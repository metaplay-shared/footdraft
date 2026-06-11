// FOOTDRAFT — Phase 5 season pass / ranked-ladder unit tests.

using Metaplay.Core;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    /// <summary>
    /// Unit tests for the Phase 5 season meta: the Season Pass tier curve, ranked-division thresholds, and the
    /// monthly season window rollover.
    /// </summary>
    [TestFixture]
    public class SeasonTests
    {
        static MetaTime At(long ms) => MetaTime.FromMillisecondsSinceEpoch(ms);

        [Test]
        public void PassTierFromXpCapsAtTrackLength()
        {
            GlobalConfig g = new GlobalConfig();
            Assert.That(g.PassTier(0), Is.EqualTo(0));
            Assert.That(g.PassTier(g.PassXpPerTier), Is.EqualTo(1));
            Assert.That(g.PassTier(g.PassXpPerTier * 3 + 5), Is.EqualTo(3));
            Assert.That(g.PassTier((long)g.PassXpPerTier * 1000), Is.EqualTo(g.PassFreeRewards.Length), "capped at track length");
            Assert.That(g.PassPremiumRewards.Length, Is.GreaterThan(0));
        }

        [Test]
        public void DivisionIndexClimbsWithPoints()
        {
            GlobalConfig g = new GlobalConfig();
            Assert.That(g.DivisionIndex(0), Is.EqualTo(0), "Bronze at 0");
            Assert.That(g.DivisionIndex(g.RankDivisions[1].MinPoints), Is.EqualTo(1));
            Assert.That(g.DivisionIndex(int.MaxValue), Is.EqualTo(g.RankDivisions.Length - 1), "top division");
            // Monotonic non-decreasing.
            int prev = 0;
            for (int p = 0; p <= 2000; p += 50)
            {
                int d = g.DivisionIndex(p);
                Assert.That(d, Is.GreaterThanOrEqualTo(prev));
                prev = d;
            }
        }

        [Test]
        public void SeasonRollsOverMonthly()
        {
            GlobalConfig g = new GlobalConfig();
            long windowMs = (long)g.SeasonWindowDays * 24L * 60L * 60L * 1000L;
            long s0 = SeasonSchedule.CurrentSeasonId(At(windowMs * 2 + 10), g);
            long s1 = SeasonSchedule.CurrentSeasonId(At(windowMs * 3 + 10), g);
            Assert.That(s1, Is.EqualTo(s0 + 1));
            Assert.That(SeasonSchedule.SeasonEndsAt(s0, g).MillisecondsSinceEpoch, Is.EqualTo(windowMs * 3));
        }

        [Test]
        public void ShopHasProducts()
        {
            GlobalConfig g = new GlobalConfig();
            Assert.That(g.ShopProducts.Length, Is.GreaterThan(0));
            foreach (ShopProduct p in g.ShopProducts)
                Assert.That(p.Gems, Is.GreaterThan(0));
        }
    }
}
