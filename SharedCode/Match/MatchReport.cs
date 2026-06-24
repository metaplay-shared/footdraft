// FOOTDRAFT — deterministic "AI" match reports: a short, flavourful headline for a resolved match, picked from
// seeded template pools so it varies per match but client + server generate the identical line. Pure, no state.
// Not an LLM — templated commentary keyed to the scoreline/margin/stakes (cheap, offline, reproducible).

using Metaplay.Core;

namespace Game.Logic
{
    public static class MatchReport
    {
        /// <summary>
        /// A knockout-round headline from the drafting manager's perspective. <paramref name="win"/> /
        /// <paramref name="champion"/> come from the caller (a draw can still be a win on tie-breaks).
        /// </summary>
        public static string Knockout(string oppName, int myGoals, int oppGoals, string scorers, string roundName, bool win, bool champion, ulong seed)
        {
            RandomPCG rng = RandomPCG.CreateFromSeed(seed ^ 0xA17EC0FFEEUL);
            string opp = string.IsNullOrEmpty(oppName) ? "the opponent" : oppName;
            string scl = string.IsNullOrEmpty(scorers) ? "" : $" {scorers} on the scoresheet.";
            int margin = myGoals - oppGoals;

            if (win && champion)
                return Pick(rng, new[]
                {
                    $"🏆 CHAMPIONS! {opp} beaten {myGoals}–{oppGoals} in the final — the trophy is yours.{scl}",
                    $"🏆 Glory! A {myGoals}–{oppGoals} final win over {opp} crowns you champions.{scl}",
                    $"🏆 History made — {opp} swept aside {myGoals}–{oppGoals} to lift the cup.{scl}",
                });

            if (win && margin >= 3)
                return Pick(rng, new[]
                {
                    $"Statement win — {opp} demolished {myGoals}–{oppGoals} in the {roundName}.{scl}",
                    $"Rampant. {myGoals}–{oppGoals} past {opp} and into the next round.{scl}",
                    $"No contest: {opp} brushed aside {myGoals}–{oppGoals} in the {roundName}.{scl}",
                });

            if (win && margin <= 0) // advanced on a tie-break / late drama
                return Pick(rng, new[]
                {
                    $"Nerves of steel — {myGoals}–{oppGoals} with {opp}, but you squeeze through the {roundName}.{scl}",
                    $"Drama! Level at {myGoals}–{oppGoals}, you edge {opp} on the fine margins.{scl}",
                });

            if (win)
                return Pick(rng, new[]
                {
                    $"Through! A hard-fought {myGoals}–{oppGoals} over {opp} in the {roundName}.{scl}",
                    $"{opp} edged {myGoals}–{oppGoals} — job done in the {roundName}.{scl}",
                    $"Tense but taken: {myGoals}–{oppGoals} past {opp} to advance.{scl}",
                });

            // Eliminated.
            return Pick(rng, new[]
            {
                $"Heartbreak — {opp} knock you out {oppGoals}–{myGoals} in the {roundName}.{scl}",
                $"The run ends in the {roundName}: {opp} win {oppGoals}–{myGoals}.{scl}",
                $"So close. {opp} edge it {oppGoals}–{myGoals} and your campaign is over.{scl}",
            });
        }

        /// <summary> A league-fixture headline from the viewing manager's perspective. </summary>
        public static string Fixture(string oppName, int myGoals, int oppGoals, string scorers, ulong seed)
        {
            RandomPCG rng = RandomPCG.CreateFromSeed(seed ^ 0x1EA6DE5UL);
            string opp = string.IsNullOrEmpty(oppName) ? "the opponent" : oppName;
            string scl = string.IsNullOrEmpty(scorers) ? "" : $" {scorers} netted.";
            int margin = myGoals - oppGoals;

            if (margin >= 3) return Pick(rng, new[] { $"Routed {opp} {myGoals}–{oppGoals}.{scl}", $"Five-star stuff — {myGoals}–{oppGoals} over {opp}.{scl}" });
            if (margin > 0)  return Pick(rng, new[] { $"Beat {opp} {myGoals}–{oppGoals}, three points banked.{scl}", $"A {myGoals}–{oppGoals} win over {opp}.{scl}" });
            if (margin == 0) return Pick(rng, new[] { $"Shared the spoils with {opp}, {myGoals}–{oppGoals}.{scl}", $"Honours even vs {opp} at {myGoals}–{oppGoals}.{scl}" });
            return Pick(rng, new[] { $"Beaten {oppGoals}–{myGoals} by {opp}.{scl}", $"{opp} took it {oppGoals}–{myGoals} — back to work.{scl}" });
        }

        static string Pick(RandomPCG rng, string[] opts) => opts[rng.NextInt(opts.Length)];
    }
}
