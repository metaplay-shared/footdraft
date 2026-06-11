// FOOTDRAFT — Phase 7 bracket & cosmetics unit tests.

using NUnit.Framework;

namespace Game.Logic.Tests
{
    /// <summary>
    /// Unit tests for the Phase 7 marquee Bracket Cup round resolution (determinism + strength bias) and the
    /// cosmetic catalog defaults.
    /// </summary>
    [TestFixture]
    public class BracketTests
    {
        [Test]
        public void RoundResolutionIsDeterministic()
        {
            GlobalConfig g = new GlobalConfig();
            for (ulong seed = 1; seed <= 5; seed++)
            {
                bool a = BracketCup.ResolveRound(46, 0, seed, g);
                bool b = BracketCup.ResolveRound(46, 0, seed, g);
                Assert.That(a, Is.EqualTo(b), "same inputs must reproduce the same result");
            }
        }

        [Test]
        public void StrongerSquadWinsMoreBracketRounds()
        {
            GlobalConfig g = new GlobalConfig();
            int strongWins = 0, weakWins = 0;
            const int samples = 200;
            for (ulong seed = 1; seed <= samples; seed++)
            {
                if (BracketCup.ResolveRound(50, 0, seed, g)) strongWins++;
                if (BracketCup.ResolveRound(28, 0, seed, g)) weakWins++;
            }
            Assert.That(strongWins, Is.GreaterThan(weakWins),
                $"a stronger squad should win more bracket rounds (strong {strongWins}, weak {weakWins})");
        }

        [Test]
        public void BracketRewardsCoverEveryRound()
        {
            GlobalConfig g = new GlobalConfig();
            Assert.That(BracketCup.RoundsTotal, Is.EqualTo(4));
            Assert.That(g.BracketRoundRewards.Length, Is.EqualTo(BracketCup.RoundsTotal));
            Assert.That(BracketCup.RoundName(0), Is.EqualTo("Round of 16"));
            Assert.That(BracketCup.RoundName(3), Is.EqualTo("Final"));
        }

        [Test]
        public void CosmeticCatalogHasFreeDefaults()
        {
            CosmeticItem[] items = CosmeticContent.Items;
            Assert.That(items.Length, Is.GreaterThan(0));

            CosmeticItem avatar = System.Array.Find(items, c => c.Id == "avatar_default");
            CosmeticItem dice   = System.Array.Find(items, c => c.Id == "dice_default");
            Assert.That(avatar, Is.Not.Null);
            Assert.That(dice, Is.Not.Null);
            Assert.That(avatar.GemCost, Is.EqualTo(0), "default avatar is free");
            Assert.That(dice.GemCost, Is.EqualTo(0), "default dice skin is free");
        }
    }
}
