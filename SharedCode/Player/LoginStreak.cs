// FOOTDRAFT — daily-login streak (WS4 retention). Tracked server-side on each session start; a single missed
// day is forgiven so one slip doesn't nuke a long streak. The reward scales with the streak length (capped).

using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> A manager's daily-login streak state. </summary>
    [MetaSerializable]
    public class LoginStreak
    {
        /// <summary> Days-since-epoch (UTC) of the last counted login; 0 = never. </summary>
        [MetaMember(1)] public long LastClaimDay  { get; set; }
        /// <summary> Current consecutive-day streak (with one forgiven gap). </summary>
        [MetaMember(2)] public int  CurrentStreak { get; set; }
        /// <summary> Best streak ever reached. </summary>
        [MetaMember(3)] public int  BestStreak    { get; set; }
        /// <summary> True once this streak's single forgiven missed day has been spent (resets with the streak). </summary>
        [MetaMember(4)] public bool ForgivenessUsed { get; set; }

        public LoginStreak() { }
    }

    /// <summary> Pure daily-login-streak logic (no time reads — the day index is passed in for determinism). </summary>
    public static class LoginStreakEngine
    {
        /// <summary>
        /// Records a login on <paramref name="today"/> (days since epoch). Returns true if this counts as a NEW
        /// day (so a reward is due). Forgiveness: a gap of one missed day keeps the streak going; a larger gap
        /// resets it to 1.
        /// </summary>
        public static bool Advance(LoginStreak streak, long today)
        {
            if (streak.CurrentStreak > 0 && streak.LastClaimDay >= today)
                return false; // already counted today (or clock skew — never count backwards)

            long gap = today - streak.LastClaimDay;
            if (streak.CurrentStreak == 0 || gap > 2 || (gap == 2 && streak.ForgivenessUsed))
            {
                streak.CurrentStreak   = 1;     // first login ever, or the streak broke
                streak.ForgivenessUsed = false; // a fresh streak gets a fresh forgiveness
            }
            else
            {
                if (gap == 2)
                    streak.ForgivenessUsed = true; // spend the streak's single forgiven missed day
                streak.CurrentStreak += 1;
            }

            streak.LastClaimDay = today;
            if (streak.CurrentStreak > streak.BestStreak)
                streak.BestStreak = streak.CurrentStreak;
            return true;
        }
    }
}
