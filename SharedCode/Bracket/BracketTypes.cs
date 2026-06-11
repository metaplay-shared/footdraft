// FOOTDRAFT — weekly marquee Bracket Cup shared types.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> State of a player's weekly Bracket Cup run. </summary>
    [MetaSerializable]
    public enum BracketState
    {
        None       = 0, // not entered this week
        Active     = 1, // in the bracket, a round to play
        Eliminated = 2, // knocked out
        Champion   = 3, // won the final
    }

    /// <summary> A reward for reaching a bracket round (index 0 = winning the Round of 16, etc.). </summary>
    [MetaSerializable]
    public class BracketRoundReward
    {
        [MetaMember(1)] public int Coins  { get; private set; }
        [MetaMember(2)] public int Gems   { get; private set; }
        [MetaMember(3)] public int Shards { get; private set; }

        public BracketRoundReward() { }
        public BracketRoundReward(int coins, int gems, int shards)
        {
            Coins  = coins;
            Gems   = gems;
            Shards = shards;
        }
    }

    /// <summary>
    /// The weekly marquee Bracket Cup: a 16-entrant single-elimination bracket (Round of 16 → Quarter-final →
    /// Semi-final → Final = 4 wins to be champion). Each round is resolved server-side as a strength-weighted
    /// deterministic dice duel vs a (tougher each round) bot. One run per <see cref="GlobalConfig.BracketWindowDays"/>-day
    /// window.
    /// </summary>
    public static class BracketCup
    {
        public const int RoundsTotal = 4;

        public static readonly string[] RoundNames =
        {
            "Round of 16", "Quarter-final", "Semi-final", "Final",
        };

        public static string RoundName(int roundIndex)
            => roundIndex >= 0 && roundIndex < RoundNames.Length ? RoundNames[roundIndex] : "—";

        public static long CurrentWeekId(MetaTime now, GlobalConfig global)
        {
            long windowMs = (long)global.BracketWindowDays * 24L * 60L * 60L * 1000L;
            return windowMs <= 0 ? 0 : now.MillisecondsSinceEpoch / windowMs;
        }

        /// <summary>
        /// Deterministically resolves one bracket round: a strength-weighted coin-flip between the player's squad
        /// strength and a per-round-scaled opponent. Returns true if the player wins.
        /// </summary>
        public static bool ResolveRound(int playerStrength, int roundIndex, ulong seed, GlobalConfig global)
        {
            int opponentStrength = global.BracketOpponentBaseStrength + roundIndex * global.BracketOpponentStrengthPerRound;
            int total = playerStrength + opponentStrength;
            if (total <= 0)
                return true;
            RandomPCG rng = RandomPCG.CreateFromSeed(seed);
            return rng.NextInt(total) < playerStrength;
        }
    }
}
