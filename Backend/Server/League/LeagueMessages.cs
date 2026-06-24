// FOOTDRAFT — player ↔ LeagueActor server messages (create/join/leave/start/play/snapshot).

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Server
{
    /// <summary> Create a new league under <see cref="Code"/> with the caller as commissioner + first member. </summary>
    [MetaMessage(MessageCodes.LeagueCreateRequest, MessageDirection.ServerInternal)]
    public class LeagueCreateRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code       { get; private set; }
        public string LeagueName { get; private set; }
        public string PlayerName { get; private set; }
        public string Crest      { get; private set; }
        /// <summary> Single-player season: skip the lobby and start the draft immediately (CPU teams fill the league at season lock). </summary>
        public bool   Solo        { get; private set; }
        /// <summary> Hard mode: hide player ratings in this league's draft + roster UI. </summary>
        public bool   HideRatings { get; private set; }
        /// <summary> Rule: max players from one club (0 = no limit, 1 = one-per-club). </summary>
        public int    MaxPerClub  { get; private set; }
        /// <summary> Rule: chosen OVR-cap-bands ("90:2,80:3,75:4"); "" = no caps. </summary>
        public string CapBands    { get; private set; }
        /// <summary> Pin-draft House Rule ("era:E2010s,elite:1"); "" = no pin (see <see cref="Game.Logic.LeaguePin"/>). </summary>
        public string DraftPin    { get; private set; }

        LeagueCreateRequest() { }
        public LeagueCreateRequest(string code, string leagueName, string playerName, string crest, bool solo = false, bool hideRatings = false, int maxPerClub = 1, string capBands = "90:2,80:3,75:4", string draftPin = "")
        {
            Code        = code;
            LeagueName  = leagueName;
            PlayerName  = playerName;
            Crest       = crest;
            Solo        = solo;
            HideRatings = hideRatings;
            MaxPerClub  = maxPerClub;
            CapBands    = capBands ?? "";
            DraftPin    = draftPin ?? "";
        }
    }

    /// <summary> Join an existing league by <see cref="Code"/> (while it is still in the lobby and not full). </summary>
    [MetaMessage(MessageCodes.LeagueJoinRequest, MessageDirection.ServerInternal)]
    public class LeagueJoinRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code       { get; private set; }
        public string PlayerName { get; private set; }
        public string Crest      { get; private set; }

        LeagueJoinRequest() { }
        public LeagueJoinRequest(string code, string playerName, string crest)
        {
            Code       = code;
            PlayerName = playerName;
            Crest      = crest;
        }
    }

    /// <summary> Leave a league (only meaningful while it is still in the lobby). </summary>
    [MetaMessage(MessageCodes.LeagueLeaveRequest, MessageDirection.ServerInternal)]
    public class LeagueLeaveRequest : EntityAskRequest<EntityAskOk>
    {
        public string Code { get; private set; }

        LeagueLeaveRequest() { }
        public LeagueLeaveRequest(string code) { Code = code; }
    }

    /// <summary> Commissioner kicks off the season: locks each member's drafted XI and generates the fixtures. </summary>
    [MetaMessage(MessageCodes.LeagueStartRequest, MessageDirection.ServerInternal)]
    public class LeagueStartRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code { get; private set; }

        LeagueStartRequest() { }
        public LeagueStartRequest(string code) { Code = code; }
    }

    /// <summary> Play the caller's next unplayed fixture (their XI vs the opponent's locked XI snapshot). </summary>
    [MetaMessage(MessageCodes.LeaguePlayRequest, MessageDirection.ServerInternal)]
    public class LeaguePlayRequest : EntityAskRequest<LeaguePlayResponse>
    {
        public string Code { get; private set; }

        LeaguePlayRequest() { }
        public LeaguePlayRequest(string code) { Code = code; }
    }

    /// <summary> Fetch a fresh snapshot of the caller's league. </summary>
    [MetaMessage(MessageCodes.LeagueGetSnapshotRequest, MessageDirection.ServerInternal)]
    public class LeagueGetSnapshotRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code { get; private set; }

        LeagueGetSnapshotRequest() { }
        public LeagueGetSnapshotRequest(string code) { Code = code; }
    }

    /// <summary> Set the caller's drafting formation (lobby, or before their first pick). </summary>
    [MetaMessage(MessageCodes.LeagueSetFormationRequest, MessageDirection.ServerInternal)]
    public class LeagueSetFormationRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code        { get; private set; }
        public string FormationId { get; private set; }

        LeagueSetFormationRequest() { }
        public LeagueSetFormationRequest(string code, string formationId) { Code = code; FormationId = formationId; }
    }

    /// <summary> Draft a legend into the caller's XI (server enforces turn + uniqueness + open slot). </summary>
    [MetaMessage(MessageCodes.LeagueDraftPickRequest, MessageDirection.ServerInternal)]
    public class LeagueDraftPickRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code     { get; private set; }
        public string LegendId { get; private set; }

        LeagueDraftPickRequest() { }
        public LeagueDraftPickRequest(string code, string legendId) { Code = code; LegendId = legendId; }
    }

    /// <summary> Auto-pick for the current drafter; <see cref="FillAll"/> (commissioner) drafts the whole rest of the league. </summary>
    [MetaMessage(MessageCodes.LeagueAutoPickRequest, MessageDirection.ServerInternal)]
    public class LeagueAutoPickRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code    { get; private set; }
        public bool   FillAll { get; private set; }

        LeagueAutoPickRequest() { }
        public LeagueAutoPickRequest(string code, bool fillAll) { Code = code; FillAll = fillAll; }
    }

    /// <summary> Commissioner: simulate every remaining fixture at once and finish the season. </summary>
    [MetaMessage(MessageCodes.LeagueSimulateRequest, MessageDirection.ServerInternal)]
    public class LeagueSimulateRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code { get; private set; }

        LeagueSimulateRequest() { }
        public LeagueSimulateRequest(string code) { Code = code; }
    }

    /// <summary> Spin the wheel: roll a random Club × Season squad for the caller to pick a player from (their turn). </summary>
    [MetaMessage(MessageCodes.LeagueSpinRequest, MessageDirection.ServerInternal)]
    public class LeagueSpinRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code  { get; private set; }
        /// <summary> True = Gem-paid spin restricted to top-tier clubs (the caller's PlayerActor refunds on rejection). </summary>
        public bool   Elite { get; private set; }

        LeagueSpinRequest() { }
        public LeagueSpinRequest(string code, bool elite = false)
        {
            Code  = code;
            Elite = elite;
        }
    }

    // ---- Admin / dashboard requests (the LiveOps Dashboard "Season Leagues" page) ----

    /// <summary> List every league in the registry (flat, admin-wide view) for the dashboard. </summary>
    [MetaMessage(MessageCodes.LeagueListRequest, MessageDirection.ServerInternal)]
    public class LeagueListRequest : EntityAskRequest<LeagueListResponse>
    {
        public LeagueListRequest() { }
    }

    /// <summary> The flattened registry listing. </summary>
    [MetaMessage(MessageCodes.LeagueListRequest + 100_000, MessageDirection.ServerInternal)]
    public class LeagueListResponse : EntityAskResponse
    {
        public LeagueRegistryListView List { get; private set; }

        LeagueListResponse() { }
        public LeagueListResponse(LeagueRegistryListView list) { List = list; }
    }

    /// <summary> Admin (non-member) snapshot of one league by code. Reuses <see cref="LeagueOpResponse"/>. </summary>
    [MetaMessage(MessageCodes.LeagueAdminSnapshotRequest, MessageDirection.ServerInternal)]
    public class LeagueAdminSnapshotRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code { get; private set; }

        LeagueAdminSnapshotRequest() { }
        public LeagueAdminSnapshotRequest(string code) { Code = code; }
    }

    /// <summary> A manager swaps a drafted legend for another during an open transfer window. The fee was already charged from the player's wallet. </summary>
    [MetaMessage(MessageCodes.LeagueTransferSwapRequest, MessageDirection.ServerInternal)]
    public class LeagueTransferSwapRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code         { get; private set; }
        public string DropLegendId { get; private set; }
        public string AddLegendId  { get; private set; }
        /// <summary> True = the fee was charged in Gems (marquee signing); used only for the refund on rejection. </summary>
        public bool   PayWithGems  { get; private set; }

        LeagueTransferSwapRequest() { }
        public LeagueTransferSwapRequest(string code, string dropLegendId, string addLegendId, bool payWithGems = false)
        {
            Code         = code;
            DropLegendId = dropLegendId;
            AddLegendId  = addLegendId;
            PayWithGems  = payWithGems;
        }
    }

    /// <summary> Admin: open/close a league's transfer window (overrides the schedule). Dashboard write action. </summary>
    [MetaMessage(MessageCodes.LeagueSetTransferWindowRequest, MessageDirection.ServerInternal)]
    public class LeagueSetTransferWindowRequest : EntityAskRequest<LeagueOpResponse>
    {
        /// <summary> 0 = follow schedule, 1 = force open, 2 = force closed. </summary>
        public int    Override { get; private set; }
        public string Code     { get; private set; }

        LeagueSetTransferWindowRequest() { }
        public LeagueSetTransferWindowRequest(string code, int @override) { Code = code; Override = @override; }
    }

    /// <summary> Admin: force-advance a league's next matchday now. Dashboard write action. </summary>
    [MetaMessage(MessageCodes.LeagueAdminPlayMatchdayRequest, MessageDirection.ServerInternal)]
    public class LeagueAdminPlayMatchdayRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code { get; private set; }

        LeagueAdminPlayMatchdayRequest() { }
        public LeagueAdminPlayMatchdayRequest(string code) { Code = code; }
    }

    /// <summary> P2P: propose a player(+cash)-for-player trade to another manager in the league. </summary>
    [MetaMessage(MessageCodes.LeagueTradeOfferRequest, MessageDirection.ServerInternal)]
    public class LeagueTradeOfferRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code         { get; private set; }
        public int    ToIndex      { get; private set; } // the target manager's member index
        public string GiveLegendId { get; private set; } // a player from the proposer's roster
        public string GetLegendId  { get; private set; } // a player from the target's roster
        public int    Coins        { get; private set; } // cash the proposer adds (already escrowed from their wallet)

        LeagueTradeOfferRequest() { }
        public LeagueTradeOfferRequest(string code, int toIndex, string giveLegendId, string getLegendId, int coins)
        {
            Code = code; ToIndex = toIndex; GiveLegendId = giveLegendId; GetLegendId = getLegendId; Coins = coins;
        }
    }

    /// <summary> P2P: respond to a pending trade — the recipient accepts/rejects, or the proposer cancels (Accept=false). </summary>
    [MetaMessage(MessageCodes.LeagueTradeRespondRequest, MessageDirection.ServerInternal)]
    public class LeagueTradeRespondRequest : EntityAskRequest<LeagueOpResponse>
    {
        public string Code    { get; private set; }
        public int    OfferId { get; private set; }
        public bool   Accept  { get; private set; }

        LeagueTradeRespondRequest() { }
        public LeagueTradeRespondRequest(string code, int offerId, bool accept) { Code = code; OfferId = offerId; Accept = accept; }
    }

    /// <summary> Cast from the LeagueActor to a player's actor to grant/refund trade coins (delta &gt; 0 = grant). </summary>
    [MetaMessage(MessageCodes.LeagueAdjustCoinsMessage, MessageDirection.ServerInternal)]
    public class LeagueAdjustCoinsMessage : MetaMessage
    {
        public long Delta { get; private set; }

        LeagueAdjustCoinsMessage() { }
        public LeagueAdjustCoinsMessage(long delta) { Delta = delta; }
    }

    /// <summary> League op result: an error string ("" on success) plus the caller's fresh snapshot. </summary>
    [MetaMessage(MessageCodes.LeagueCreateRequest + 100_000, MessageDirection.ServerInternal)]
    public class LeagueOpResponse : EntityAskResponse
    {
        public string         Error    { get; private set; } = "";
        public LeagueSnapshot Snapshot { get; private set; }

        LeagueOpResponse() { }
        public LeagueOpResponse(string error, LeagueSnapshot snapshot)
        {
            Error    = error ?? "";
            Snapshot = snapshot;
        }
    }

    /// <summary> Result of playing a fixture: the scoreline, plus error + fresh snapshot. </summary>
    [MetaMessage(MessageCodes.LeaguePlayRequest + 100_000, MessageDirection.ServerInternal)]
    public class LeaguePlayResponse : EntityAskResponse
    {
        public string           Error    { get; private set; } = "";
        public LeaguePlayResult Result   { get; private set; }
        public LeagueSnapshot   Snapshot { get; private set; }

        LeaguePlayResponse() { }
        public LeaguePlayResponse(string error, LeaguePlayResult result, LeagueSnapshot snapshot)
        {
            Error    = error ?? "";
            Result   = result;
            Snapshot = snapshot;
        }
    }
}
