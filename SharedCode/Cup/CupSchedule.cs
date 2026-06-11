// FOOTDRAFT — the twice-daily Cup schedule and milestone reward definitions.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// One milestone on a Cup's reward track: reach <see cref="Tokens"/> Cup Tokens during the Cup to claim the
    /// reward. Defined in <see cref="GlobalConfig.CupMilestones"/> so it's designer/LiveOps tunable.
    /// </summary>
    [MetaSerializable]
    public class CupMilestone
    {
        [MetaMember(1)] public int Tokens { get; private set; }
        [MetaMember(2)] public int Coins  { get; private set; }
        [MetaMember(3)] public int Gems   { get; private set; }
        [MetaMember(4)] public int Shards { get; private set; }

        public CupMilestone() { }
        public CupMilestone(int tokens, int coins, int gems, int shards)
        {
            Tokens = tokens;
            Coins  = coins;
            Gems   = gems;
            Shards = shards;
        }
    }

    /// <summary>
    /// The recurring Cup schedule. Cups are contiguous, back-to-back windows of
    /// <see cref="GlobalConfig.CupWindowHours"/> (default 12h → a fresh Cup twice a day at 00:00 / 12:00 UTC),
    /// so there is always exactly one live Cup. Each window has a stable integer id derived from wall-clock time;
    /// when it rolls over, a player's Cup Tokens and claimed milestones reset. (This is the v1 stand-in for a
    /// dashboard-scheduled LiveOps Event — the cadence the headline twice-daily Cup is built around.)
    /// </summary>
    public static class CupSchedule
    {
        static long WindowMs(GlobalConfig global) => (long)global.CupWindowHours * 60L * 60L * 1000L;

        /// <summary> The id of the Cup live at <paramref name="now"/>. </summary>
        public static long CurrentCupId(MetaTime now, GlobalConfig global)
        {
            long windowMs = WindowMs(global);
            if (windowMs <= 0)
                return 0;
            return now.MillisecondsSinceEpoch / windowMs;
        }

        /// <summary> When the Cup with <paramref name="cupId"/> ends (and the next begins). </summary>
        public static MetaTime CupEndsAt(long cupId, GlobalConfig global)
            => MetaTime.FromMillisecondsSinceEpoch((cupId + 1) * WindowMs(global));

        /// <summary> A short human label for a Cup id (e.g. "Cup #1234"). </summary>
        public static string CupLabel(long cupId) => $"Cup #{cupId}";
    }
}
