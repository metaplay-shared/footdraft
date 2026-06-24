// FOOTDRAFT — the manager's trophy cabinet + all-time peaks (drives the profile screen + achievements).

using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// Lifetime honours: trophies won across every mode, deepest rounds reached, and all-time peaks. Updated
    /// server-authoritatively from the cup/league play actions (a Champion result bumps a title; each round
    /// tracks the best reached). Lifetime stats that drive streaks/leaderboards live on <see cref="PlayerProgression"/>;
    /// this is the "what have I actually won" record the manager profile shows off.
    /// </summary>
    [MetaSerializable]
    public class PlayerHonours
    {
        // --- World Cup 2026 ---
        [MetaMember(1)]  public int WorldCupTitles    { get; set; }
        [MetaMember(2)]  public int WorldCupBestRound { get; set; } = -1; // 0 = R32 … (rounds-1) = won the final
        [MetaMember(3)]  public int WorldCupRuns      { get; set; }

        // --- Draft Cup ---
        [MetaMember(4)]  public int DraftCupTitles    { get; set; }
        [MetaMember(5)]  public int DraftCupBestRound { get; set; } = -1;
        [MetaMember(6)]  public int DraftCupRuns      { get; set; }

        // --- Weekly Bracket Cup ---
        [MetaMember(7)]  public int BracketTitles     { get; set; }
        [MetaMember(8)]  public int BracketBestRound  { get; set; } = -1;

        // --- Season league ---
        [MetaMember(9)]  public int LeagueTitles        { get; set; }
        [MetaMember(10)] public int LeagueSeasonsPlayed { get; set; }
        [MetaMember(11)] public int InvincibleSeasons   { get; set; } // finished a season unbeaten (38-0)
        /// <summary> Guards league-finish honours against double-counting on snapshot refresh (last counted league code). </summary>
        [MetaMember(12)] public string LastScoredLeagueCode { get; set; } = "";

        // --- All-time peaks ---
        /// <summary> Highest overall of any XI the manager has taken into a knockout (any mode). </summary>
        [MetaMember(13)] public int BestDraftedXiOvr { get; set; }
        /// <summary> Best ranked-ladder division index ever reached (peaks across seasons; Rank.BestDivisionIndex resets). </summary>
        [MetaMember(14)] public int BestRankDivision { get; set; }

        public PlayerHonours() { }

        /// <summary> Total trophies across every mode — the headline number on the profile. </summary>
        public int TotalTrophies => WorldCupTitles + DraftCupTitles + BracketTitles + LeagueTitles;
    }
}
