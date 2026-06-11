// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

namespace Game.Logic
{
    /// <summary>
    /// Client-side side-effect callbacks for the <see cref="LobbyModel"/>.
    /// </summary>
    public interface ILobbyModelClientListener
    {
        /// <summary> The queue contents or countdown changed. </summary>
        void QueueChanged();
    }

    public class EmptyLobbyModelClientListener : ILobbyModelClientListener
    {
        public static readonly EmptyLobbyModelClientListener Instance = new EmptyLobbyModelClientListener();

        public void QueueChanged() { }
    }
}
