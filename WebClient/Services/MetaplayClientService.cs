using Game.Logic;
using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.MultiplayerEntity;
using Metaplay.Core.Player;
using Metaplay.Unity;
using Metaplay.Unity.DefaultIntegration;
using WebClientBase.Services;

namespace WebClient.Services;

/// <summary>
/// Typed Metaplay client for web application. Registers the lobby and match multiplayer sub-clients.
/// </summary>
public class MetaplayClient : MetaplayClientBase<PlayerModel>
{
    public static LobbyClient? LobbyClient => ClientStore?.TryGetClient<LobbyClient>(ClientSlotGame.Lobby);
    public static MatchClient? MatchClient => ClientStore?.TryGetClient<MatchClient>(ClientSlotGame.Match);
}

/// <summary>
/// Game-specific Metaplay client service that manages the MetaplayClient connection.
/// Inherits connection lifecycle management from MetaplayClientServiceBase.
/// </summary>
public class MetaplayClientService : MetaplayClientServiceBase<PlayerModel>,
    IPlayerModelClientListener, ILobbyModelClientListener, IMatchModelClientListener
{
    /// <summary>
    /// The current player model, or null if not connected.
    /// </summary>
    public override PlayerModel? PlayerModel => MetaplayClient.PlayerModel;

    /// <summary>
    /// The lobby the player is currently queued in, or null.
    /// </summary>
    public LobbyModel? Lobby => ConnectionState == ConnectionState.Connected ? MetaplayClient.LobbyClient?.Model : null;

    /// <summary>
    /// The match the player is currently in, or null.
    /// </summary>
    public MatchModel? Match => ConnectionState == ConnectionState.Connected ? MetaplayClient.MatchClient?.Model : null;

    /// <summary>
    /// True if the player is currently queued in the lobby.
    /// </summary>
    public bool IsInLobby => Lobby != null;

    /// <summary>
    /// True if the player is currently in a match.
    /// </summary>
    public bool IsInMatch => Match != null;

    /// <summary>
    /// Initialize the MetaplayClient with lifecycle delegate and the multiplayer sub-clients.
    /// </summary>
    protected override void InitializeClient()
    {
        MetaplayClient.Initialize(new MetaplayClientOptions
        {
            LifecycleDelegate = this,
            AdditionalClients = new IMetaplaySubClient[]
            {
                new LobbyClient(),
                new MatchClient(),
            },
        });
    }

    /// <summary>
    /// Connect to the Metaplay server.
    /// </summary>
    protected override void Connect()
    {
        MetaplayClient.Connect();
    }

    /// <summary>
    /// Execute a player action.
    /// Enqueues the action to run on the main thread to avoid conflicts with the tick loop.
    /// </summary>
    public override void ExecuteAction(PlayerActionBase action)
    {
        MetaplaySDK.RunOnMainThreadAsync(() =>
        {
            MetaplayClient.PlayerContext?.ExecuteAction(action);
        });
    }

    /// <summary>
    /// Enqueue a match client action to the match entity.
    /// </summary>
    public void EnqueueMatchAction(MatchAction action)
    {
        MetaplaySDK.RunOnMainThreadAsync(() =>
        {
            MetaplayClient.MatchClient?.Context?.EnqueueAction(action);
        });
    }

    /// <summary>
    /// Add the player to the matchmaking queue.
    /// </summary>
    public void FindMatch() { PendingFriendlyCode = null; ExecuteAction(new PlayerFindMatch()); }

    /// <summary>
    /// Leave the current match and return to the lobby.
    /// </summary>
    public void LeaveMatch() { PendingFriendlyCode = null; ExecuteAction(new PlayerLeaveMatch()); }

    // ----- Private "play a friend" rooms (Phase 2) -----

    /// <summary> The room code this client created and is waiting on, or null. The code is generated locally and
    /// shown immediately; the friend types it to join. </summary>
    public string? PendingFriendlyCode { get; private set; }

    static readonly System.Random _codeRng = new System.Random();
    static string GenerateFriendlyCode()
    {
        // Unambiguous alphabet (no O/0/I/1).
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        char[] chars = new char[5];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = alphabet[_codeRng.Next(alphabet.Length)];
        return new string(chars);
    }

    /// <summary> Open a private room and start waiting for a friend to join with the shown code. </summary>
    public void CreateFriendly()
    {
        PendingFriendlyCode = GenerateFriendlyCode();
        ExecuteAction(new PlayerCreateFriendly(PendingFriendlyCode));
        NotifyStateChanged();
    }

    /// <summary> Join a friend's private room by code (forms the match if the room exists). </summary>
    public void JoinFriendly(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return;
        ExecuteAction(new PlayerJoinFriendly(code.Trim().ToUpperInvariant()));
    }

    /// <summary> Cancel the private room this client is waiting on. </summary>
    public void CancelFriendly()
    {
        PendingFriendlyCode = null;
        ExecuteAction(new PlayerCancelFriendly());
        NotifyStateChanged();
    }

    /// <summary>
    /// Force the lobby to start a match immediately, padding with bots as needed.
    /// </summary>
    public void StartMatchNow() => ExecuteAction(new PlayerStartMatchNow());

    /// <summary> Set the national team the manager fields. </summary>
    public void SelectTeam(string teamId) => ExecuteAction(new PlayerSelectTeam(teamId));

    /// <summary> Upgrade one squad card (team slot), spending Coins + Shards. </summary>
    public void UpgradeCard(string teamId, int slot) => ExecuteAction(new PlayerUpgradeCard(teamId, slot));

    /// <summary> Development-only currency grant (for local testing of the economy UI). </summary>
    public void GrantCurrencyDebug(CurrencyType currency, long amount) => ExecuteAction(new PlayerGrantCurrencyDebug(currency, amount));

    /// <summary> Claim the next reached Cup milestone. </summary>
    public void ClaimCupMilestone() => ExecuteAction(new PlayerClaimCupMilestone());

    /// <summary> Refill Match Tickets to full for Gems. </summary>
    public void RefillTickets() => ExecuteAction(new PlayerRefillTickets());

    /// <summary> Development-only: refill Match Tickets for local testing. </summary>
    public void GrantTicketsDebug() => ExecuteAction(new PlayerGrantTicketsDebug());

    /// <summary> Join (or create) the named club. </summary>
    public void JoinClub(string name)
    {
        if (!string.IsNullOrWhiteSpace(name))
            ExecuteAction(new PlayerJoinClub(name.Trim()));
    }

    /// <summary> Leave the current club. </summary>
    public void LeaveClub() => ExecuteAction(new PlayerLeaveClub());

    /// <summary> Ask the server to refresh the cached Club League standings. </summary>
    public void RefreshClub() => ExecuteAction(new PlayerRefreshClub());

    /// <summary> Claim the next reached tier on the free or premium Season Pass track. </summary>
    public void ClaimPassReward(bool premium) => ExecuteAction(new PlayerClaimPassReward(premium));

    /// <summary> Unlock the premium Season Pass for Gems. </summary>
    public void BuyPremiumPass() => ExecuteAction(new PlayerBuyPremiumPass());

    /// <summary> Simulated in-app purchase of a gem pack (real builds route through IAP validation). </summary>
    public void SimPurchase(string productId) => ExecuteAction(new PlayerSimPurchase(productId));

    /// <summary> Operator/dev: set a player's live form die-tier override (0 clears it). </summary>
    public void ApplyForm(string playerName, int tierDelta)
    {
        if (!string.IsNullOrWhiteSpace(playerName))
            ExecuteAction(new PlayerApplyFormDebug(playerName.Trim(), tierDelta));
    }

    /// <summary> Operator/dev: clear all live-form overrides. </summary>
    public void ClearForm() => ExecuteAction(new PlayerClearFormDebug());

    /// <summary> Refresh the cached live-form snapshot. </summary>
    public void RefreshForm() => ExecuteAction(new PlayerRefreshForm());

    /// <summary> Enter this week's marquee Bracket Cup. </summary>
    public void EnterBracket() => ExecuteAction(new PlayerEnterBracket());

    /// <summary> Play the current bracket round. </summary>
    public void PlayBracketRound() => ExecuteAction(new PlayerPlayBracketRound());

    /// <summary> Buy a cosmetic with Gems. </summary>
    public void BuyCosmetic(string id) => ExecuteAction(new PlayerBuyCosmetic(id));

    /// <summary> Equip an owned cosmetic. </summary>
    public void EquipCosmetic(string id) => ExecuteAction(new PlayerEquipCosmetic(id));

    // ----- FOOTDRAFT spin-draft (P1) -----

    /// <summary> Choose the formation to draft into. </summary>
    public void ChooseFormation(string formationId) => ExecuteAction(new PlayerChooseFormation(formationId));

    /// <summary> Request a server-rolled spin for a draft slot (re-spinning the same slot is a reroll). </summary>
    public void SpinForSlot(int slot) => ExecuteAction(new PlayerSpinForSlot(slot));

    /// <summary> Pick a candidate from the pending spin offer into its slot. </summary>
    public void PickFromOffer(string legendId) => ExecuteAction(new PlayerPickFromOffer(legendId));

    /// <summary> Clear the in-progress draft (picks, offer, rerolls). </summary>
    public void ResetDraft() => ExecuteAction(new PlayerResetDraft());

    // ----- Season league (P4) -----

    /// <summary> Create a private league with a freshly-generated invite code (shown once the snapshot returns). </summary>
    public void CreateLeague(string leagueName, string teamName)
    {
        string code = GenerateFriendlyCode();
        ExecuteAction(new PlayerCreateLeague((leagueName ?? "").Trim(), code, (teamName ?? "").Trim()));
    }

    /// <summary> Create a single-player season: no lobby, straight into the draft vs a league of CPU teams. </summary>
    public void CreateSoloLeague(string teamName)
    {
        string code = GenerateFriendlyCode();
        ExecuteAction(new PlayerCreateSoloLeague(code, (teamName ?? "").Trim()));
    }

    /// <summary> Join a league by invite code. </summary>
    public void JoinLeague(string code, string teamName)
    {
        if (!string.IsNullOrWhiteSpace(code))
            ExecuteAction(new PlayerJoinLeague(code.Trim().ToUpperInvariant(), (teamName ?? "").Trim()));
    }

    /// <summary> Leave the current league (lobby only). </summary>
    public void LeaveLeague() => ExecuteAction(new PlayerLeaveLeague());

    /// <summary> Commissioner: start the draft (managers then take turns picking their XIs). </summary>
    public void StartLeagueSeason() => ExecuteAction(new PlayerStartLeagueSeason());

    /// <summary> Play your next league fixture. </summary>
    public void PlayLeagueFixture() => ExecuteAction(new PlayerPlayLeagueFixture());

    /// <summary> Refresh the cached league snapshot. </summary>
    public void RefreshLeague() => ExecuteAction(new PlayerRefreshLeague());

    // ----- League draft (the headline flow) -----

    /// <summary> Set the formation you draft into. </summary>
    public void SetLeagueFormation(string formationId) => ExecuteAction(new PlayerSetLeagueFormation(formationId));

    /// <summary> Spin the wheel for a random Club×Season squad to pick from. </summary>
    public void LeagueSpin() => ExecuteAction(new PlayerLeagueSpin());

    /// <summary> Gem-paid elite spin: guaranteed top-tier club (refunded server-side if the league rejects). </summary>
    public void LeagueEliteSpin() => ExecuteAction(new PlayerLeagueSpin(elite: true));

    /// <summary> Draft a legend into your XI (only on your turn). </summary>
    public void LeagueDraftPick(string legendId) => ExecuteAction(new PlayerLeagueDraftPick(legendId));

    /// <summary> Auto-pick one legend for the current drafter. </summary>
    public void LeagueAutoPick() => ExecuteAction(new PlayerLeagueAutoPick(false));

    /// <summary> Commissioner: auto-draft the entire rest of the league. </summary>
    public void LeagueAutoDraftAll() => ExecuteAction(new PlayerLeagueAutoPick(true));

    /// <summary> Commissioner: simulate every remaining fixture and finish the season. </summary>
    public void SimulateLeague() => ExecuteAction(new PlayerSimulateLeague());

    // ----- Transfer window (WS3) + retention (WS4) -----

    /// <summary> Transfer-window swap: drop one drafted legend for another (during an open window). The fee is
    /// charged from the wallet — OVR-scaled Coins, or Gems when <paramref name="payWithGems"/> (marquee signing). </summary>
    public void LeagueTransferSwap(string dropLegendId, string addLegendId, bool payWithGems = false) => ExecuteAction(new PlayerLeagueTransferSwap(dropLegendId, addLegendId, payWithGems));

    /// <summary> Claim a completed quest's reward (daily or season scope). </summary>
    public void ClaimQuest(string questId) => ExecuteAction(new PlayerClaimQuest(questId));

    /// <summary> Gem-paid reroll: swap one unclaimed daily quest for the next unused definition. </summary>
    public void RerollQuest(string questId) => ExecuteAction(new PlayerRerollQuest(questId));

    /// <summary> Exchange Gems for a Coins pack (the hard→soft currency bridge). </summary>
    public void BuyCoinPack(string productId) => ExecuteAction(new PlayerBuyCoinPack(productId));

    // ----- Shop overlay (single mount point in the TopBar HUD; any "insufficient funds" moment opens it) -----

    /// <summary> True while the Shop overlay is open (owned by the TopBar so every screen can summon it). </summary>
    public bool ShopOpen { get; private set; }

    public void RequestShop() { ShopOpen = true; NotifyStateChanged(); }
    public void CloseShop()   { ShopOpen = false; NotifyStateChanged(); }

    // ----- In-game inbox (dashboard mail / broadcasts; SDK built-in actions) -----

    /// <summary> Claim a mail's attached rewards and mark it consumed. </summary>
    public void ConsumeMail(Metaplay.Core.MetaGuid mailId) => ExecuteAction(new Metaplay.Core.Player.PlayerConsumeMail(mailId));

    /// <summary> Mark a mail read/unread. </summary>
    public void SetMailRead(Metaplay.Core.MetaGuid mailId, bool isRead) => ExecuteAction(new Metaplay.Core.Player.PlayerToggleMailIsRead(mailId, isRead));

    /// <summary> Delete a mail from the inbox. </summary>
    public void DeleteMail(Metaplay.Core.MetaGuid mailId) => ExecuteAction(new Metaplay.Core.Player.PlayerDeleteMail(mailId));

    // ----- Phase 1 UI feedback state (post-match reward popup + level-up celebration) -----

    /// <summary> Rewards from the most recently finished match, shown as a popup until dismissed. </summary>
    public MatchRewardInfo? LastMatchReward { get; private set; }

    /// <summary> The level the manager just reached (for a level-up flourish), or null. </summary>
    public int? PendingLevelUp { get; private set; }

    public void ClearMatchReward() { LastMatchReward = null; NotifyStateChanged(); }
    public void ClearLevelUp() { PendingLevelUp = null; NotifyStateChanged(); }

    /// <summary>
    /// Close the connection.
    /// </summary>
    protected override void CloseConnection(bool flush)
    {
        MetaplayClient.Connection?.Close(flushEnqueuedMessages: flush);
    }

    /// <summary>
    /// Update the client store during the update loop.
    /// </summary>
    protected override void UpdateClientStore()
    {
        MetaplayClient.ClientStore?.EarlyUpdate();
        MetaplayClient.ClientStore?.UpdateLogic(MetaTime.Now);
    }

    /// <summary>
    /// Check if the current connection is in an error state.
    /// </summary>
    protected override bool IsConnectionInErrorState()
    {
        return MetaplayClient.Connection?.State?.Status == Metaplay.Core.Session.ConnectionStatus.Error;
    }

    /// <summary>
    /// Called when a session has started. Sets up the client listeners for the player and multiplayer entities.
    /// </summary>
    protected override void OnSessionStarted()
    {
        if (PlayerModel != null)
            PlayerModel.ClientListener = this;

        MetaplayClient.LobbyClient?.SetClientListeners(model => ((LobbyModel)model).ClientListener = this);
        MetaplayClient.MatchClient?.SetClientListeners(model => ((MatchModel)model).ClientListener = this);
    }

    #region Model client listeners (trigger UI re-render)

    void ILobbyModelClientListener.QueueChanged() => NotifyStateChanged();

    void IMatchModelClientListener.GoalScored(EntityId scorer, int minute) => NotifyStateChanged();
    void IMatchModelClientListener.MatchEnded(EntityId winner) => NotifyStateChanged();

    void IPlayerModelClientListener.MatchRewardsGranted(bool won, int coins, int xp, int shards, int cupTokens)
    {
        LastMatchReward = new MatchRewardInfo(won, coins, xp, shards, cupTokens);
        NotifyStateChanged();
    }

    void IPlayerModelClientListener.ManagerLeveledUp(int newLevel)
    {
        PendingLevelUp = newLevel;
        NotifyStateChanged();
    }

    void IPlayerModelClientListener.CardUpgraded(string cardKey, int newLevel) => NotifyStateChanged();

    void IPlayerModelClientListener.CupMilestoneClaimed(int claimedCount) => NotifyStateChanged();

    #endregion
}

/// <summary> Rewards granted at the end of a match, surfaced to the post-match popup. </summary>
public record MatchRewardInfo(bool Won, int Coins, int Xp, int Shards, int CupTokens);
