// FOOTDRAFT — match client-side listeners.

using Metaplay.Core;

namespace Game.Logic
{
    /// <summary>
    /// Client-side side-effect callbacks for the <see cref="MatchModel"/>. Used to drive UI updates and
    /// dice-reveal effects in response to rounds resolved on the model.
    /// </summary>
    public interface IMatchModelClientListener
    {
        /// <summary> A goal was just revealed (drives the goal flash + scoreboard bump). </summary>
        void GoalScored(EntityId scorer, int minute);

        /// <summary> The match ended; <paramref name="winner"/> is the winning manager (or None if undecided). </summary>
        void MatchEnded(EntityId winner);
    }

    public class EmptyMatchModelClientListener : IMatchModelClientListener
    {
        public static readonly EmptyMatchModelClientListener Instance = new EmptyMatchModelClientListener();

        public void GoalScored(EntityId scorer, int minute) { }
        public void MatchEnded(EntityId winner) { }
    }

    /// <summary>
    /// Server-side side-effect callbacks for the <see cref="MatchModel"/>. Set only on the server (by the
    /// <c>MatchActor</c> in <c>OnSwitchedToModel</c>); left as the empty instance on clients. Used to react to
    /// match outcomes authoritatively — e.g. granting end-of-match rewards to the participating players.
    /// </summary>
    public interface IMatchModelServerListener
    {
        /// <summary> The match ended; <paramref name="winner"/> is the winning manager (or None if undecided). </summary>
        void MatchEnded(EntityId winner);
    }

    public class EmptyMatchModelServerListener : IMatchModelServerListener
    {
        public static readonly EmptyMatchModelServerListener Instance = new EmptyMatchModelServerListener();

        public void MatchEnded(EntityId winner) { }
    }
}
