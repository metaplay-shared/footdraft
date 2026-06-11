// FOOTDRAFT — club & club-league shared types.

using Metaplay.Core;
using Metaplay.Core.Model;
using System.Collections.Generic;

namespace Game.Logic
{
    /// <summary>
    /// The weekly Club League schedule: contiguous <see cref="GlobalConfig.ClubLeagueWindowDays"/>-day windows
    /// (default 7 → a fresh league week), each with a stable id. When it rolls over, clubs' weekly points reset.
    /// </summary>
    public static class ClubWeek
    {
        static long WindowMs(GlobalConfig global) => (long)global.ClubLeagueWindowDays * 24L * 60L * 60L * 1000L;

        public static long CurrentWeekId(MetaTime now, GlobalConfig global)
        {
            long windowMs = WindowMs(global);
            return windowMs <= 0 ? 0 : now.MillisecondsSinceEpoch / windowMs;
        }

        public static MetaTime WeekEndsAt(long weekId, GlobalConfig global)
            => MetaTime.FromMillisecondsSinceEpoch((weekId + 1) * WindowMs(global));
    }

    /// <summary> One club's standing in the Club League leaderboard (for display). </summary>
    [MetaSerializable]
    public class ClubStanding
    {
        [MetaMember(1)] public string Name    { get; private set; }
        [MetaMember(2)] public long   Points  { get; private set; }
        [MetaMember(3)] public int    Members { get; private set; }

        public ClubStanding() { }
        public ClubStanding(string name, long points, int members)
        {
            Name    = name;
            Points  = points;
            Members = members;
        }
    }

    /// <summary>
    /// A snapshot of the Club League the player can observe (their club's standing plus the top clubs). Refreshed
    /// by the server from the <c>ClubsActor</c>; slightly stale by design, which is fine for a standings board.
    /// </summary>
    [MetaSerializable]
    public class ClubSnapshot
    {
        [MetaMember(1)] public long              WeekId      { get; set; }
        [MetaMember(2)] public long              MyPoints    { get; set; }
        [MetaMember(3)] public int               MyRank      { get; set; }
        [MetaMember(4)] public int               TotalClubs  { get; set; }
        [MetaMember(5)] public int               MemberCount { get; set; }
        [MetaMember(6)] public List<ClubStanding> Top        { get; set; } = new List<ClubStanding>();

        public ClubSnapshot() { }
    }
}
