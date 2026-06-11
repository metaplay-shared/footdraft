// FOOTDRAFT — a player's weekly Bracket Cup run.

using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// The player's run in the current weekly Bracket Cup: which week it tracks, their state, the round they're
    /// on, the best round reached, and a run counter (varies the deterministic round seeds across re-entries).
    /// Resets when the bracket week rolls over (see <see cref="PlayerModel.SyncBracket"/>).
    /// </summary>
    [MetaSerializable]
    public class BracketRun
    {
        [MetaMember(1)] public long         WeekId           { get; set; } = -1;
        [MetaMember(2)] public BracketState State            { get; set; } = BracketState.None;
        [MetaMember(3)] public int          RoundIndex       { get; set; }
        [MetaMember(4)] public int          BestRoundReached { get; set; } = -1;
        [MetaMember(5)] public int          RunCount         { get; set; }

        public BracketRun() { }
    }
}
