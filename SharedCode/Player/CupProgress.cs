// FOOTDRAFT — a player's progress in the current twice-daily Cup.

using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// A player's progress in the current Cup: which Cup window it tracks, the Cup Tokens earned this window, and
    /// how many milestones have been claimed. When the Cup rolls over (a new window starts), this resets — see
    /// <see cref="PlayerModel.SyncCup"/>.
    /// </summary>
    [MetaSerializable]
    public class CupProgress
    {
        [MetaMember(1)] public long CupId             { get; set; } = -1;
        [MetaMember(2)] public int  Tokens            { get; set; }
        [MetaMember(3)] public int  ClaimedMilestones { get; set; }

        public CupProgress() { }
    }
}
