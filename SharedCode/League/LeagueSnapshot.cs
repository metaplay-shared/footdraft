// FOOTDRAFT — season-league view + player-cached state (the data the LeagueHub renders).
//
// A private league of up to 20 managers, each drafting an XI then playing a double round-robin (38 matchdays
// for 20). League state lives in the singleton LeagueActor; the player caches the latest snapshot here for
// display (mirrors how PlayerClub caches the Club League standings).

using System.Collections.Generic;
using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    [MetaSerializable]
    public enum LeagueState
    {
        Lobby    = 0, // gathering managers; commissioner hasn't kicked off
        Active   = 1, // season in progress
        Finished = 2, // all fixtures played
        Drafting = 3, // managers taking turns drafting their XIs from the shared legend pool
    }

    /// <summary> A league member as shown in the UI. </summary>
    [MetaSerializable]
    public class LeagueMemberView
    {
        [MetaMember(1)] public int    Index          { get; set; }
        [MetaMember(2)] public string Name           { get; set; }
        [MetaMember(3)] public string Crest          { get; set; }
        /// <summary> The formation this manager is drafting into (e.g. "4-3-3"); empty until chosen. </summary>
        [MetaMember(4)] public string FormationName  { get; set; } = "";
        /// <summary> How many of their 11 XI slots this manager has drafted so far. </summary>
        [MetaMember(5)] public int    PicksCount     { get; set; }
        /// <summary> True once this manager's XI is fully drafted (11/11). </summary>
        [MetaMember(6)] public bool   RosterComplete { get; set; }
        /// <summary> True if this team is a CPU/bot filling the 20-team league. </summary>
        [MetaMember(7)] public bool   IsBot          { get; set; }

        public LeagueMemberView() { }
        public LeagueMemberView(int index, string name, string crest)
        {
            Index = index;
            Name  = name;
            Crest = crest;
        }
    }

    /// <summary> A snapshot of a league for one viewing manager — standings, members, their next fixture. </summary>
    [MetaSerializable]
    public class LeagueSnapshot
    {
        [MetaMember(1)]  public string                Code           { get; set; } = "";
        [MetaMember(2)]  public string                Name           { get; set; } = "";
        [MetaMember(3)]  public LeagueState           State          { get; set; } = LeagueState.Lobby;
        [MetaMember(4)]  public int                   MyIndex        { get; set; } = -1;
        [MetaMember(5)]  public bool                  IsCommissioner { get; set; }
        [MetaMember(6)]  public List<LeagueMemberView> Members       { get; set; } = new List<LeagueMemberView>();
        [MetaMember(7)]  public List<LeagueRow>       Table          { get; set; } = new List<LeagueRow>();
        [MetaMember(8)]  public int                   FixturesPlayed { get; set; }
        [MetaMember(9)]  public int                   FixturesTotal  { get; set; }
        [MetaMember(10)] public bool                  HasNextFixture { get; set; }
        [MetaMember(11)] public string                NextOpponent   { get; set; } = "";
        [MetaMember(12)] public bool                  NextIsHome     { get; set; }
        [MetaMember(13)] public bool                  Invincible     { get; set; } // the viewing manager went 38-0

        // ---- Draft phase (State == Drafting): the turn-based snake draft from the shared legend pool. ----
        /// <summary> Member index whose turn it is to draft, or -1 when not drafting. </summary>
        [MetaMember(14)] public int                   CurrentDrafterIndex { get; set; } = -1;
        /// <summary> True when it is the viewing manager's turn to pick. </summary>
        [MetaMember(15)] public bool                  IsMyTurn            { get; set; }
        /// <summary> Global pick number drafted so far (0-based). </summary>
        [MetaMember(16)] public int                   DraftPick           { get; set; }
        /// <summary> Total picks in the draft (managers × 11). </summary>
        [MetaMember(17)] public int                   DraftTotalPicks     { get; set; }
        /// <summary> 1-based draft round (round 1 = everyone's first pick). </summary>
        [MetaMember(18)] public int                   DraftRound          { get; set; }
        /// <summary> Player NAMES already on some team (shared-pool uniqueness is by name) — filters draft/transfer pick lists. </summary>
        [MetaMember(19)] public List<string>          TakenNames          { get; set; } = new List<string>();
        /// <summary> The viewing manager's drafted XI: formation slot index → legend id. </summary>
        [MetaMember(20)] public MetaDictionary<int, string> MyRoster       { get; set; } = new MetaDictionary<int, string>();
        /// <summary> The viewing manager's formation id value (e.g. "4-3-3"). </summary>
        [MetaMember(21)] public string                MyFormation         { get; set; } = "";
        /// <summary> Name of the manager whose turn it is (for the "X is picking…" banner). </summary>
        [MetaMember(22)] public string                CurrentDrafterName  { get; set; } = "";

        // ---- Daily matchday schedule (State == Active/Finished): one matchday simmed per day at 7pm. ----
        /// <summary> Matchdays already played (0..TotalMatchdays). </summary>
        [MetaMember(23)] public int                   CurrentMatchday { get; set; }
        /// <summary> Total matchdays in the season (38 for a full 20-team league). </summary>
        [MetaMember(24)] public int                   TotalMatchdays  { get; set; }
        /// <summary> Unix-ms timestamp of the next scheduled matchday sim (0 if none / season finished). </summary>
        [MetaMember(25)] public long                  NextSimAtMillis { get; set; }
        /// <summary> The most-recently-played matchday number (1-based; 0 if none yet). </summary>
        [MetaMember(26)] public int                   LastMatchdayNumber { get; set; }
        /// <summary> Scorelines of the most recently played matchday, e.g. "Guest A 2–1 CPU United". </summary>
        [MetaMember(27)] public List<string>          LastMatchdayLines  { get; set; } = new List<string>();

        /// <summary> The squad the viewing manager just spun (Club × Season) to pick from, or null if they haven't spun. </summary>
        [MetaMember(28)] public SpunSquadView         MySpin             { get; set; }

        // ---- Transfer windows (the metagame between match days) ----
        /// <summary> Whether the league's transfer window is currently open (schedule or admin override). </summary>
        [MetaMember(29)] public bool                  TransferWindowOpen { get; set; }
        /// <summary> The viewing manager's remaining transfer budget (Coins); 0 for spectators/bots. </summary>
        [MetaMember(30)] public long                  MyTransferBudget   { get; set; }

        // ---- Squad-building rules (per-league hard-mode + draft constraints) ----
        /// <summary> Hard mode: hide player OVR ratings in the draft + roster UI for this league. </summary>
        [MetaMember(31)] public bool                  HideRatings        { get; set; }
        /// <summary> Browsable season results (most recent fixtures, all members) with named scorers — "view past results". </summary>
        [MetaMember(32)] public List<LeagueResultLine> SeasonResults     { get; set; } = new List<LeagueResultLine>();
        /// <summary> Rule: no two players from the same club in one XI (chosen at creation). </summary>
        [MetaMember(33)] public bool                  NoSameClub         { get; set; }
        /// <summary> Rule: enforce the OVR-band squad caps (chosen at creation). [legacy — superseded by CapBands] </summary>
        [MetaMember(34)] public bool                  SquadCaps          { get; set; }
        /// <summary> Max players allowed from one club (0 = no limit; 1 = one-per-club). Supersedes NoSameClub. </summary>
        [MetaMember(35)] public int                   MaxPerClub         { get; set; }
        /// <summary> Chosen OVR-cap-bands string ("90:2,80:3,75:4"); "" = no caps. Supersedes SquadCaps. </summary>
        [MetaMember(36)] public string                CapBands           { get; set; } = "";
        /// <summary> Pin-draft House Rule ("era:E2010s,elite:1"); "" = no pin (see <see cref="LeaguePin"/>). </summary>
        [MetaMember(37)] public string                DraftPin           { get; set; } = "";
        /// <summary> The viewing manager's pending P2P trade offers (incoming to accept/reject + outgoing to cancel). </summary>
        [MetaMember(38)] public List<TradeOfferView>  MyTradeOffers      { get; set; } = new List<TradeOfferView>();
        /// <summary> Other HUMAN managers' rosters (legend ids; client resolves names) — the trade-proposal picker. </summary>
        [MetaMember(39)] public List<LeagueRosterView> TradeRosters      { get; set; } = new List<LeagueRosterView>();
    }

    /// <summary> Another manager's roster as legend ids (the client resolves names/OVR/position), for the trade picker. </summary>
    [MetaSerializable]
    public class LeagueRosterView
    {
        [MetaMember(1)] public int          MemberIndex { get; set; }
        [MetaMember(2)] public string       Name        { get; set; } = "";
        [MetaMember(3)] public List<string> LegendIds   { get; set; } = new List<string>();
    }

    /// <summary>
    /// A pending P2P trade as shown to one viewing manager. Fields are from the PROPOSER's perspective:
    /// the proposer gives <see cref="GiveName"/> (+ <see cref="Coins"/>) for the other manager's <see cref="GetName"/>.
    /// <see cref="Incoming"/> = the viewer is the recipient (can accept/reject); else it's the viewer's own offer (cancel).
    /// </summary>
    [MetaSerializable]
    public class TradeOfferView
    {
        [MetaMember(1)]  public int      OfferId   { get; set; }
        [MetaMember(2)]  public bool     Incoming  { get; set; }
        [MetaMember(3)]  public string   OtherName { get; set; } = ""; // the other manager in the trade
        [MetaMember(4)]  public string   GiveName  { get; set; } = "";
        [MetaMember(5)]  public int      GiveOvr   { get; set; }
        [MetaMember(6)]  public Position GivePos   { get; set; }
        [MetaMember(7)]  public string   GetName   { get; set; } = "";
        [MetaMember(8)]  public int      GetOvr    { get; set; }
        [MetaMember(9)]  public Position GetPos    { get; set; }
        [MetaMember(10)] public int      Coins     { get; set; } // proposer adds this much cash to the deal
    }

    /// <summary> One player in a spun squad, with draft-availability flags for the picking manager. </summary>
    [MetaSerializable]
    public class SpunPlayer
    {
        [MetaMember(1)] public string   LegendId     { get; set; }
        [MetaMember(2)] public string   Name         { get; set; }
        [MetaMember(3)] public Position Position     { get; set; }
        [MetaMember(4)] public int      Ovr          { get; set; }
        [MetaMember(5)] public string   Nation       { get; set; }
        [MetaMember(6)] public bool     Taken        { get; set; } // already on another team
        [MetaMember(7)] public bool     FitsOpenSlot { get; set; } // matches one of the manager's open positions
    }

    /// <summary> A spun Club × Season squad offered to the current drafter. </summary>
    [MetaSerializable]
    public class SpunSquadView
    {
        [MetaMember(1)] public string           Club    { get; set; } = "";
        [MetaMember(2)] public string           Season  { get; set; } = "";
        [MetaMember(3)] public List<SpunPlayer> Players { get; set; } = new List<SpunPlayer>();
    }

    /// <summary> The outcome of playing a single league fixture, surfaced to the UI as a popup. </summary>
    [MetaSerializable]
    public class LeaguePlayResult
    {
        [MetaMember(1)] public string OpponentName { get; set; } = "";
        [MetaMember(2)] public bool   Home         { get; set; }
        [MetaMember(3)] public int    MyGoals      { get; set; }
        [MetaMember(4)] public int    OppGoals     { get; set; }
        [MetaMember(5)] public int    Outcome      { get; set; } // 1 win, 0 draw, -1 loss
        /// <summary> The fixture's named goals (scorer + assister), for the cinematic + full-time detail. </summary>
        [MetaMember(6)] public List<MatchGoalDetail> Goals { get; set; } = new List<MatchGoalDetail>();
        /// <summary> A flavour "match report" headline for this fixture (shown at full-time). </summary>
        [MetaMember(7)] public string Report { get; set; } = "";
    }

    /// <summary> One row in the browsable season results history (scoreline + named scorers), shown to all members. </summary>
    [MetaSerializable]
    public class LeagueResultLine
    {
        [MetaMember(1)] public int    Matchday  { get; set; }
        [MetaMember(2)] public string HomeName  { get; set; } = "";
        [MetaMember(3)] public string AwayName  { get; set; } = "";
        [MetaMember(4)] public int    HomeGoals { get; set; }
        [MetaMember(5)] public int    AwayGoals { get; set; }
        [MetaMember(6)] public List<MatchGoalDetail> Goals { get; set; } = new List<MatchGoalDetail>();
    }

    /// <summary> A manager's cached league membership + latest standings snapshot. </summary>
    [MetaSerializable]
    public class PlayerLeague
    {
        [MetaMember(1)] public string           Code           { get; set; } = "";
        [MetaMember(2)] public LeagueSnapshot    Snapshot       { get; set; }
        [MetaMember(3)] public string            LastError      { get; set; } = "";
        [MetaMember(4)] public LeaguePlayResult  LastPlayResult { get; set; }

        public bool InLeague => !string.IsNullOrEmpty(Code);
    }

    /// <summary> A league invite code: same shape as a friendly room code (4–8 uppercase alphanumerics). </summary>
    public static class LeagueCode
    {
        public static bool IsValid(string code) => FriendlyCode.IsValid(code);
    }
}
