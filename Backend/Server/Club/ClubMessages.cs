// FOOTDRAFT — club ↔ ClubsActor server messages.

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Server
{
    /// <summary> A player joins (or creates) a club by name; the actor removes them from any other club first. </summary>
    [MetaMessage(MessageCodes.ClubJoinRequest, MessageDirection.ServerInternal)]
    public class ClubJoinRequest : EntityAskRequest<EntityAskOk>
    {
        public string PlayerName { get; private set; }
        public string ClubName   { get; private set; }

        ClubJoinRequest() { }
        public ClubJoinRequest(string playerName, string clubName)
        {
            PlayerName = playerName;
            ClubName   = clubName;
        }
    }

    /// <summary> A player leaves their club. </summary>
    [MetaMessage(MessageCodes.ClubLeaveRequest, MessageDirection.ServerInternal)]
    public class ClubLeaveRequest : EntityAskRequest<EntityAskOk>
    {
        public ClubLeaveRequest() { }
    }

    /// <summary> Fire-and-forget: add a match's Club Points to the player's club for the current league week. </summary>
    [MetaMessage(MessageCodes.ClubReportPoints, MessageDirection.ServerInternal)]
    public class ClubReportPoints : MetaMessage
    {
        public string ClubName { get; private set; }
        public int    Points   { get; private set; }
        public long   WeekId   { get; private set; }

        ClubReportPoints() { }
        public ClubReportPoints(string clubName, int points, long weekId)
        {
            ClubName = clubName;
            Points   = points;
            WeekId   = weekId;
        }
    }

    /// <summary> Fetch the Club League standings snapshot for a player's club. </summary>
    [MetaMessage(MessageCodes.ClubGetSnapshotRequest, MessageDirection.ServerInternal)]
    public class ClubGetSnapshotRequest : EntityAskRequest<ClubGetSnapshotResponse>
    {
        public string ClubName { get; private set; }
        public long   WeekId   { get; private set; }
        public int    TopN     { get; private set; }

        ClubGetSnapshotRequest() { }
        public ClubGetSnapshotRequest(string clubName, long weekId, int topN)
        {
            ClubName = clubName;
            WeekId   = weekId;
            TopN     = topN;
        }
    }

    /// <summary> The standings snapshot for the requesting player's club. </summary>
    [MetaMessage(MessageCodes.ClubGetSnapshotRequest + 100_000, MessageDirection.ServerInternal)]
    public class ClubGetSnapshotResponse : EntityAskResponse
    {
        public ClubSnapshot Snapshot { get; private set; }

        ClubGetSnapshotResponse() { }
        public ClubGetSnapshotResponse(ClubSnapshot snapshot) { Snapshot = snapshot; }
    }
}
