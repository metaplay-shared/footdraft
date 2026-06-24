// FOOTDRAFT — World Cup leaderboard: the shared snapshot the client caches + renders, plus the pure ranking
// rule the server actor and tests share. Ranks managers by their best WC run: titles, then deepest round
// reached, then best-XI overall, then fewer runs (efficiency tie-break).

using System.Collections.Generic;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> One ranked manager on the World Cup leaderboard. </summary>
    [MetaSerializable]
    public class WcLeaderboardEntry
    {
        [MetaMember(1)] public int    Rank      { get; set; }
        [MetaMember(2)] public string Name      { get; set; } = "";
        [MetaMember(3)] public int    Titles    { get; set; }
        [MetaMember(4)] public int    BestRound { get; set; } = -1; // 0 = R32 … (rounds-1) = champion
        [MetaMember(5)] public int    BestXiOvr { get; set; }
        [MetaMember(6)] public int    Runs      { get; set; }

        public WcLeaderboardEntry() { }
        public WcLeaderboardEntry(string name, int titles, int bestRound, int bestXiOvr, int runs)
        {
            Name = name; Titles = titles; BestRound = bestRound; BestXiOvr = bestXiOvr; Runs = runs;
        }
    }

    /// <summary> A cached top-N World Cup leaderboard + where the viewing manager sits. </summary>
    [MetaSerializable]
    public class WorldCupLeaderboardSnapshot
    {
        [MetaMember(1)] public List<WcLeaderboardEntry> Top          { get; set; } = new List<WcLeaderboardEntry>();
        [MetaMember(2)] public int                      MyRank       { get; set; } // 0 = unranked
        [MetaMember(3)] public int                      TotalPlayers { get; set; }
        [MetaMember(4)] public int                      MyTitles     { get; set; }
        [MetaMember(5)] public int                      MyBestRound  { get; set; } = -1;
    }

    /// <summary> Pure leaderboard ranking — shared so the actor + tests order identically. </summary>
    public static class WcLeaderboard
    {
        /// <summary> Sorts entries best-first and assigns 1-based <see cref="WcLeaderboardEntry.Rank"/>. </summary>
        public static List<WcLeaderboardEntry> Rank(List<WcLeaderboardEntry> entries)
        {
            entries.Sort(Compare);
            for (int i = 0; i < entries.Count; i++)
                entries[i].Rank = i + 1;
            return entries;
        }

        public static int Compare(WcLeaderboardEntry a, WcLeaderboardEntry b)
        {
            if (a.Titles != b.Titles)       return b.Titles - a.Titles;        // most titles first
            if (a.BestRound != b.BestRound) return b.BestRound - a.BestRound;  // then deepest round
            if (a.BestXiOvr != b.BestXiOvr) return b.BestXiOvr - a.BestXiOvr;  // then strongest XI
            if (a.Runs != b.Runs)           return a.Runs - b.Runs;            // fewer runs = more efficient
            return string.CompareOrdinal(a.Name ?? "", b.Name ?? "");
        }
    }
}
