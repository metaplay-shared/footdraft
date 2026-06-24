// FOOTDRAFT — Draft Cup (the FUT-Draft-style paid mode): pay an entry fee, draft a one-off XI, then play a
// short knockout vs escalating CPU sides for a reward ladder. Pure + deterministic: rounds resolve through
// MatchSim from a seed, so the client-predicted action and the server compute the identical outcome (no actor).

using System.Collections.Generic;
using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> A player's Draft Cup run state. </summary>
    [MetaSerializable]
    public enum DraftCupState
    {
        None       = 0, // not in a run
        Drafting   = 1, // entry paid; drafting the one-off XI
        Active     = 2, // XI drafted; a knockout round to play
        Eliminated = 3, // knocked out
        Champion   = 4, // won the final
    }

    /// <summary> The player's current Draft Cup run: state, round, best round, tier, and the last round's result. </summary>
    [MetaSerializable]
    public class DraftCupRun
    {
        [MetaMember(1)] public DraftCupState State            { get; set; } = DraftCupState.None;
        [MetaMember(2)] public int           RoundIndex       { get; set; }
        [MetaMember(3)] public int           BestRoundReached { get; set; } = -1;
        [MetaMember(4)] public int           RunCount         { get; set; }
        /// <summary> True if this run was entered with the Gems (premium) tier — boosts the reward ladder. </summary>
        [MetaMember(5)] public bool          Premium          { get; set; }
        // Last played round's result (for the result card in the hub).
        [MetaMember(6)] public int           LastMyGoals      { get; set; }
        [MetaMember(7)] public int           LastOppGoals     { get; set; }
        [MetaMember(8)] public string        LastScorers      { get; set; } = "";
        /// <summary> A flavour "match report" headline for the last resolved round. </summary>
        [MetaMember(9)] public string        LastReport       { get; set; } = "";

        public DraftCupRun() { }
    }

    /// <summary> Pure Draft Cup logic: opponent scaling, the deterministic per-round seed, and round resolution. </summary>
    public static class DraftCup
    {
        public const int RoundsTotal = 4;

        public static readonly string[] RoundNames = { "Round of 16", "Quarter-final", "Semi-final", "Final" };

        public static string RoundName(int roundIndex)
            => roundIndex >= 0 && roundIndex < RoundNames.Length ? RoundNames[roundIndex] : "—";

        /// <summary> The CPU opponent's line ratings for a round — flat OVR that climbs each round. </summary>
        public static LineRatings OpponentLines(int roundIndex, GlobalConfig global)
        {
            int ovr = global.DraftCupOpponentBaseOvr + roundIndex * global.DraftCupOpponentOvrPerRound;
            return new LineRatings
            {
                Attack      = ovr,
                Midfield    = ovr,
                Defence     = ovr,
                Goalkeeping = System.Math.Max(1, ovr - 2),
                Chemistry   = 0,
            };
        }

        /// <summary> Deterministic per-(run, round) seed so client + server resolve the same match. </summary>
        public static ulong SeedFor(int runCount, int roundIndex)
            => (ulong)(runCount + 1) * 0x9E3779B97F4A7C15ul ^ (ulong)(roundIndex + 1) * 0xC2B2AE3D27D4EB4Ful;

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
