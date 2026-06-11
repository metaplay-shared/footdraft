// FOOTDRAFT — P2 match-sim unit tests (pure, deterministic).

using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class MatchSimTests
    {
        static LineRatings Strong() => new LineRatings { Attack = 92, Midfield = 90, Defence = 90, Goalkeeping = 88, Chemistry = 12 };
        static LineRatings Weak()   => new LineRatings { Attack = 68, Midfield = 66, Defence = 67, Goalkeeping = 65, Chemistry = 0 };
        static LineRatings Even()   => new LineRatings { Attack = 82, Midfield = 82, Defence = 82, Goalkeeping = 80, Chemistry = 4 };

        [Test]
        public void ResolveIsDeterministicForSeed()
        {
            MatchResult a = MatchSim.Resolve(Strong(), Even(), 0xBEEF);
            MatchResult b = MatchSim.Resolve(Strong(), Even(), 0xBEEF);

            Assert.That(a.HomeGoals, Is.EqualTo(b.HomeGoals));
            Assert.That(a.AwayGoals, Is.EqualTo(b.AwayGoals));
            Assert.That(a.Goals.Count, Is.EqualTo(b.Goals.Count));
            for (int i = 0; i < a.Goals.Count; i++)
            {
                Assert.That(a.Goals[i].Minute, Is.EqualTo(b.Goals[i].Minute));
                Assert.That(a.Goals[i].HomeScored, Is.EqualTo(b.Goals[i].HomeScored));
            }
        }

        [Test]
        public void ScorelineMatchesGoalEvents()
        {
            // Sweep seeds; the tallied goals must always equal the per-side event counts, minutes sorted & in range.
            for (ulong seed = 0; seed < 100; seed++)
            {
                MatchResult r = MatchSim.Resolve(Even(), Even(), seed);
                int home = 0, away = 0;
                int lastMinute = 0;
                foreach (GoalEvent g in r.Goals)
                {
                    if (g.HomeScored) home++; else away++;
                    Assert.That(g.Minute, Is.InRange(1, 90));
                    Assert.That(g.Minute, Is.GreaterThanOrEqualTo(lastMinute)); // sorted
                    lastMinute = g.Minute;
                }
                Assert.That(home, Is.EqualTo(r.HomeGoals));
                Assert.That(away, Is.EqualTo(r.AwayGoals));
            }
        }

        [Test]
        public void StrongerTeamWinsTheMajority()
        {
            int homeWins = 0, draws = 0, awayWins = 0;
            for (ulong seed = 0; seed < 300; seed++)
            {
                MatchResult r = MatchSim.Resolve(Strong(), Weak(), seed);
                if (r.HomeWon) homeWins++;
                else if (r.IsDraw) draws++;
                else awayWins++;
            }
            // The much stronger side should win clearly more than it loses (RNG still allows upsets/draws).
            Assert.That(homeWins, Is.GreaterThan(awayWins * 3));
            Assert.That(homeWins, Is.GreaterThan(150)); // > half of 300
        }

        [Test]
        public void EvenMatchupProducesVariety()
        {
            HashSet<string> scorelines = new HashSet<string>();
            int decisive = 0;
            for (ulong seed = 0; seed < 200; seed++)
            {
                MatchResult r = MatchSim.Resolve(Even(), Even(), seed);
                scorelines.Add($"{r.HomeGoals}-{r.AwayGoals}");
                if (!r.IsDraw) decisive++;
            }
            // Not a degenerate sim: many distinct scorelines, and plenty of decisive results.
            Assert.That(scorelines.Count, Is.GreaterThan(5));
            Assert.That(decisive, Is.GreaterThan(40));
        }

        [Test]
        public void GoalsStayInPlausibleRange()
        {
            // No absurd cricket scores: keep an eye on the ceiling across many seeds.
            int maxGoals = 0;
            for (ulong seed = 0; seed < 300; seed++)
            {
                MatchResult r = MatchSim.Resolve(Strong(), Weak(), seed);
                maxGoals = System.Math.Max(maxGoals, r.HomeGoals + r.AwayGoals);
            }
            Assert.That(maxGoals, Is.LessThanOrEqualTo(12));
        }
    }
}
