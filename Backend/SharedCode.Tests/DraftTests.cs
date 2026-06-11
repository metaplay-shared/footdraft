// 38-0-20 — DraftEngine unit tests (line ratings + chemistry). Corpus-agnostic: built from synthetic players so
// they don't depend on the seed squads (which the importer replaces).

using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class DraftTests
    {
        static LegendPlayer Pl(string id, Position pos, int ovr, string club) =>
            new LegendPlayer(id, id, pos, ovr, "Nation", club, Era.E2010s, "2015/16");

        static System.Func<LegendId, LegendPlayer> LookupFor(List<LegendPlayer> players)
        {
            Dictionary<string, LegendPlayer> map = new Dictionary<string, LegendPlayer>();
            foreach (LegendPlayer p in players)
                map[p.Id.Value] = p;
            return id => map.TryGetValue(id.Value, out LegendPlayer p) ? p : null;
        }

        [Test]
        public void LineRatingsAverageByLine()
        {
            FormationInfo f433 = FormationContent.Formations[0];
            Assert.That(f433.Id.Value, Is.EqualTo("4-3-3"));

            List<LegendPlayer> players = new List<LegendPlayer>
            {
                Pl("gk",  Position.GK,  80, "A"),
                Pl("d1",  Position.DEF, 90, "A"), Pl("d2", Position.DEF, 88, "A"),
                Pl("d3",  Position.DEF, 86, "A"), Pl("d4", Position.DEF, 84, "A"),
                Pl("m1",  Position.MID, 85, "A"), Pl("m2", Position.MID, 83, "A"), Pl("m3", Position.MID, 81, "A"),
                Pl("f1",  Position.FWD, 92, "A"), Pl("f2", Position.FWD, 90, "A"), Pl("f3", Position.FWD, 88, "A"),
            };
            DraftedSquad squad = new DraftedSquad { Formation = FormationId.FromString("4-3-3") };
            for (int i = 0; i < players.Count; i++)
                squad.Picks[i] = players[i].Id;

            LineRatings r = DraftEngine.ComputeLines(squad, f433, LookupFor(players));
            Assert.That(r.Goalkeeping, Is.EqualTo(80));
            Assert.That(r.Defence,     Is.EqualTo((90 + 88 + 86 + 84) / 4)); // 87
            Assert.That(r.Midfield,    Is.EqualTo((85 + 83 + 81) / 3));      // 83
            Assert.That(r.Attack,      Is.EqualTo((92 + 90 + 88) / 3));      // 90
        }

        [Test]
        public void ChemistryRewardsSameClub()
        {
            // Two players from the same club → a same-club chemistry bonus; different clubs → none.
            List<LegendPlayer> sameClub = new List<LegendPlayer> { Pl("a", Position.MID, 85, "Arsenal"), Pl("b", Position.DEF, 84, "Arsenal") };
            Assert.That(DraftEngine.Chemistry(sameClub, formation: null), Is.EqualTo(DraftEngine.SameClubPairPoints));

            List<LegendPlayer> diffClub = new List<LegendPlayer> { Pl("a", Position.MID, 85, "Arsenal"), Pl("b", Position.DEF, 84, "Chelsea") };
            Assert.That(DraftEngine.Chemistry(diffClub, formation: null), Is.EqualTo(0));
        }

        [Test]
        public void ChemistryPenalisesMissingKeeperAndThinDefence()
        {
            FormationInfo f433 = FormationContent.Formations[0];
            // Two midfielders from different clubs: no GK and < 3 defenders → both penalties.
            List<LegendPlayer> noKeeper = new List<LegendPlayer> { Pl("a", Position.MID, 85, "Arsenal"), Pl("b", Position.MID, 83, "Chelsea") };
            int chem = DraftEngine.Chemistry(noKeeper, f433);
            Assert.That(chem, Is.EqualTo(-(DraftEngine.NoGoalkeeperPenalty + DraftEngine.ThinDefencePenalty)));
        }
    }
}
