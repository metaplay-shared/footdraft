// FOOTDRAFT — P4 league engine tests: double round-robin fixtures + standings table.

using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class LeagueTests
    {
        // Validates the core round-robin properties for any team count.
        static void AssertValidDoubleRoundRobin(int teamCount)
        {
            List<List<LeagueFixture>> schedule = LeagueEngine.GenerateDoubleRoundRobin(teamCount);

            Assert.That(schedule.Count, Is.EqualTo(LeagueEngine.MatchdayCount(teamCount)), "matchday count");

            int totalFixtures = 0;
            // pairKey -> count, and home-tally per ordered direction
            Dictionary<(int, int), int> pairMeetings = new Dictionary<(int, int), int>();
            Dictionary<(int, int), int> directed     = new Dictionary<(int, int), int>();

            foreach (List<LeagueFixture> matchday in schedule)
            {
                HashSet<int> seen = new HashSet<int>();
                foreach (LeagueFixture f in matchday)
                {
                    Assert.That(f.HomeIndex, Is.InRange(0, teamCount - 1));
                    Assert.That(f.AwayIndex, Is.InRange(0, teamCount - 1));
                    Assert.That(f.HomeIndex, Is.Not.EqualTo(f.AwayIndex));
                    // No team is double-booked within a matchday.
                    Assert.That(seen.Add(f.HomeIndex), Is.True, "home double-booked");
                    Assert.That(seen.Add(f.AwayIndex), Is.True, "away double-booked");

                    int lo = System.Math.Min(f.HomeIndex, f.AwayIndex);
                    int hi = System.Math.Max(f.HomeIndex, f.AwayIndex);
                    pairMeetings[(lo, hi)] = pairMeetings.TryGetValue((lo, hi), out int c) ? c + 1 : 1;
                    directed[(f.HomeIndex, f.AwayIndex)] = directed.TryGetValue((f.HomeIndex, f.AwayIndex), out int d) ? d + 1 : 1;
                    totalFixtures++;
                }
            }

            // Every unordered pair meets exactly twice, once with each team at home.
            Assert.That(totalFixtures, Is.EqualTo(teamCount * (teamCount - 1)), "total fixtures");
            Assert.That(pairMeetings.Count, Is.EqualTo(teamCount * (teamCount - 1) / 2), "distinct pairs");
            foreach (KeyValuePair<(int, int), int> kv in pairMeetings)
            {
                Assert.That(kv.Value, Is.EqualTo(2), "each pair meets twice");
                Assert.That(directed[(kv.Key.Item1, kv.Key.Item2)], Is.EqualTo(1), "team A hosts once");
                Assert.That(directed[(kv.Key.Item2, kv.Key.Item1)], Is.EqualTo(1), "team B hosts once");
            }
        }

        [Test]
        public void TwentyTeamsGiveThirtyEightMatchdays()
        {
            Assert.That(LeagueEngine.MatchdayCount(20), Is.EqualTo(38)); // the literal "38-0"
            AssertValidDoubleRoundRobin(20);
            List<List<LeagueFixture>> schedule = LeagueEngine.GenerateDoubleRoundRobin(20);
            foreach (List<LeagueFixture> md in schedule)
                Assert.That(md.Count, Is.EqualTo(10)); // 20 teams → 10 fixtures per matchday
        }

        [Test]
        public void EvenAndOddCountsAreValid()
        {
            AssertValidDoubleRoundRobin(2);
            AssertValidDoubleRoundRobin(5);   // odd → byes
            AssertValidDoubleRoundRobin(8);
            AssertValidDoubleRoundRobin(11);  // odd
            AssertValidDoubleRoundRobin(16);
        }

        [Test]
        public void TableTalliesPointsAndSorts()
        {
            // 3 teams, a mini double round-robin played out by hand.
            List<LeagueResult> results = new List<LeagueResult>
            {
                new LeagueResult(0, 1, 2, 0), // 0 beats 1
                new LeagueResult(0, 2, 1, 1), // 0 draws 2
                new LeagueResult(1, 2, 0, 3), // 2 beats 1
                new LeagueResult(1, 0, 1, 1), // 1 draws 0
                new LeagueResult(2, 0, 0, 0), // 2 draws 0
                new LeagueResult(2, 1, 2, 1), // 2 beats 1
            };
            List<LeagueRow> table = LeagueEngine.ComputeTable(3, results);

            // Team 2: W2 D2 → 8 pts; Team 0: W1 D3 → 6 pts; Team 1: D1 L3 → 1 pt.
            Assert.That(table[0].TeamIndex, Is.EqualTo(2));
            Assert.That(table[0].Points, Is.EqualTo(8));
            Assert.That(table[1].TeamIndex, Is.EqualTo(0));
            Assert.That(table[1].Points, Is.EqualTo(6));
            Assert.That(table[2].TeamIndex, Is.EqualTo(1));
            Assert.That(table[2].Points, Is.EqualTo(1));
            foreach (LeagueRow row in table)
                Assert.That(row.Played, Is.EqualTo(4));
        }

        [Test]
        public void InvinciblesGoThirtyEightAndZero()
        {
            // Team 0 wins every one of its 38 fixtures 1-0.
            List<List<LeagueFixture>> schedule = LeagueEngine.GenerateDoubleRoundRobin(20);
            List<LeagueResult> results = new List<LeagueResult>();
            foreach (List<LeagueFixture> md in schedule)
            {
                foreach (LeagueFixture f in md)
                {
                    if (f.HomeIndex == 0)      results.Add(new LeagueResult(0, f.AwayIndex, 1, 0));
                    else if (f.AwayIndex == 0) results.Add(new LeagueResult(f.HomeIndex, 0, 0, 1));
                    else                       results.Add(new LeagueResult(f.HomeIndex, f.AwayIndex, 0, 0));
                }
            }

            List<LeagueRow> table = LeagueEngine.ComputeTable(20, results);
            LeagueRow champ = table[0];
            Assert.That(champ.TeamIndex, Is.EqualTo(0));
            Assert.That(champ.Played, Is.EqualTo(38));
            Assert.That(champ.Won, Is.EqualTo(38));   // 38-0
            Assert.That(champ.Lost, Is.EqualTo(0));
            Assert.That(champ.Points, Is.EqualTo(114));
        }
    }
}
