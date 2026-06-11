// FOOTDRAFT — manager progression & lifetime stats.

using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// Manager progression state. The manager <b>level</b> itself lives on the base <c>PlayerModel.PlayerLevel</c>
    /// (so the LiveOps Dashboard shows it); this sub-model holds the XP toward the next level plus the lifetime
    /// match statistics that drive streaks, leaderboards, and analytics.
    /// </summary>
    [MetaSerializable]
    public class PlayerProgression
    {
        /// <summary> XP accumulated toward the next manager level (resets to the overflow on each level-up). </summary>
        [MetaMember(1)] public long Xp { get; set; }

        [MetaMember(2)] public int MatchesPlayed    { get; set; }
        [MetaMember(3)] public int MatchesWon       { get; set; }
        [MetaMember(4)] public int CurrentWinStreak { get; set; }
        [MetaMember(5)] public int BestWinStreak    { get; set; }

        public PlayerProgression() { }
    }
}
