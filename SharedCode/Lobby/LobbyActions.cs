// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// Base class for all actions executed against the <see cref="LobbyModel"/>.
    /// </summary>
    [MetaSerializable]
    public abstract class LobbyAction : ModelAction<LobbyModel>
    {
    }

    /// <summary>
    /// Base class for server-originated lobby actions. The lobby state is mutated only by the server, so all
    /// lobby actions are server actions.
    /// </summary>
    [ModelActionExecuteFlags(ModelActionExecuteFlags.LeaderSynchronized)]
    public abstract class LobbyServerAction : LobbyAction
    {
    }

    /// <summary>
    /// Adds (or updates) a player in the matchmaking queue.
    /// </summary>
    [ModelAction(MatchActionCodes.LobbyAddQueuedPlayer)]
    public class LobbyAddQueuedPlayer : LobbyServerAction
    {
        public EntityId PlayerId   { get; private set; }
        public string   PlayerName { get; private set; }

        LobbyAddQueuedPlayer() { }
        public LobbyAddQueuedPlayer(EntityId playerId, string playerName)
        {
            PlayerId = playerId;
            PlayerName = playerName;
        }

        public override MetaActionResult InvokeExecute(LobbyModel lobby, bool commit)
        {
            if (commit)
            {
                lobby.QueuedPlayers[PlayerId] = PlayerName;
                lobby.ClientListener.QueueChanged();
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Removes a player from the matchmaking queue.
    /// </summary>
    [ModelAction(MatchActionCodes.LobbyRemoveQueuedPlayer)]
    public class LobbyRemoveQueuedPlayer : LobbyServerAction
    {
        public EntityId PlayerId { get; private set; }

        LobbyRemoveQueuedPlayer() { }
        public LobbyRemoveQueuedPlayer(EntityId playerId)
        {
            PlayerId = playerId;
        }

        public override MetaActionResult InvokeExecute(LobbyModel lobby, bool commit)
        {
            if (commit)
            {
                lobby.QueuedPlayers.Remove(PlayerId);
                lobby.ClientListener.QueueChanged();
            }
            return MetaActionResult.Success;
        }
    }

    /// <summary>
    /// Sets (or clears) the auto-start countdown. Pass <see cref="MetaTime.Epoch"/> to clear it.
    /// </summary>
    [ModelAction(MatchActionCodes.LobbySetCountdown)]
    public class LobbySetCountdown : LobbyServerAction
    {
        public MetaTime CountdownEndsAt { get; private set; }

        LobbySetCountdown() { }
        public LobbySetCountdown(MetaTime countdownEndsAt)
        {
            CountdownEndsAt = countdownEndsAt;
        }

        public override MetaActionResult InvokeExecute(LobbyModel lobby, bool commit)
        {
            if (commit)
            {
                lobby.CountdownEndsAt = CountdownEndsAt;
                lobby.ClientListener.QueueChanged();
            }
            return MetaActionResult.Success;
        }
    }
}
