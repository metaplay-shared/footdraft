using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Player;

namespace WebClientBase.Services;

/// <summary>
/// Non-generic interface providing connection state for components that don't need typed PlayerModel access.
/// </summary>
public interface IMetaplayConnectionService
{
    /// <summary>
    /// Event fired when connection state or player model changes.
    /// </summary>
    event Action? OnStateChanged;

    /// <summary>
    /// Current connection state.
    /// </summary>
    ConnectionState ConnectionState { get; }

    /// <summary>
    /// Error message if connection failed.
    /// </summary>
    string? ErrorMessage { get; }

    /// <summary>
    /// Player ID if connected, otherwise null.
    /// </summary>
    EntityId? PlayerId { get; }

    /// <summary>
    /// Initialize and connect to the Metaplay server.
    /// </summary>
    Task ConnectAsync();

    /// <summary>
    /// Reset the client: disconnect, delete credentials, and reset state.
    /// Returns true if reset was successful.
    /// </summary>
    bool Reset();
}

/// <summary>
/// Interface for Metaplay client services that manage player connection and state.
/// </summary>
/// <typeparam name="TPlayerModel">The game-specific PlayerModel type.</typeparam>
public interface IMetaplayClientService<TPlayerModel> : IMetaplayConnectionService
    where TPlayerModel : IPlayerModelBase
{
    /// <summary>
    /// The current player model, or null if not connected.
    /// </summary>
    TPlayerModel? PlayerModel { get; }

    /// <summary>
    /// Execute a player action.
    /// </summary>
    void ExecuteAction(PlayerActionBase action);
}
