// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity;
using System.Runtime.Serialization;

namespace Game.Logic
{
    /// <summary>
    /// Shared model for the global matchmaking lobby (a singleton entity). Players associate with the lobby
    /// while queuing for a match. The lobby tracks the queue and an auto-start countdown; the actual match
    /// creation is driven from the server-side <c>LobbyActor</c>.
    /// </summary>
    [MetaSerializableDerived(101)]
    [SupportedSchemaVersions(1, 1)]
    public class LobbyModel : MultiplayerModelBase<LobbyModel>
    {
        /// <summary> Minimum number of queued human players required to auto-start a match. </summary>
        public const int MinPlayers = 2;

        /// <summary>
        /// Maximum number of managers in a single match. FOOTDRAFT is head-to-head, so the queue auto-starts
        /// once two humans queue, and a forced "start now" pads up to this count with a bot opponent.
        /// </summary>
        public const int MaxPlayers = 2;

        /// <summary> Seconds to wait (filling up the match) after the minimum is reached before auto-starting. </summary>
        public const int CountdownSeconds = 5;

        // The lobby doesn't run any tick-based game logic; the countdown is evaluated against wall-clock time
        // both on the server (actor timer) and on the client (for display). TicksPerSecond is required by the
        // base class but ticking is disabled in the actor (IsTicking => false).
        public override int TicksPerSecond => 1;

        [IgnoreDataMember] public ILobbyModelClientListener ClientListener { get; set; } = EmptyLobbyModelClientListener.Instance;

        /// <summary> Players currently queued for a match, keyed by player EntityId, value is the player name. </summary>
        [MetaMember(1)] public MetaDictionary<EntityId, string> QueuedPlayers { get; set; } = new MetaDictionary<EntityId, string>();

        /// <summary> Wall-clock time at which the countdown completes and a match is formed. <see cref="MetaTime.Epoch"/> means no countdown is running. </summary>
        [MetaMember(2)] public MetaTime CountdownEndsAt { get; set; } = MetaTime.Epoch;

        public bool HasCountdown => CountdownEndsAt > MetaTime.Epoch;

        public override void OnTick() { }
        public override void OnFastForwardTime(MetaDuration elapsedTime) { }

        public override string GetDisplayNameForDashboard() => $"Lobby ({QueuedPlayers.Count} queued)";
    }
}
