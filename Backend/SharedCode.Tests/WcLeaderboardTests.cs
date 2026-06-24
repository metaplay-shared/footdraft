// FOOTDRAFT — World Cup leaderboard ranking tests (the pure rule the actor + client share).

using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class WcLeaderboardTests
    {
        static WcLeaderboardEntry E(string name, int titles, int round, int ovr, int runs)
            => new WcLeaderboardEntry(name, titles, round, ovr, runs);

        [Test]
        public void RanksByTitlesThenRoundThenOvr()
        {
            List<WcLeaderboardEntry> list = new List<WcLeaderboardEntry> { E("a", 0, 4, 90, 5), E("b", 2, 1, 80, 3), E("c", 2, 4, 85, 2) };
            WcLeaderboard.Rank(list);
            Assert.That(list[0].Name, Is.EqualTo("c"), "2 titles + deepest round");
            Assert.That(list[1].Name, Is.EqualTo("b"), "2 titles, shallower round");
            Assert.That(list[2].Name, Is.EqualTo("a"), "no titles");
            Assert.That(list[0].Rank, Is.EqualTo(1));
            Assert.That(list[2].Rank, Is.EqualTo(3));
        }

        [Test]
        public void FewerRunsWinsTheTieBreak()
        {
            List<WcLeaderboardEntry> list = new List<WcLeaderboardEntry> { E("grinder", 1, 4, 88, 9), E("natural", 1, 4, 88, 2) };
            WcLeaderboard.Rank(list);
            Assert.That(list[0].Name, Is.EqualTo("natural"), "same titles/round/OVR → fewer runs ranks higher");
        }

        [Test]
        public void HigherBestXiBreaksRoundTie()
        {
            List<WcLeaderboardEntry> list = new List<WcLeaderboardEntry> { E("weak", 0, 2, 78, 1), E("strong", 0, 2, 90, 1) };
            WcLeaderboard.Rank(list);
            Assert.That(list[0].Name, Is.EqualTo("strong"));
        }
    }
}
