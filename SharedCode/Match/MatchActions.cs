// FOOTDRAFT — match actions.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// Registry for match (and lobby) action codes. Action codes are global across the whole game, so these
    /// must not collide with <see cref="ActionCodes"/> used by player actions.
    /// </summary>
    public static class MatchActionCodes
    {
        // Lobby actions (6001-6099)
        public const int LobbyAddQueuedPlayer    = 6001;
        public const int LobbyRemoveQueuedPlayer  = 6002;
        public const int LobbySetCountdown        = 6003;

        // Match actions (6101-6199): none — the match plays out server-authoritatively, clients only observe.
    }

    /// <summary>
    /// Base class for all actions executed against the <see cref="MatchModel"/>.
    /// </summary>
    [MetaSerializable]
    public abstract class MatchAction : ModelAction<MatchModel>
    {
    }

    /// <summary>
    /// Base class for server-originated match actions. These can only be issued by the server (the match actor).
    /// </summary>
    [ModelActionExecuteFlags(ModelActionExecuteFlags.LeaderSynchronized)]
    public abstract class MatchServerAction : MatchAction
    {
    }

    /// <summary>
    /// Base class for client-originated match actions. Clients enqueue these and the server executes them and
    /// broadcasts them to all participants.
    /// </summary>
    [ModelActionExecuteFlags(ModelActionExecuteFlags.FollowerSynchronized)]
    public abstract class MatchClientAction : MatchAction
    {
        /// <summary>
        /// Hook for validating and preprocessing client-originated actions on the server, run before
        /// <see cref="ModelAction{TModel}.InvokeExecute"/>. The default implementation accepts the action.
        /// </summary>
        public virtual bool ValidateOnServer(EntityId issuer) => true;
    }

    /// <summary>
    /// Game-specific results returned from match actions. (Retained for the match action plumbing; the match
    /// itself currently exposes no client-originated actions — it plays out server-authoritatively.)
    /// </summary>
    public static class MatchActionResults
    {
        public static readonly MetaActionResult MatchNotActive  = new MetaActionResult(nameof(MatchNotActive));
        public static readonly MetaActionResult NotAParticipant = new MetaActionResult(nameof(NotAParticipant));
    }
}
