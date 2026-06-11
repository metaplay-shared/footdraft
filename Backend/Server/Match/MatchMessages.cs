// FOOTDRAFT — match ↔ player server messages (squad fetch + reward grant).

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Server
{
    /// <summary>
    /// Sent by the MatchActor to a participating PlayerActor at match setup to fetch the player's drafted-XI
    /// line ratings (resolved server-authoritatively from their <c>DraftedSquad</c>), so the match reflects the
    /// team they drafted.
    /// </summary>
    [MetaMessage(MessageCodes.PlayerGetSquadRequest, MessageDirection.ServerInternal)]
    public class PlayerGetSquadRequest : EntityAskRequest<PlayerGetSquadResponse>
    {
        public PlayerGetSquadRequest() { }
    }

    /// <summary> Response to <see cref="PlayerGetSquadRequest"/>: the drafted XI's line ratings + manager level. </summary>
    [MetaMessage(MessageCodes.PlayerGetSquadRequest + 100_000, MessageDirection.ServerInternal)]
    public class PlayerGetSquadResponse : EntityAskResponse
    {
        public LineRatings Ratings      { get; private set; }
        public string      Crest        { get; private set; }
        public int         ManagerLevel { get; private set; }

        PlayerGetSquadResponse() { }
        public PlayerGetSquadResponse(LineRatings ratings, string crest, int managerLevel)
        {
            Ratings      = ratings;
            Crest        = crest;
            ManagerLevel = managerLevel;
        }
    }

    /// <summary>
    /// Fire-and-forget message from the MatchActor to a participating PlayerActor when a match ends, telling it
    /// whether the player won so it can grant the configured rewards. Cast (not an ask) because the match doesn't
    /// need a reply and the player may grant the rewards on its own timeline (even after reconnecting).
    /// </summary>
    [MetaMessage(MessageCodes.GrantMatchRewards, MessageDirection.ServerInternal)]
    public class GrantMatchRewardsMessage : MetaMessage
    {
        public bool Won       { get; private set; }
        public int  RoundsWon { get; private set; }

        GrantMatchRewardsMessage() { }
        public GrantMatchRewardsMessage(bool won, int roundsWon)
        {
            Won       = won;
            RoundsWon = roundsWon;
        }
    }
}
