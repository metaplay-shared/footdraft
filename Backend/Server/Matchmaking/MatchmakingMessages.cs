// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Server
{
    /// <summary>
    /// Registry for game-specific message codes. Kept separate from the SDK's core message codes; the
    /// game range starts at 10_000.
    /// </summary>
    public static class MessageCodes
    {
        public const int LobbyEnqueuePlayerRequest  = 10_001;
        public const int PlayerAssignToMatchRequest = 10_002;
        public const int LobbyStartNowRequest       = 10_003;
        public const int PlayerGetSquadRequest      = 10_004;
        public const int GrantMatchRewards          = 10_005;
        public const int LobbyCreateFriendlyRequest = 10_006;
        public const int LobbyJoinFriendlyRequest   = 10_007;
        public const int LobbyCancelFriendlyRequest = 10_008;
        public const int ClubJoinRequest            = 10_009;
        public const int ClubLeaveRequest           = 10_010;
        public const int ClubReportPoints           = 10_011;
        public const int ClubGetSnapshotRequest     = 10_012;
        public const int FormSetRequest             = 10_013;
        public const int FormClearRequest           = 10_014;
        public const int FormGetRequest             = 10_015;
        public const int LeagueCreateRequest        = 10_016;
        public const int LeagueJoinRequest          = 10_017;
        public const int LeagueLeaveRequest         = 10_018;
        public const int LeagueStartRequest         = 10_019;
        public const int LeaguePlayRequest          = 10_020;
        public const int LeagueGetSnapshotRequest   = 10_021;
        public const int LeagueSetFormationRequest  = 10_022;
        public const int LeagueDraftPickRequest     = 10_023;
        public const int LeagueAutoPickRequest      = 10_024;
        public const int LeagueSimulateRequest      = 10_025;
        public const int LeagueSpinRequest          = 10_026;
        // --- Admin / dashboard (LiveOps Dashboard "Season Leagues" page) ---
        public const int LeagueListRequest            = 10_027; // list every league in the registry (read)
        public const int LeagueAdminSnapshotRequest   = 10_028; // admin (non-member) snapshot of one league (read)
        public const int LeagueAdminPlayMatchdayRequest = 10_029; // force-advance one matchday (write)
        public const int LeagueSetTransferWindowRequest  = 10_030; // open/close a league's transfer window (write)
        public const int LeagueTransferSwapRequest       = 10_031; // a manager swaps a drafted player during the window
        public const int WcLeaderboardReport             = 10_032; // a manager reports a finished World Cup run (cast)
        public const int WcLeaderboardGetSnapshotRequest = 10_033; // fetch the top-N World Cup leaderboard (read)
        public const int LeagueTradeOfferRequest         = 10_034; // P2P: propose a player+cash trade to a leaguemate
        public const int LeagueTradeRespondRequest       = 10_035; // P2P: accept/reject/cancel a pending trade offer
        public const int LeagueAdjustCoinsMessage        = 10_036; // P2P: league grants/refunds a player's coins (cast)
    }

    /// <summary> A player opens a private room under <see cref="Code"/>. </summary>
    [MetaMessage(MessageCodes.LobbyCreateFriendlyRequest, MessageDirection.ServerInternal)]
    public class LobbyCreateFriendlyRequest : EntityAskRequest<EntityAskOk>
    {
        public string PlayerName { get; private set; }
        public string Code       { get; private set; }

        LobbyCreateFriendlyRequest() { }
        public LobbyCreateFriendlyRequest(string playerName, string code)
        {
            PlayerName = playerName;
            Code       = code;
        }
    }

    /// <summary> A player joins a private room by <see cref="Code"/>; the lobby forms the match if the room exists. </summary>
    [MetaMessage(MessageCodes.LobbyJoinFriendlyRequest, MessageDirection.ServerInternal)]
    public class LobbyJoinFriendlyRequest : EntityAskRequest<EntityAskOk>
    {
        public string PlayerName { get; private set; }
        public string Code       { get; private set; }

        LobbyJoinFriendlyRequest() { }
        public LobbyJoinFriendlyRequest(string playerName, string code)
        {
            PlayerName = playerName;
            Code       = code;
        }
    }

    /// <summary> A player closes any private room they created. </summary>
    [MetaMessage(MessageCodes.LobbyCancelFriendlyRequest, MessageDirection.ServerInternal)]
    public class LobbyCancelFriendlyRequest : EntityAskRequest<EntityAskOk>
    {
        public LobbyCancelFriendlyRequest() { }
    }

    /// <summary>
    /// Sent by a PlayerActor to the lobby singleton to add the player to the matchmaking queue.
    /// </summary>
    [MetaMessage(MessageCodes.LobbyEnqueuePlayerRequest, MessageDirection.ServerInternal)]
    public class LobbyEnqueuePlayerRequest : EntityAskRequest<EntityAskOk>
    {
        public string PlayerName { get; private set; }

        LobbyEnqueuePlayerRequest() { }
        public LobbyEnqueuePlayerRequest(string playerName)
        {
            PlayerName = playerName;
        }
    }

    /// <summary>
    /// Sent by the lobby to a PlayerActor to inform it that the player has been placed into a match.
    /// The PlayerActor updates its match association in response.
    /// </summary>
    [MetaMessage(MessageCodes.PlayerAssignToMatchRequest, MessageDirection.ServerInternal)]
    public class PlayerAssignToMatchRequest : EntityAskRequest<EntityAskOk>
    {
        public EntityId MatchId { get; private set; }

        PlayerAssignToMatchRequest() { }
        public PlayerAssignToMatchRequest(EntityId matchId)
        {
            MatchId = matchId;
        }
    }

    /// <summary>
    /// Sent by a queued player's PlayerActor to force the lobby to start a match immediately, padding with
    /// bots up to the minimum player count.
    /// </summary>
    [MetaMessage(MessageCodes.LobbyStartNowRequest, MessageDirection.ServerInternal)]
    public class LobbyStartNowRequest : EntityAskRequest<EntityAskOk>
    {
        public LobbyStartNowRequest() { }
    }
}
