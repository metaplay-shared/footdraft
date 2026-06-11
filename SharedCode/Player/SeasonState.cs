// FOOTDRAFT — per-player season pass & ranked-ladder state.

using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// The player's Season Pass progress for the current season: pass XP earned, how many free/premium tiers have
    /// been claimed, and whether the premium track is owned. Resets each season (see <see cref="PlayerModel.SyncSeason"/>).
    /// </summary>
    [MetaSerializable]
    public class SeasonPass
    {
        [MetaMember(1)] public long SeasonId        { get; set; } = -1;
        [MetaMember(2)] public long Xp              { get; set; }
        [MetaMember(3)] public int  ClaimedFree     { get; set; }
        [MetaMember(4)] public int  ClaimedPremium  { get; set; }
        [MetaMember(5)] public bool PremiumOwned    { get; set; }

        public SeasonPass() { }
    }

    /// <summary>
    /// The player's ranked-ladder state for the current season: Season Rank Points (SRP) and the best division
    /// index reached. SRP resets each season. The current division is derived from SRP via config thresholds.
    /// </summary>
    [MetaSerializable]
    public class SeasonRank
    {
        [MetaMember(1)] public long SeasonId         { get; set; } = -1;
        [MetaMember(2)] public int  Points           { get; set; }
        [MetaMember(3)] public int  BestDivisionIndex { get; set; }

        public SeasonRank() { }
    }
}
