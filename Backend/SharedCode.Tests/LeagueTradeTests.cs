// FOOTDRAFT — P2P trade validation tests (the pure rule the LeagueActor enforces on propose + accept).

using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class LeagueTradeTests
    {
        static LegendPlayer P(string name, Position pos, int ovr) =>
            new LegendPlayer(name, name, pos, ovr, "Nation", "Club", Era.E2010s, "2015/16");

        [Test]
        public void LikeForLikeIsAllowed()
        {
            Assert.That(LeagueTradeEngine.ValidatePlayers(P("a", Position.MID, 88), P("b", Position.MID, 84)), Is.Empty);
        }

        [Test]
        public void DifferentPositionsRejected()
        {
            string err = LeagueTradeEngine.ValidatePlayers(P("a", Position.FWD, 88), P("b", Position.DEF, 84));
            Assert.That(err, Is.Not.Empty);
        }

        [Test]
        public void SamePlayerRejected()
        {
            Assert.That(LeagueTradeEngine.ValidatePlayers(P("same", Position.MID, 88), P("same", Position.MID, 88)), Is.Not.Empty);
        }

        [Test]
        public void NullsRejected()
        {
            Assert.That(LeagueTradeEngine.ValidatePlayers(null, P("b", Position.MID, 84)), Is.Not.Empty);
            Assert.That(LeagueTradeEngine.ValidatePlayers(P("a", Position.MID, 88), null), Is.Not.Empty);
        }
    }
}
