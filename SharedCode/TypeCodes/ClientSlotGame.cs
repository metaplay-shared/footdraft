// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core.Client;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// Registry for game-specific <see cref="ClientSlot"/> values. A ClientSlot identifies a multiplayer
    /// entity association on the client. Core slots use the low range (1-4), so we use higher values here.
    /// </summary>
    [MetaSerializable]
    public class ClientSlotGame : ClientSlot
    {
        public ClientSlotGame(int id, string name) : base(id, name) { }

        /// <summary> The matchmaking lobby the player is currently queued in. </summary>
        public static readonly ClientSlot Lobby = new ClientSlotGame(20, nameof(Lobby));

        /// <summary> The match the player is currently in. </summary>
        public static readonly ClientSlot Match = new ClientSlotGame(21, nameof(Match));
    }
}
