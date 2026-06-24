// FOOTDRAFT — deterministic XI-vs-XI football match sim. Replaces the best-of-5 dice duel.
//
// Pure + server-authoritative: takes each side's LineRatings (the SAME object the draft produces via
// DraftEngine.ComputeLines) plus a seed, and resolves a full match — a list of timed goal events and a
// final scoreline. Seeded from synchronized model state so server + every client reproduce it exactly
// (server-rolled — no client mod can change a result).

using System.Collections.Generic;
using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> A goal at a given match minute, for one side. Drives the live goal-by-goal reveal. </summary>
    [MetaSerializable]
    public class GoalEvent
    {
        [MetaMember(1)] public int  Minute     { get; private set; }
        [MetaMember(2)] public bool HomeScored { get; private set; }

        public GoalEvent() { }
        public GoalEvent(int minute, bool homeScored)
        {
            Minute     = minute;
            HomeScored = homeScored;
        }
    }

    /// <summary> A goal with its named scorer + (optional) assister — the match "details" testers asked for. </summary>
    [MetaSerializable]
    public class MatchGoalDetail
    {
        [MetaMember(1)] public int    Minute   { get; private set; }
        [MetaMember(2)] public bool   HomeSide { get; private set; } // scored by the fixture's home side
        [MetaMember(3)] public string Scorer   { get; private set; } = "";
        [MetaMember(4)] public string Assist   { get; private set; } = ""; // "" = unassisted

        public MatchGoalDetail() { }
        public MatchGoalDetail(int minute, bool homeSide, string scorer, string assist)
        {
            Minute   = minute;
            HomeSide = homeSide;
            Scorer   = scorer ?? "";
            Assist   = assist ?? "";
        }
    }

    /// <summary> A resolved match: the timed goals and the final score. "Home" = the first side passed to the sim. </summary>
    [MetaSerializable]
    public class MatchResult
    {
        [MetaMember(1)] public List<GoalEvent> Goals     { get; private set; } = new List<GoalEvent>();
        [MetaMember(2)] public int             HomeGoals { get; private set; }
        [MetaMember(3)] public int             AwayGoals { get; private set; }

        public MatchResult() { }
        public MatchResult(List<GoalEvent> goals, int homeGoals, int awayGoals)
        {
            Goals     = goals;
            HomeGoals = homeGoals;
            AwayGoals = awayGoals;
        }

        public bool IsDraw  => HomeGoals == AwayGoals;
        public bool HomeWon => HomeGoals >  AwayGoals;
    }

    /// <summary>
    /// Resolves an XI-vs-XI match from two <see cref="LineRatings"/> and a seed. Each side gets a share of a
    /// fixed pool of attacks (weighted by midfield + attack), and each attack converts to a goal with a
    /// probability driven by that side's attacking power vs the opponent's defensive power. All randomness comes
    /// from a single seeded <see cref="RandomPCG"/>, drawn in a fixed order, so the outcome is fully reproducible.
    /// </summary>
    public static class MatchSim
    {
        /// <summary> Total dangerous attacks in a match, split between the sides by midfield/attack share. </summary>
        public const int TotalAttacks = 18;

        /// <summary> Weight on the defender's resistance — higher means fewer goals convert (tunes scoring rate). </summary>
        public const int DefenceResistance = 7;

        /// <summary> Attacking power: forwards + midfield supply + a positive chemistry boost. </summary>
        public static int Offence(LineRatings r) => r.Attack * 3 + r.Midfield * 2 + (r.Chemistry > 0 ? r.Chemistry : 0);

        /// <summary> Defensive power: defenders + midfield screen + goalkeeping. </summary>
        public static int Defence(LineRatings r) => r.Defence * 3 + r.Midfield + r.Goalkeeping * 2;

        public static MatchResult Resolve(LineRatings home, LineRatings away, ulong seed)
        {
            RandomPCG rng = RandomPCG.CreateFromSeed(seed);

            int offH = Offence(home), defH = Defence(home);
            int offA = Offence(away), defA = Defence(away);

            // Split the attack pool by each side's midfield+attack initiative.
            int initH = home.Midfield + home.Attack + 1;
            int initA = away.Midfield + away.Attack + 1;
            int homeAttacks = TotalAttacks * initH / (initH + initA);
            int awayAttacks = TotalAttacks - homeAttacks;

            List<GoalEvent> goals = new List<GoalEvent>();
            int h = 0, a = 0;

            // Home attacks first, then away — a fixed draw order keeps the result reproducible.
            for (int i = 0; i < homeAttacks; i++)
            {
                if (Converts(rng, offH, defA))
                {
                    goals.Add(new GoalEvent(MinuteFor(rng, i, homeAttacks), homeScored: true));
                    h++;
                }
            }
            for (int i = 0; i < awayAttacks; i++)
            {
                if (Converts(rng, offA, defH))
                {
                    goals.Add(new GoalEvent(MinuteFor(rng, i, awayAttacks), homeScored: false));
                    a++;
                }
            }

            goals.Sort((x, y) => x.Minute.CompareTo(y.Minute));
            return new MatchResult(goals, h, a);
        }

        /// <summary> An attack converts with probability offence / (offence + DefenceResistance·oppDefence). </summary>
        static bool Converts(RandomPCG rng, int offence, int oppDefence)
        {
            int denom = offence + DefenceResistance * oppDefence;
            if (denom <= 0 || offence <= 0)
                return false;
            return rng.NextInt(denom) < offence;
        }

        /// <summary> Spreads a side's attacks across the 90 minutes, with a little deterministic jitter. </summary>
        static int MinuteFor(RandomPCG rng, int index, int total)
        {
            int baseMinute = (index + 1) * 90 / (total + 1);
            int jitter     = rng.NextInt(9) - 4;
            int minute     = baseMinute + jitter;
            return minute < 1 ? 1 : (minute > 90 ? 90 : minute);
        }

        /// <summary>
        /// Assigns a named scorer + (usually) an assister to each goal in <paramref name="result"/>, drawn from the
        /// scoring side's squad weighted by position (forwards score most, keepers never). Deterministic but uses
        /// its OWN seeded RandomPCG (derived from the match seed) so it never perturbs Resolve's draw order, while
        /// the server + every client still reproduce identical scorers. A squad entry is (name, position, ovr).
        /// </summary>
        public static List<MatchGoalDetail> AttributeScorers(MatchResult result,
            IReadOnlyList<(string Name, Position Pos, int Ovr)> homeSquad,
            IReadOnlyList<(string Name, Position Pos, int Ovr)> awaySquad,
            ulong seed)
        {
            RandomPCG rng = RandomPCG.CreateFromSeed(seed ^ 0x90F1A2B3C4D5E6F7ul);
            List<MatchGoalDetail> details = new List<MatchGoalDetail>();
            if (result?.Goals == null)
                return details;
            foreach (GoalEvent g in result.Goals)
            {
                IReadOnlyList<(string Name, Position Pos, int Ovr)> squad = g.HomeScored ? homeSquad : awaySquad;
                string scorer = PickWeighted(rng, squad, ScorerWeight, excludeName: null);
                // ~1 in 4 goals is unassisted (solo runs, penalties, rebounds) — roll BEFORE the assist draw so the order is fixed.
                bool unassisted = rng.NextInt(100) < 25;
                string assist = unassisted ? "" : PickWeighted(rng, squad, AssistWeight, excludeName: scorer);
                details.Add(new MatchGoalDetail(g.Minute, g.HomeScored, scorer, assist));
            }
            return details;
        }

        static int ScorerWeight((string Name, Position Pos, int Ovr) p)
        {
            int posW = p.Pos switch { Position.FWD => 6, Position.MID => 3, Position.DEF => 1, _ => 0 };
            return posW * System.Math.Max(1, p.Ovr - 55);
        }

        static int AssistWeight((string Name, Position Pos, int Ovr) p)
        {
            int posW = p.Pos switch { Position.MID => 5, Position.FWD => 4, Position.DEF => 2, _ => 0 };
            return posW * System.Math.Max(1, p.Ovr - 55);
        }

        static string PickWeighted(RandomPCG rng, IReadOnlyList<(string Name, Position Pos, int Ovr)> squad,
            System.Func<(string Name, Position Pos, int Ovr), int> weight, string excludeName)
        {
            int total = 0;
            foreach ((string Name, Position Pos, int Ovr) p in squad)
                if (p.Name != excludeName) total += weight(p);
            if (total <= 0)
            {
                foreach ((string Name, Position Pos, int Ovr) p in squad)
                    if (p.Name != excludeName) return p.Name; // fallback: any other player
                return "";
            }
            int roll = rng.NextInt(total);
            foreach ((string Name, Position Pos, int Ovr) p in squad)
            {
                if (p.Name == excludeName) continue;
                int w = weight(p);
                if (w <= 0) continue;
                if (roll < w) return p.Name;
                roll -= w;
            }
            return "";
        }
    }
}
