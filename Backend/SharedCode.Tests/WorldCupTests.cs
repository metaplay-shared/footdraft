// FOOTDRAFT — World Cup 2026 mode unit tests: the generated squad dataset, opponent-strength escalation,
// best-XI line ratings, and the config reward ladder. Pure/static — no game-config archive needed.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class WorldCupTests
    {
        [Test]
        public void DatasetHasAll48NationsWithFullSquads()
        {
            Assert.That(WorldCupContent.Nations.Count, Is.EqualTo(48), "the 2026 World Cup has 48 nations");
            Assert.That(WorldCupContent.Players.Count, Is.GreaterThanOrEqualTo(1240), "≈ 48 × 26 squad players");

            foreach (NationInfo n in WorldCupContent.Nations)
            {
                Assert.That(n.Squad.Count, Is.GreaterThanOrEqualTo(20), $"{n.DisplayName} should have a near-full squad");
                Assert.That(n.Squad.Any(p => p.Position == Position.GK), Is.True, $"{n.DisplayName} needs a keeper");
                Assert.That(n.Squad.Count(p => p.Position == Position.DEF), Is.GreaterThanOrEqualTo(3), $"{n.DisplayName} needs defenders");
                Assert.That(string.IsNullOrEmpty(n.FlagEmoji), Is.False, $"{n.DisplayName} needs a flag");
                foreach (LegendPlayer p in n.Squad)
                {
                    Assert.That(p.Ovr, Is.InRange(58, 95), $"{p.Name} ({n.DisplayName}) OVR in clamp range");
                    Assert.That(p.Id.Value, Does.StartWith("wc__"), "WC ids are namespaced");
                    Assert.That(p.Season, Is.EqualTo("WC2026"));
                }
            }
        }

        [Test]
        public void NationIdsAreUnique()
        {
            List<string> ids = WorldCupContent.Nations.Select(n => n.Id.Value).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "no duplicate nation codes");
        }

        [Test]
        public void BestXiOverallAndLinesAreSane()
        {
            foreach (NationInfo n in WorldCupContent.Nations)
            {
                int strength = WorldCup.BestXiOverall(n);
                Assert.That(strength, Is.InRange(58, 95), $"{n.DisplayName} best-XI overall in range");

                LineRatings lines = WorldCup.LinesForNation(n);
                Assert.That(lines.Attack, Is.GreaterThan(0));
                Assert.That(lines.Midfield, Is.GreaterThan(0));
                Assert.That(lines.Defence, Is.GreaterThan(0));
                Assert.That(lines.Goalkeeping, Is.GreaterThan(0));
                Assert.That(lines.Chemistry, Is.EqualTo(WorldCup.NationChemistry));
            }
        }

        [Test]
        public void OpponentStrengthEscalatesByRound()
        {
            GlobalConfig g = new GlobalConfig();
            int rounds = WorldCup.RoundsTotal(g);
            Assert.That(rounds, Is.EqualTo(5));

            // Average the opening-round and final-round opponent strength across many runs; the final should
            // pull from a markedly stronger band than the opening round.
            double firstAvg = 0, finalAvg = 0;
            const int runs = 64;
            for (int run = 0; run < runs; run++)
            {
                firstAvg += WorldCup.OpponentNation(run, 0, g).Strength;
                finalAvg += WorldCup.OpponentNation(run, rounds - 1, g).Strength;
            }
            firstAvg /= runs;
            finalAvg /= runs;
            Assert.That(finalAvg, Is.GreaterThan(firstAvg + 3), $"final opponents ({finalAvg:0.0}) should be tougher than R32 ({firstAvg:0.0})");
        }

        [Test]
        public void OpponentSelectionIsDeterministic()
        {
            GlobalConfig g = new GlobalConfig();
            NationInfo a = WorldCup.OpponentNation(7, 2, g);
            NationInfo b = WorldCup.OpponentNation(7, 2, g);
            Assert.That(a.Id.Value, Is.EqualTo(b.Id.Value), "same (run, round) → same opponent (client/server agree)");
        }

        [Test]
        public void RewardLadderMatchesRoundsAndEscalates()
        {
            GlobalConfig g = new GlobalConfig();
            Assert.That(g.WorldCupRoundRewards.Length, Is.EqualTo(g.WorldCupRoundNames.Length), "one reward per round");
            for (int i = 1; i < g.WorldCupRoundRewards.Length; i++)
                Assert.That(g.WorldCupRoundRewards[i].Coins, Is.GreaterThan(g.WorldCupRoundRewards[i - 1].Coins),
                    "deeper rounds pay more");
            Assert.That(g.WorldCupEntryCoins, Is.GreaterThan(0));
            Assert.That(g.WorldCupEntryGems, Is.GreaterThan(0));
        }

        [Test]
        public void StrongDraftedXiBeatsAWeakNationMostOfTheTime()
        {
            // Knockout football has upsets on any single deterministic seed, so balance is measured over many:
            // a near-perfect drafted XI should clearly win the majority of ties vs the weakest nation.
            GlobalConfig g = new GlobalConfig();
            NationInfo weak = WorldCupContent.NationsByStrength[WorldCupContent.NationsByStrength.Count - 1];
            LineRatings you = new LineRatings { Attack = 92, Midfield = 90, Defence = 90, Goalkeeping = 90, Chemistry = 20 };
            LineRatings opp = WorldCup.LinesForNation(weak);

            int wins = 0, trials = 200;
            for (int s = 0; s < trials; s++)
            {
                MatchResult result = MatchSim.Resolve(you, opp, WorldCup.SeedFor(s, s % 5));
                if (WorldCup.IsWin(result, you, opp)) wins++;
            }
            Assert.That(wins / (double)trials, Is.GreaterThan(0.7), $"elite XI won {wins}/{trials} vs {weak.DisplayName}");
        }

        [Test]
        public void StarPlayersCarryRealRatings()
        {
            // Sanity: a few household names resolved to elite ratings (curated or FIFA), not the position floor.
            string[] stars = { "Messi", "Mbappé", "Haaland", "Bellingham", "Vinícius" };
            foreach (string star in stars)
            {
                LegendPlayer p = WorldCupContent.Players.FirstOrDefault(x => x.Name.Contains(star));
                if (p != null)
                    Assert.That(p.Ovr, Is.GreaterThanOrEqualTo(85), $"{p.Name} should be elite-rated");
            }
        }
    }
}
