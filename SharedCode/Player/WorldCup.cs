// FOOTDRAFT — World Cup 2026 mode: pay an entry fee, spin-draft a one-off XI from real WC national-team
// squads, then play the 2026 knockout (Round of 32 → Final) vs real nations of escalating strength for a
// reward ladder. Same shape as the Draft Cup, but the draft pool is WC squads (bucketed by nation) and the
// opponents are real nations' best XIs — not a flat escalating CPU OVR. Pure + deterministic (MatchSim from a
// seed), so the client-predicted action and the server compute the identical outcome (no actor needed).

using System.Collections.Generic;
using System.Linq;
using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> A player's World Cup run state (mirrors <see cref="DraftCupState"/>). </summary>
    [MetaSerializable]
    public enum WorldCupState
    {
        None       = 0, // not in a run
        Drafting   = 1, // entry paid; drafting the one-off WC XI
        Active     = 2, // XI drafted; a knockout round to play
        Eliminated = 3, // knocked out
        Champion   = 4, // lifted the trophy
    }

    /// <summary> The player's current World Cup run: state, round, best round, tier, and the last round's result. </summary>
    [MetaSerializable]
    public class WorldCupRun
    {
        [MetaMember(1)] public WorldCupState State            { get; set; } = WorldCupState.None;
        [MetaMember(2)] public int           RoundIndex       { get; set; }
        [MetaMember(3)] public int           BestRoundReached { get; set; } = -1;
        [MetaMember(4)] public int           RunCount         { get; set; }
        /// <summary> True if entered with the Gems (premium) tier — boosts the reward ladder. </summary>
        [MetaMember(5)] public bool          Premium          { get; set; }
        // Last played round's result (for the result card in the hub).
        [MetaMember(6)] public int           LastMyGoals      { get; set; }
        [MetaMember(7)] public int           LastOppGoals     { get; set; }
        [MetaMember(8)] public string        LastScorers      { get; set; } = "";
        /// <summary> The nation faced in the last resolved round (badge "🇧🇷 Brazil"), for the result card. </summary>
        [MetaMember(9)] public string        LastOpponent     { get; set; } = "";
        /// <summary> A flavour "match report" headline for the last resolved round. </summary>
        [MetaMember(10)] public string       LastReport       { get; set; } = "";

        public WorldCupRun() { }
    }

    /// <summary> Pure World Cup logic: round naming, real-nation opponent selection + line ratings, seeds, resolution. </summary>
    public static class WorldCup
    {
        /// <summary> National sides play with built-in cohesion — a small chemistry floor for opponent lines. </summary>
        public const int NationChemistry = 6;

        /// <summary> Number of knockout rounds (config-driven via the round-name list; 5 = R32 → Final). </summary>
        public static int RoundsTotal(GlobalConfig global)
            => global?.WorldCupRoundNames is { Length: > 0 } names ? names.Length : 5;

        public static string RoundName(GlobalConfig global, int roundIndex)
        {
            string[] names = global?.WorldCupRoundNames;
            return names != null && roundIndex >= 0 && roundIndex < names.Length ? names[roundIndex] : "—";
        }

        /// <summary>
        /// The opponent nation for a (run, round): drawn from the real nations by strength, escalating — the
        /// weakest band in the opening round, the strongest band in the final. Deterministic from the seed so
        /// client + server agree. Returns null only if no nations are configured (importer not run).
        /// </summary>
        public static NationInfo OpponentNation(int runCount, int roundIndex, GlobalConfig global)
        {
            IReadOnlyList<NationInfo> sorted = WorldCupContent.NationsByStrength; // index 0 = strongest
            int c = sorted.Count;
            if (c == 0)
                return null;

            int n         = RoundsTotal(global);
            int band      = System.Math.Clamp(n - 1 - roundIndex, 0, n - 1); // round 0 → weakest (highest index)
            int bandSize  = System.Math.Max(1, c / n);
            int bandStart = System.Math.Min(band * bandSize, c - 1);
            int bandCount = (band == n - 1) ? (c - bandStart) : System.Math.Min(bandSize, c - bandStart);
            ulong seed    = SeedFor(runCount, roundIndex);
            int pick      = bandStart + (int)(seed % (ulong)System.Math.Max(1, bandCount));
            return sorted[System.Math.Clamp(pick, 0, c - 1)];
        }

        /// <summary> A nation's opponent line ratings: its best XI by line (mirrors <see cref="DraftEngine.ComputeLines"/>). </summary>
        public static LineRatings LinesForNation(NationInfo nation)
        {
            if (nation == null)
                return new LineRatings { Attack = 70, Midfield = 70, Defence = 70, Goalkeeping = 68, Chemistry = 0 };
            return new LineRatings
            {
                Attack      = AvgTop(nation, Position.FWD, 3),
                Midfield    = AvgTop(nation, Position.MID, 3),
                Defence     = AvgTop(nation, Position.DEF, 4),
                Goalkeeping = System.Math.Max(1, AvgTop(nation, Position.GK, 1)),
                Chemistry   = NationChemistry,
            };
        }

        /// <summary> Best-XI overall (avg of the best 11 in a 4-3-3) — the nation's bracket-seeding strength. </summary>
        public static int BestXiOverall(NationInfo nation)
        {
            if (nation == null)
                return 0;
            List<int> xi = new List<int>(11);
            xi.AddRange(TopOvrs(nation, Position.GK,  1));
            xi.AddRange(TopOvrs(nation, Position.DEF, 4));
            xi.AddRange(TopOvrs(nation, Position.MID, 3));
            xi.AddRange(TopOvrs(nation, Position.FWD, 3));
            if (xi.Count == 0)
                return 0;
            int sum = 0;
            foreach (int v in xi) sum += v;
            return sum / xi.Count;
        }

        static List<int> TopOvrs(NationInfo nation, Position pos, int n)
            => nation.Squad.Where(p => p.Position == pos).OrderByDescending(p => p.Ovr).Take(n).Select(p => p.Ovr).ToList();

        static int AvgTop(NationInfo nation, Position pos, int n)
        {
            List<int> top = TopOvrs(nation, pos, n);
            if (top.Count == 0)
                return 0;
            int sum = 0;
            foreach (int v in top) sum += v;
            return sum / top.Count;
        }

        /// <summary> Deterministic per-(run, round) seed so client + server resolve the same match. </summary>
        public static ulong SeedFor(int runCount, int roundIndex)
            => (ulong)(runCount + 1) * 0xD1B54A32D192ED03ul ^ (ulong)(roundIndex + 1) * 0x9E3779B97F4A7C15ul;

        /// <summary> True if the player's XI advances: win on the scoreline, or take a draw on the stronger attack. </summary>
        public static bool IsWin(MatchResult result, LineRatings you, LineRatings opp)
            => result.HomeWon || (result.IsDraw && MatchSim.Offence(you) >= MatchSim.Offence(opp));

        /// <summary> A compact "Surname 23', Surname 67'" line for the player's own goals in a resolved round. </summary>
        public static string ScorersLine(MatchResult result, IReadOnlyList<(string Name, Position Pos, int Ovr)> mySquad, ulong seed)
        {
            List<MatchGoalDetail> goals = MatchSim.AttributeScorers(result, mySquad, System.Array.Empty<(string Name, Position Pos, int Ovr)>(), seed);
            List<string> mine = new List<string>();
            foreach (MatchGoalDetail g in goals)
                if (g.HomeSide && !string.IsNullOrEmpty(g.Scorer))
                    mine.Add($"{Surname(g.Scorer)} {g.Minute}'");
            return string.Join(", ", mine);
        }

        static string Surname(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            string[] parts = name.Split(' ');
            return parts.Length > 1 ? parts[parts.Length - 1] : name;
        }
    }
}
