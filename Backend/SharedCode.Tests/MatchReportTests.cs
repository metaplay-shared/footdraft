// FOOTDRAFT — match-report generator tests: deterministic (client + server agree), non-empty, and branch-correct.

using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class MatchReportTests
    {
        [Test]
        public void DeterministicForSameInputs()
        {
            string a = MatchReport.Knockout("Brazil", 2, 1, "Messi 23'", "Quarter-final", win: true, champion: false, seed: 42);
            string b = MatchReport.Knockout("Brazil", 2, 1, "Messi 23'", "Quarter-final", win: true, champion: false, seed: 42);
            Assert.That(a, Is.EqualTo(b), "client + server must generate the identical report");
            Assert.That(a, Is.Not.Empty);
        }

        [Test]
        public void ChampionReportMentionsTheTrophy()
        {
            string r = MatchReport.Knockout("France", 3, 0, "", "Final", win: true, champion: true, seed: 7);
            Assert.That(r.ToLowerInvariant(), Does.Contain("champ").Or.Contain("trophy").Or.Contain("cup"));
        }

        [Test]
        public void WinAndLossReadDifferently()
        {
            string win = MatchReport.Knockout("Spain", 2, 1, "", "Semi-final", win: true, champion: false, seed: 5);
            string loss = MatchReport.Knockout("Spain", 1, 2, "", "Semi-final", win: false, champion: false, seed: 5);
            Assert.That(win, Is.Not.EqualTo(loss));
            Assert.That(win, Does.Contain("Spain"));
            Assert.That(loss, Does.Contain("Spain"));
        }

        [Test]
        public void ScorersAppearWhenProvided()
        {
            string r = MatchReport.Knockout("Italy", 2, 0, "Kane 12', Saka 60'", "Round of 16", win: true, champion: false, seed: 9);
            Assert.That(r, Does.Contain("Kane"));
        }

        [Test]
        public void FixtureReportCoversAllOutcomes()
        {
            Assert.That(MatchReport.Fixture("Rovers", 3, 0, "", 1), Is.Not.Empty);
            Assert.That(MatchReport.Fixture("Rovers", 1, 1, "", 2), Does.Contain("Rovers"));
            Assert.That(MatchReport.Fixture("Rovers", 0, 2, "", 3), Is.Not.Empty);
        }
    }
}
