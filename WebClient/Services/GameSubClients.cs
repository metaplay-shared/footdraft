using Game.Logic;
using Metaplay.Core.Client;
using Metaplay.Core.MultiplayerEntity;

namespace WebClient.Services;

/// <summary>
/// Client for the matchmaking lobby multiplayer entity.
/// </summary>
public class LobbyClient : MultiplayerEntityClientBase<LobbyModel>
{
    public override ClientSlot ClientSlot => ClientSlotGame.Lobby;
}

/// <summary>
/// Client for the match multiplayer entity.
/// </summary>
public class MatchClient : MultiplayerEntityClientBase<MatchModel>
{
    public override ClientSlot ClientSlot => ClientSlotGame.Match;
}
