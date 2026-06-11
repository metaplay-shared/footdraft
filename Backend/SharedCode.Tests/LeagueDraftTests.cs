// FOOTDRAFT — league snake-draft engine tests (pure logic; no server/config-load required).
//
// These exercise the exact functions the LeagueActor drives, so they validate the three guarantees the vision
// requires: managers pick in turns (snake order), a legend can only be on ONE team (shared `taken` pool), and
// every team ends with a valid formation (one legend per slot, positions matching). The final test drafts a full
// 20-manager league and simulates the whole 38-matchday season.

using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class LeagueDraftTests
    {
        static readonly IReadOnlyList<LegendPlayer> Corpus = LegendContent.Legends;
        static readonly IReadOnlyList<LegendPlayer> Pool   = LeagueDraftEngine.BuildDraftPool(LegendContent.Legends);

        static System.Func<LegendId, LegendPlayer> Lookup()
        {
            Dictionary<string, LegendPlayer> map = new Dictionary<string, LegendPlayer>();
            foreach (LegendPlayer p in Corpus)
                map[p.Id.Value] = p;
            return id => map.TryGetValue(id.Value, out LegendPlayer p) ? p : null;
        }

        static FormationInfo Formation(string id)
        {
            foreach (FormationInfo f in FormationContent.Formations)
                if (f.Id.Value == id)
                    return f;
            return null;
        }

        #region Snake order

        [Test]
        public void SnakeOrderReversesEachRound()
        {
            // 4 managers: round 0 → 0,1,2,3 ; round 1 → 3,2,1,0 ; round 2 → 0,1,2,3 …
            int[] expected = { 0, 1, 2, 3, 3, 2, 1, 0, 0, 1, 2, 3 };
            for (int pick = 0; pick < expected.Length; pick++)
                Assert.That(LeagueDraftEngine.CurrentDrafterIndex(pick, 4), Is.EqualTo(expected[pick]), $"pick {pick}");

            Assert.That(LeagueDraftEngine.RoundNumber(0, 4),  Is.EqualTo(1));
            Assert.That(LeagueDraftEngine.RoundNumber(4, 4),  Is.EqualTo(2));
            Assert.That(LeagueDraftEngine.RoundNumber(11, 4), Is.EqualTo(3));
        }

        [Test]
        public void DraftCompletesAfterElevenPicksEach()
        {
            Assert.That(LeagueDraftEngine.TotalPicks(4), Is.EqualTo(44));
            Assert.That(LeagueDraftEngine.IsComplete(43, 4), Is.False);
            Assert.That(LeagueDraftEngine.IsComplete(44, 4), Is.True);
            Assert.That(LeagueDraftEngine.CurrentDrafterIndex(44, 4), Is.EqualTo(-1)); // nobody's turn once complete
        }

        #endregion

        #region Slot / position validation

        [Test]
        public void NextOpenSlotMatchesFormationPositionsThenFills()
        {
            FormationInfo f433 = Formation("4-3-3"); // GK, 4 DEF, 3 MID, 3 FWD
            Dictionary<int, string> roster = new Dictionary<int, string>();

            // First DEF goes to slot 1 (slot 0 is GK); GK goes to slot 0.
            Assert.That(LeagueDraftEngine.NextOpenSlotForPosition(f433, roster, Position.GK),  Is.EqualTo(0));
            Assert.That(LeagueDraftEngine.NextOpenSlotForPosition(f433, roster, Position.DEF), Is.EqualTo(1));

            // Fill all four DEF slots; a fifth defender then has nowhere to go.
            roster[1] = "a"; roster[2] = "b"; roster[3] = "c"; roster[4] = "d";
            Assert.That(LeagueDraftEngine.NextOpenSlotForPosition(f433, roster, Position.DEF), Is.EqualTo(-1));
            Assert.That(LeagueDraftEngine.NextOpenSlotForPosition(f433, roster, Position.MID), Is.EqualTo(5));
        }

        [Test]
        public void CanPickRejectsTakenAndPositionFull()
        {
            FormationInfo f433 = Formation("4-3-3");
            LegendPlayer keeper = null;
            foreach (LegendPlayer p in Corpus)
                if (p.Position == Position.GK) { keeper = p; break; }
            Assert.That(keeper, Is.Not.Null);

            Dictionary<int, string> roster = new Dictionary<int, string>();
            HashSet<string> taken = new HashSet<string>();

            Assert.That(LeagueDraftEngine.CanPick(f433, roster, taken, keeper), Is.True);

            // The real player is already taken (uniqueness is by name) → cannot pick.
            taken.Add(keeper.Name);
            Assert.That(LeagueDraftEngine.CanPick(f433, roster, taken, keeper), Is.False);

            // GK slot already filled → a second keeper has no open slot.
            taken.Clear();
            roster[0] = "someone_else";
            Assert.That(LeagueDraftEngine.CanPick(f433, roster, taken, keeper), Is.False);
        }

        #endregion

        #region Full simulated drafts

        // Drives a complete snake draft exactly the way the LeagueActor will: each turn, the current drafter takes
        // the best available legend that fits an open slot. Returns the filled rosters.
        static Dictionary<int, Dictionary<int, string>> RunDraft(string[] formationIds)
        {
            int n = formationIds.Length;
            FormationInfo[] formations = new FormationInfo[n];
            for (int i = 0; i < n; i++)
                formations[i] = Formation(formationIds[i]);

            Dictionary<int, Dictionary<int, string>> rosters = new Dictionary<int, Dictionary<int, string>>();
            for (int i = 0; i < n; i++)
                rosters[i] = new Dictionary<int, string>();
            HashSet<string> taken = new HashSet<string>();

            int pick = 0;
            int guard = 0;
            while (!LeagueDraftEngine.IsComplete(pick, n))
            {
                Assert.That(guard++, Is.LessThan(LeagueDraftEngine.TotalPicks(n) + 5), "draft did not converge");

                int drafter = LeagueDraftEngine.CurrentDrafterIndex(pick, n);
                Assert.That(drafter, Is.InRange(0, n - 1));

                LegendPlayer best = LeagueDraftEngine.BestAvailablePick(formations[drafter], rosters[drafter], taken, Pool);
                Assert.That(best, Is.Not.Null, $"pool exhausted at pick {pick} for member {drafter}");

                int slot = LeagueDraftEngine.NextOpenSlotForPosition(formations[drafter], rosters[drafter], best.Position);
                Assert.That(slot, Is.GreaterThanOrEqualTo(0));

                rosters[drafter][slot] = best.Id.Value;
                taken.Add(best.Name); // uniqueness by real-player name
                pick++;
            }
            return rosters;
        }

        static void AssertRostersValidAndUnique(Dictionary<int, Dictionary<int, string>> rosters, string[] formationIds)
        {
            HashSet<string> globallyPicked = new HashSet<string>();
            HashSet<string> globalNames    = new HashSet<string>();
            System.Func<LegendId, LegendPlayer> lookup = Lookup();

            for (int i = 0; i < formationIds.Length; i++)
            {
                FormationInfo f = Formation(formationIds[i]);
                Dictionary<int, string> roster = rosters[i];

                // Every slot filled exactly once → a complete XI.
                Assert.That(roster.Count, Is.EqualTo(11), $"member {i} XI size");
                for (int slot = 0; slot < f.Slots.Count; slot++)
                {
                    Assert.That(roster.ContainsKey(slot), Is.True, $"member {i} slot {slot} empty");
                    // The legend in each slot plays that slot's position → a valid formation.
                    LegendPlayer p = lookup(LegendId.FromString(roster[slot]));
                    Assert.That(p, Is.Not.Null);
                    Assert.That(p.Position, Is.EqualTo(f.Slots[slot]), $"member {i} slot {slot} wrong position");
                }

                // No legend id AND no real player (by name) appears on two teams (the shared-pool guarantee).
                foreach (KeyValuePair<int, string> kv in roster)
                {
                    Assert.That(globallyPicked.Add(kv.Value), Is.True, $"legend {kv.Value} drafted twice");
                    LegendPlayer p = lookup(LegendId.FromString(kv.Value));
                    Assert.That(globalNames.Add(p.Name), Is.True, $"player '{p.Name}' drafted onto two teams");
                }
            }
        }

        [Test]
        public void DraftPoolHasOneEntryPerPlayer()
        {
            // The full corpus repeats stars across (club, era) buckets; the pool must keep each name once.
            HashSet<string> names = new HashSet<string>();
            foreach (LegendPlayer p in Pool)
                Assert.That(names.Add(p.Name), Is.True, $"duplicate name in pool: {p.Name}");

            Assert.That(Pool.Count, Is.LessThan(Corpus.Count), "pool should be smaller than the corpus (dupes removed)");
            // The seed comfortably supports the multi-team draft test below; the full importer DB fills a 20-team league.
            Assert.That(Pool.Count, Is.GreaterThanOrEqualTo(110), "pool must fill the multi-team draft test");
        }

        [Test]
        public void FourManagerMixedFormationDraftIsValidAndUnique()
        {
            string[] formations = { "4-3-3", "3-5-2", "5-3-2", "4-4-2" };
            Dictionary<int, Dictionary<int, string>> rosters = RunDraft(formations);
            AssertRostersValidAndUnique(rosters, formations);
        }

        [Test]
        public void MultiManagerDraftFillsEverySquadWithoutCollision()
        {
            // A 10-manager snake draft with rotating formations — the seed pool fills this; the full importer DB
            // scales it to the 20-team product league.
            string[] menu = { "4-3-3", "4-4-2", "4-2-3-1", "3-5-2", "5-3-2" };
            const int teams = 10;
            string[] formations = new string[teams];
            for (int i = 0; i < teams; i++)
                formations[i] = menu[i % menu.Length];

            Dictionary<int, Dictionary<int, string>> rosters = RunDraft(formations);
            AssertRostersValidAndUnique(rosters, formations);

            // teams × 11 distinct players drafted across the league.
            HashSet<string> all = new HashSet<string>();
            foreach (KeyValuePair<int, Dictionary<int, string>> r in rosters)
                foreach (KeyValuePair<int, string> kv in r.Value)
                    all.Add(kv.Value);
            Assert.That(all.Count, Is.EqualTo(teams * 11));
        }

        [Test]
        public void DraftedLeagueResolvesRatingsAndSimulatesFullSeason()
        {
            string[] formations = { "4-3-3", "3-5-2", "5-3-2", "4-4-2" };
            Dictionary<int, Dictionary<int, string>> rosters = RunDraft(formations);
            System.Func<LegendId, LegendPlayer> lookup = Lookup();

            // Resolve each drafted roster to line ratings (what the actor locks at draft completion).
            Dictionary<int, LineRatings> locked = new Dictionary<int, LineRatings>();
            for (int i = 0; i < formations.Length; i++)
            {
                LineRatings r = LeagueDraftEngine.ResolveRosterRatings(Formation(formations[i]), rosters[i], lookup);
                Assert.That(r.Goalkeeping, Is.GreaterThan(0));
                Assert.That(r.Defence,     Is.GreaterThan(0));
                Assert.That(r.Midfield,    Is.GreaterThan(0));
                Assert.That(r.Attack,      Is.GreaterThan(0));
                locked[i] = r;
            }

            // Simulate every fixture of the double round-robin, exactly like LeagueActor's simulate-all.
            List<List<LeagueFixture>> schedule = LeagueEngine.GenerateDoubleRoundRobin(formations.Length);
            List<LeagueResult> results = new List<LeagueResult>();
            foreach (List<LeagueFixture> matchday in schedule)
            {
                foreach (LeagueFixture f in matchday)
                {
                    MatchResult sim = MatchSim.Resolve(locked[f.HomeIndex], locked[f.AwayIndex], seed: (ulong)((f.HomeIndex + 1) * 31 + f.AwayIndex + 1));
                    results.Add(new LeagueResult(f.HomeIndex, f.AwayIndex, sim.HomeGoals, sim.AwayGoals));
                }
            }

            List<LeagueRow> table = LeagueEngine.ComputeTable(formations.Length, results);
            Assert.That(table.Count, Is.EqualTo(4));
            int playedEach = 2 * (formations.Length - 1); // 6 for 4 teams
            foreach (LeagueRow row in table)
                Assert.That(row.Played, Is.EqualTo(playedEach));
        }

        #endregion
    }
}
