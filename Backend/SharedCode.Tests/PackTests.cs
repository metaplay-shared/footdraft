// FOOTDRAFT — Scout Pack roll tests (pre-draft boost model): determinism, coin ranges, and the fixed
// reroll / elite-spin payout per tier. Pure — no game-config archive needed.

using System.Linq;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class PackTests
    {
        static readonly GlobalConfig G = new GlobalConfig();
        static PackDef Pack(string id) => G.Packs.First(p => p.Id == id);

        [Test]
        public void EveryTierIsConfiguredWithOneFreeDaily()
        {
            Assert.That(G.Packs.Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(G.Packs.Count(p => p.IsFreeDaily), Is.EqualTo(1), "exactly one free daily pack");
        }

        [Test]
        public void RollIsDeterministicForSameSeed()
        {
            PackDef wc = Pack("wc");
            ulong seed = PackEngine.SeedFor(3, wc.Id);
            PackReward a = PackEngine.Roll(wc, seed);
            PackReward b = PackEngine.Roll(wc, seed);
            Assert.That(a.Coins, Is.EqualTo(b.Coins));
            Assert.That(a.Rerolls, Is.EqualTo(b.Rerolls));
            Assert.That(a.EliteSpins, Is.EqualTo(b.EliteSpins));
        }

        [Test]
        public void SeedVariesByOpenCountAndPack()
        {
            Assert.That(PackEngine.SeedFor(0, "wc"), Is.Not.EqualTo(PackEngine.SeedFor(1, "wc")));
            Assert.That(PackEngine.SeedFor(0, "wc"), Is.Not.EqualTo(PackEngine.SeedFor(0, "gold")));
        }

        [Test]
        public void RewardMatchesTierRangesAndBoosts()
        {
            foreach (PackDef pack in G.Packs)
            {
                for (int s = 0; s < 100; s++)
                {
                    PackReward r = PackEngine.Roll(pack, PackEngine.SeedFor(s, pack.Id));
                    Assert.That(r.Coins, Is.InRange(pack.CoinsMin - 10, pack.CoinsMax), $"{pack.Id} coins");
                    Assert.That(r.Rerolls, Is.EqualTo(pack.Rerolls), $"{pack.Id} rerolls");
                    Assert.That(r.EliteSpins, Is.EqualTo(pack.EliteSpins), $"{pack.Id} elite spins");
                }
            }
        }

        [Test]
        public void BetterPacksGiveMoreBoosts()
        {
            PackDef daily = Pack("daily"), wc = Pack("wc");
            Assert.That(wc.Rerolls, Is.GreaterThan(daily.Rerolls));
            Assert.That(wc.EliteSpins, Is.GreaterThan(daily.EliteSpins));
            Assert.That(wc.CoinsMax, Is.GreaterThan(daily.CoinsMax));
        }
    }
}
