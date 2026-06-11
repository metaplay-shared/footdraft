using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Session;
using Metaplay.Core.Session.ConnectionStates;
using Metaplay.Unity;

namespace WebClientBase.Services;

/// <summary>
/// Abstract base class for Metaplay client services.
/// Handles connection lifecycle, reconnection with exponential backoff, and update timer.
/// Game-specific implementations should inherit from this class.
/// </summary>
/// <typeparam name="TPlayerModel">The game-specific PlayerModel type.</typeparam>
public abstract class MetaplayClientServiceBase<TPlayerModel> : IMetaplayClientService<TPlayerModel>, IMetaplayLifecycleDelegate
    where TPlayerModel : class, IPlayerModelBase
{
    private static bool _coreInitialized = false;
    private static bool _clientInitialized = false;
    private static readonly object _initLock = new object();

    private ConnectionState _connectionState = ConnectionState.Disconnected;
    private string? _errorMessage;
    private bool _updateLoopRunning;
    private int _reconnectAttempts = 0;
    // Bumped on every (re)connect attempt so a stale, already-scheduled reconnect delay can detect it has been
    // superseded and bail out instead of firing a duplicate connection.
    private int _reconnectGeneration = 0;

    /// <summary>
    /// Maximum number of reconnection attempts before giving up.
    /// </summary>
    protected virtual int MaxReconnectAttempts => 10;

    /// <summary>
    /// Base delay in milliseconds for reconnection attempts (used with exponential backoff).
    /// </summary>
    protected virtual int BaseReconnectDelayMs => 1000;

    /// <summary>
    /// Maximum delay in milliseconds for reconnection attempts.
    /// </summary>
    protected virtual int MaxReconnectDelayMs => 30000;

    /// <summary>
    /// Update interval in milliseconds for the SDK update timer.
    /// </summary>
    protected virtual int UpdateIntervalMs => 50;

    /// <summary>
    /// Event fired when connection state or player model changes.
    /// </summary>
    public event Action? OnStateChanged;

    /// <summary>
    /// Current connection state.
    /// </summary>
    public ConnectionState ConnectionState => _connectionState;

    /// <summary>
    /// Error message if connection failed.
    /// </summary>
    public string? ErrorMessage => _errorMessage;

    /// <summary>
    /// The current player model, or null if not connected.
    /// </summary>
    public abstract TPlayerModel? PlayerModel { get; }

    /// <summary>
    /// Player ID if connected, otherwise null.
    /// </summary>
    public EntityId? PlayerId => PlayerModel?.PlayerId;

    protected MetaplayClientServiceBase()
    {
        EnsureCoreInitialized();
    }

    /// <summary>
    /// Initialize MetaplayCore for the WebAssembly platform. Override to customize initialization.
    /// </summary>
    protected virtual void EnsureCoreInitialized()
    {
        lock (_initLock)
        {
            if (!_coreInitialized)
            {
                // Set the integration roots before init, then initialize for WebAssembly: this loads the pre-built
                // Metaplay.Generated.WebAssembly.dll by name (mono-wasm cannot generate a serializer at runtime) and
                // selects the ClientWebSocket transport.
                MetaplayCore.ClientIntegrationAssemblies = IntegrationAssembly.FindRoots().ToList();
                MetaplayCore.InitializeForClient(ClientPlatform.WebAssembly);
                _coreInitialized = true;
            }
        }
    }

    /// <summary>
    /// Initialize and connect to the Metaplay server.
    /// </summary>
    public Task ConnectAsync()
    {
        lock (_initLock)
        {
            if (_connectionState == ConnectionState.Connected)
                return Task.CompletedTask;

            if (_connectionState == ConnectionState.Connecting)
                return Task.CompletedTask;

            _connectionState = ConnectionState.Connecting;
            _errorMessage = null;
            NotifyStateChanged();

            if (!_clientInitialized)
            {
                InitializeClient();
                _clientInitialized = true;

                StartUpdateLoop();
            }

            Connect();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Initialize the MetaplayClient. Called once on first connection.
    /// Implementation should call MetaplayClient.Initialize with appropriate options.
    /// </summary>
    protected abstract void InitializeClient();

    /// <summary>
    /// Connect to the server. Called after client is initialized.
    /// Implementation should call MetaplayClient.Connect().
    /// </summary>
    protected abstract void Connect();

    /// <summary>
    /// Execute a player action.
    /// </summary>
    public abstract void ExecuteAction(PlayerActionBase action);

    /// <summary>
    /// Close the connection.
    /// </summary>
    /// <param name="flush">Whether to flush enqueued messages before closing.</param>
    protected abstract void CloseConnection(bool flush);

    /// <summary>
    /// Update the client store. Called during the update loop.
    /// </summary>
    protected abstract void UpdateClientStore();

    /// <summary>
    /// Stop the SDK.
    /// </summary>
    protected virtual void StopSdk()
    {
        MetaplaySDK.Stop();
    }

    /// <summary>
    /// Get the path to the credentials file.
    /// </summary>
    protected virtual string GetCredentialsPath()
    {
        #pragma warning disable CS0618
        return Path.Combine(MetaplaySDK.PersistentDataPath, "MetaplayCredentials.dat");
        #pragma warning restore CS0618
    }

    /// <summary>
    /// Check if the current connection is in an error state.
    /// </summary>
    protected abstract bool IsConnectionInErrorState();

    /// <summary>
    /// Reset the client: disconnect, delete credentials, and reset state.
    /// Returns true if reset was successful.
    /// </summary>
    public bool Reset()
    {
        try
        {
            _updateLoopRunning = false;
            _reconnectGeneration++;

            CloseConnection(flush: false);
            StopSdk();

            // Best-effort credential cleanup. On WebAssembly the credentials live on the in-memory virtual
            // filesystem (no persistence across reloads); a missing/inaccessible path must not abort the reset.
            try
            {
                string credentialsPath = GetCredentialsPath();
                if (File.Exists(credentialsPath))
                    File.Delete(credentialsPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{GetType().Name}] Credential cleanup skipped: {ex.Message}");
            }

            _connectionState = ConnectionState.Disconnected;
            _clientInitialized = false;

            Console.WriteLine($"[{GetType().Name}] Reset complete");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{GetType().Name}] Reset failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Start the cooperative SDK update pump if it is not already running.
    /// </summary>
    private void StartUpdateLoop()
    {
        if (_updateLoopRunning)
            return;

        _updateLoopRunning = true;
        _ = RunUpdateLoopAsync();
    }

    /// <summary>
    /// Drive the SDK update pump from the single browser thread. WebAssembly is single-threaded, so instead of a
    /// thread-pool Timer we await between ticks — the yield lets the WebSocket transport's async continuations run
    /// cooperatively. (No re-entrancy guard is needed: ticks never overlap on one thread.)
    /// </summary>
    private async Task RunUpdateLoopAsync()
    {
        while (_updateLoopRunning)
        {
            try
            {
                MetaplaySDK.Update();
                UpdateClientStore();
                MetaplaySDK.OnEndOfTheFrame();

                if (_connectionState == ConnectionState.Connected)
                {
                    NotifyStateChanged();
                }
            }
            catch (Exception)
            {
                // Ignore transient update errors (e.g. during shutdown / reconnect).
            }

            await Task.Delay(UpdateIntervalMs);
        }
    }

    /// <summary>
    /// Notify listeners that state has changed.
    /// </summary>
    protected void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }

    #region IMetaplayLifecycleDelegate

    Task IMetaplayLifecycleDelegate.OnSessionStartedAsync(IMetaplayLifecycleDelegate.SessionStartedArgs args)
    {
        OnSessionStarted();

        _connectionState = ConnectionState.Connected;
        _errorMessage = null;
        _reconnectAttempts = 0;
        NotifyStateChanged();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when a session has started. Override to perform additional setup.
    /// </summary>
    protected virtual void OnSessionStarted()
    {
    }

    void IMetaplayLifecycleDelegate.OnSessionLost(ConnectionLostEvent connectionLost)
    {
        ScheduleReconnect();
    }

    void IMetaplayLifecycleDelegate.OnFailedToStartSession(ConnectionLostEvent connectionLost)
    {
        Console.WriteLine($"[{GetType().Name}] OnFailedToStartSession: {connectionLost.TechnicalError?.GetType().Name} - {connectionLost.EnglishLocalizedReason}");

        if (IsTerminalError(connectionLost))
        {
            Console.WriteLine($"[{GetType().Name}] Terminal error, not reconnecting");
            _connectionState = ConnectionState.Error;
            _errorMessage = connectionLost.EnglishLocalizedReason;
            _reconnectAttempts = 0;
            NotifyStateChanged();
        }
        else
        {
            Console.WriteLine($"[{GetType().Name}] Transient error, scheduling reconnect");
            ScheduleReconnect();
        }
    }

    /// <summary>
    /// Determine if the connection error is terminal (should not retry).
    /// </summary>
    protected virtual bool IsTerminalError(ConnectionLostEvent connectionLost)
    {
        if (connectionLost.TechnicalError is TerminalError.InMaintenance
            or TerminalError.DevServerRestarting)
            return false;

        return connectionLost.TechnicalError is TerminalError;
    }

    private void ScheduleReconnect()
    {
        if (_reconnectAttempts >= MaxReconnectAttempts)
        {
            Console.WriteLine($"[{GetType().Name}] Max reconnect attempts reached, giving up");
            _connectionState = ConnectionState.Error;
            _errorMessage = "Failed to reconnect after multiple attempts";
            _reconnectAttempts = 0;
            NotifyStateChanged();
            return;
        }

        _connectionState = ConnectionState.Connecting;
        _errorMessage = null;
        NotifyStateChanged();

        int delayMs = Math.Min(BaseReconnectDelayMs * (1 << _reconnectAttempts), MaxReconnectDelayMs);
        _reconnectAttempts++;

        Console.WriteLine($"[{GetType().Name}] Scheduling reconnect attempt {_reconnectAttempts} in {delayMs}ms");

        // WebAssembly is single-threaded; delay cooperatively rather than via a thread-pool Timer. The generation
        // token lets a later connect/reset cancel this pending reconnect.
        int generation = ++_reconnectGeneration;
        _ = ReconnectAfterDelayAsync(delayMs, generation);
    }

    private async Task ReconnectAfterDelayAsync(int delayMs, int generation)
    {
        await Task.Delay(delayMs);

        // A newer connect attempt (or a reset) superseded this one while it was waiting.
        if (generation != _reconnectGeneration)
            return;

        Console.WriteLine($"[{GetType().Name}] Reconnect delay elapsed, attempting connection...");

        try
        {
            if (IsConnectionInErrorState())
            {
                Console.WriteLine($"[{GetType().Name}] Closing errored connection first");
                CloseConnection(flush: false);
            }

            Connect();
            Console.WriteLine($"[{GetType().Name}] Connect() called");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{GetType().Name}] Connect failed with exception: {ex.Message}");
            ScheduleReconnect();
        }
    }

    #endregion
}
