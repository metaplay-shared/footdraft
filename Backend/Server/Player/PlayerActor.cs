// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Core;
using Metaplay.Core.Client;
using Metaplay.Core.Config;
using Metaplay.Server;
using Metaplay.Server.MultiplayerEntity.InternalMessages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static System.FormattableString;

namespace Game.Server.Player
{
    [EntityConfig]
    public class PlayerConfig : PlayerConfigBase
    {
        public override Type EntityActorType => typeof(PlayerActor);
    }

    /// <summary>
    /// Entity actor class representing a player. Owns the player's associations with the matchmaking lobby
    /// and the match the player is currently in.
    /// </summary>
    public sealed class PlayerActor : PlayerActorBase<PlayerModel>, IPlayerModelServerListener
    {
        public PlayerActor()
        {
        }

        /// <summary>
        /// The SharedGameConfig that contains any A/B tests that affect this user player.
        /// This is only available after PostLoad has been invoked, or rather in most methods outside Initialize, InitializePersisted, PostLoad, and the constructor!
        /// </summary>
        private SharedGameConfigBase SharedGameConfig => (SharedGameConfigBase)_specializedGameConfig.SharedConfig;

        /// <summary>
        /// The ServerGameConfig that contains any A/B tests that affect this user player.
        /// This is only available after PostLoad has been invoked, or rather in most methods outside Initialize, InitializePersisted, PostLoad, and the constructor!
        /// </summary>
        private ServerGameConfigBase ServerGameConfig => (ServerGameConfigBase)_specializedGameConfig.ServerConfig;

        protected override string RandomNewPlayerName()
        {
            return Invariant($"Guest {new Random().Next(100_000)}");
        }

        protected override void OnSwitchedToModel(PlayerModel model)
        {
            model.ServerListener = this;
        }

        /// <summary>
        /// In a LOCAL environment the SDK defaults to <see cref="ChecksumGranularity.PerActionSingleTickPerFrame"/>,
        /// which checksums the whole PlayerModel on every tick of every frame. In the single-threaded Blazor WASM
        /// client that CPU storm can stall the frame loop long enough to trip the WebSocket header timeout — a
        /// reconnect that looks like a "draft desync" but is purely a resource artefact (no real divergence; the
        /// server never reports a checksum mismatch). Cloud dev envs already use the lighter <c>PerBatch</c>; we
        /// match that locally so the consistency net stays on without starving the WASM client. Bots keep full
        /// granularity (the base returns <see cref="PlayerDebugConfig.EnableAll"/> for them, untouched here).
        /// </summary>
        protected override PlayerDebugConfig ResolveDebugConfigForSession()
        {
            PlayerDebugConfig config = base.ResolveDebugConfigForSession();
            if (config.ChecksumGranularity != ChecksumGranularity.PerActionSingleTickPerFrame)
                return config;
            return new PlayerDebugConfig
            {
                ClientConsistencyChecks = config.ClientConsistencyChecks,
                ServerConsistencyChecks = config.ServerConsistencyChecks,
                ChecksumGranularity     = ChecksumGranularity.PerBatch,
            };
        }

        protected override async Task OnSessionStartAsync(PlayerSessionParams sessionParams, bool isFirstLogin)
        {
            await base.OnSessionStartAsync(sessionParams, isFirstLogin);

            // Daily-login streak (WS4 retention): advance it server-side on each session start. The day index is
            // computed here (server time) and passed into the action so the model mutation stays deterministic.
            long today = MetaTime.Now.MillisecondsSinceEpoch / 86_400_000;
            EnqueueServerAction(new PlayerUpdateLoginStreak(today));

            // Re-attach to an in-progress match so the client immediately rejoins it.
            if (Model.CurrentMatch != EntityId.None)
            {
                UpdateMatchAssociation();
            }
            else if (Model.CurrentLobby != EntityId.None)
            {
                // The player was only queued (not in a match) last session. Matchmaking is human-triggered,
                // so don't auto re-queue: return the player to the idle lobby. The lobby association was
                // already dropped on session end (removeOnSessionEnd), and the lobby removed the queue entry.
                Model.CurrentLobby = EntityId.None;
            }

            // Re-register club membership with the (ephemeral) ClubsActor and refresh the standings snapshot.
            if (Model.Club.InClub)
                EnqueueOnActorContext(() => JoinClubAsync(Model.Club.Name));
        }

        #region IPlayerModelServerListener

        void IPlayerModelServerListener.FindMatch() => EnqueueOnActorContext(FindMatchAsync);

        void IPlayerModelServerListener.LeaveMatch() => EnqueueOnActorContext(LeaveMatchInternal);

        void IPlayerModelServerListener.StartMatchNow() => EnqueueOnActorContext(StartMatchNowAsync);

        void IPlayerModelServerListener.CreateFriendly(string code) => EnqueueOnActorContext(() => CreateFriendlyAsync(code));

        void IPlayerModelServerListener.JoinFriendly(string code) => EnqueueOnActorContext(() => JoinFriendlyAsync(code));

        void IPlayerModelServerListener.CancelFriendly() => EnqueueOnActorContext(CancelFriendlyAsync);

        void IPlayerModelServerListener.JoinClub(string clubName) => EnqueueOnActorContext(() => JoinClubAsync(clubName));

        void IPlayerModelServerListener.LeaveClub() => EnqueueOnActorContext(LeaveClubAsync);

        void IPlayerModelServerListener.RefreshClub() => EnqueueOnActorContext(RefreshClubAsync);

        void IPlayerModelServerListener.ApplyForm(string playerName, int tierDelta) => EnqueueOnActorContext(() => ApplyFormAsync(playerName, tierDelta));

        void IPlayerModelServerListener.ClearForm() => EnqueueOnActorContext(ClearFormAsync);

        void IPlayerModelServerListener.RefreshForm() => EnqueueOnActorContext(RefreshFormAsync);

        void IPlayerModelServerListener.SpinDraftSlot(int slot) => EnqueueOnActorContext(() => SpinDraftSlotInternal(slot));

        void IPlayerModelServerListener.CreateLeague(string leagueName, string code) => EnqueueOnActorContext(() => CreateLeagueAsync(leagueName, code, solo: false));
        void IPlayerModelServerListener.CreateSoloLeague(string code)  => EnqueueOnActorContext(() => CreateLeagueAsync("Solo Season", code, solo: true));
        void IPlayerModelServerListener.JoinLeague(string code)        => EnqueueOnActorContext(() => JoinLeagueAsync(code));
        void IPlayerModelServerListener.LeaveLeague(string code)       => EnqueueOnActorContext(() => LeaveLeagueAsync(code));
        void IPlayerModelServerListener.StartLeagueSeason(string code) => EnqueueOnActorContext(() => StartLeagueSeasonAsync(code));
        void IPlayerModelServerListener.PlayLeagueFixture(string code) => EnqueueOnActorContext(() => PlayLeagueFixtureAsync(code));
        void IPlayerModelServerListener.RefreshLeague(string code)     => EnqueueOnActorContext(() => RefreshLeagueAsync(code));
        void IPlayerModelServerListener.SetLeagueFormation(string code, string formationId) => EnqueueOnActorContext(() => SetLeagueFormationAsync(code, formationId));
        void IPlayerModelServerListener.LeagueSpin(string code, bool elite) => EnqueueOnActorContext(() => LeagueSpinAsync(code, elite));
        void IPlayerModelServerListener.LeagueDraftPick(string code, string legendId)        => EnqueueOnActorContext(() => LeagueDraftPickAsync(code, legendId));
        void IPlayerModelServerListener.LeagueDraftAutoPick(string code, bool fillAll)        => EnqueueOnActorContext(() => LeagueAutoPickAsync(code, fillAll));
        void IPlayerModelServerListener.SimulateLeagueSeason(string code)                     => EnqueueOnActorContext(() => SimulateLeagueAsync(code));
        void IPlayerModelServerListener.LeagueTransferSwap(string code, string dropLegendId, string addLegendId, bool payWithGems) => EnqueueOnActorContext(() => LeagueTransferSwapAsync(code, dropLegendId, addLegendId, payWithGems));

        #endregion

        async Task FindMatchAsync()
        {
            // Already in a match; ignore the request.
            if (Model.CurrentMatch != EntityId.None)
                return;

            try
            {
                await EntityAskAsync<EntityAskOk>(LobbyActor.LobbyEntityId, new LobbyEnqueuePlayerRequest(Model.PlayerName));
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to enqueue player into lobby: {Error}", ex);
                return;
            }

            Model.CurrentLobby = LobbyActor.LobbyEntityId;
            UpdateLobbyAssociation();
        }

        async Task StartMatchNowAsync()
        {
            // Only meaningful while queued and not already in a match.
            if (Model.CurrentMatch != EntityId.None || Model.CurrentLobby == EntityId.None)
                return;

            try
            {
                await EntityAskAsync<EntityAskOk>(LobbyActor.LobbyEntityId, new LobbyStartNowRequest());
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to start match immediately: {Error}", ex);
            }
        }

        async Task CreateFriendlyAsync(string code)
        {
            if (Model.CurrentMatch != EntityId.None)
                return;
            try
            {
                await EntityAskAsync<EntityAskOk>(LobbyActor.LobbyEntityId, new LobbyCreateFriendlyRequest(Model.PlayerName, code));
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to create friendly room {Code}: {Error}", code, ex);
            }
        }

        async Task JoinFriendlyAsync(string code)
        {
            if (Model.CurrentMatch != EntityId.None)
                return;
            try
            {
                // On success the lobby forms the match and assigns this player via PlayerAssignToMatchRequest.
                await EntityAskAsync<EntityAskOk>(LobbyActor.LobbyEntityId, new LobbyJoinFriendlyRequest(Model.PlayerName, code));
            }
            catch (Exception ex)
            {
                _log.Info("Join of friendly room {Code} failed (likely a bad/expired code): {Error}", code, ex);
            }
        }

        async Task CancelFriendlyAsync()
        {
            try
            {
                await EntityAskAsync<EntityAskOk>(LobbyActor.LobbyEntityId, new LobbyCancelFriendlyRequest());
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to cancel friendly room: {Error}", ex);
            }
        }

        async Task JoinClubAsync(string clubName)
        {
            try
            {
                await EntityAskAsync<EntityAskOk>(ClubsActor.ClubsEntityId, new ClubJoinRequest(Model.PlayerName, clubName));
                EnqueueServerAction(new PlayerSetClubMembership(clubName));
                await RefreshClubSnapshotAsync(clubName);
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to join club {Club}: {Error}", clubName, ex);
            }
        }

        async Task LeaveClubAsync()
        {
            try
            {
                await EntityAskAsync<EntityAskOk>(ClubsActor.ClubsEntityId, new ClubLeaveRequest());
                EnqueueServerAction(new PlayerSetClubMembership(""));
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to leave club: {Error}", ex);
            }
        }

        async Task RefreshClubAsync()
        {
            if (Model.Club.InClub)
                await RefreshClubSnapshotAsync(Model.Club.Name);
        }

        async Task RefreshClubSnapshotAsync(string clubName)
        {
            try
            {
                GlobalConfig global = Model.GameConfig.Global;
                long weekId = ClubWeek.CurrentWeekId(MetaTime.Now, global);
                ClubGetSnapshotResponse response = await EntityAskAsync<ClubGetSnapshotResponse>(
                    ClubsActor.ClubsEntityId, new ClubGetSnapshotRequest(clubName, weekId, global.ClubStandingsTopN));
                if (response?.Snapshot != null)
                    EnqueueServerAction(new PlayerSetClubSnapshot(response.Snapshot));
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to refresh club snapshot for {Club}: {Error}", clubName, ex);
            }
        }

        async Task ApplyFormAsync(string playerName, int tierDelta)
        {
            try
            {
                await EntityAskAsync<EntityAskOk>(FormActor.FormEntityId, new FormSetRequest(playerName, tierDelta));
                await RefreshFormSnapshotAsync();
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to apply form override for {Player}: {Error}", playerName, ex);
            }
        }

        async Task ClearFormAsync()
        {
            try
            {
                await EntityAskAsync<EntityAskOk>(FormActor.FormEntityId, new FormClearRequest());
                await RefreshFormSnapshotAsync();
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to clear form overrides: {Error}", ex);
            }
        }

        // ----- Season league (P4): talk to the singleton LeagueActor, cache the result via PlayerSetLeagueState. -----

        /// <summary> The manager's league display name = their chosen team name, falling back to the SDK player name. </summary>
        string LeagueMemberName()
            => !string.IsNullOrWhiteSpace(Model.TeamName) ? Model.TeamName.Trim() : Model.PlayerName;

        /// <summary> The manager's crest = equipped avatar glyph, fallback ball. </summary>
        string LeagueCrest()
        {
            SharedGameConfig config = Model.GameConfig;
            if (!string.IsNullOrEmpty(Model.Cosmetics.EquippedAvatar)
                && config.Cosmetics.TryGetValue(Model.Cosmetics.EquippedAvatar, out Game.Logic.CosmeticItem avatar))
                return avatar.Glyph;
            return "⚽";
        }

        async Task CreateLeagueAsync(string leagueName, string code, bool solo)
        {
            try
            {
                LeagueOpResponse r = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId,
                    new LeagueCreateRequest(code, leagueName, LeagueMemberName(), LeagueCrest(), solo));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? "", r.Snapshot, r.Error, null));
            }
            catch (Exception ex) { _log.Warning("CreateLeague failed: {Error}", ex); }
        }

        async Task JoinLeagueAsync(string code)
        {
            try
            {
                LeagueOpResponse r = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId,
                    new LeagueJoinRequest(code, LeagueMemberName(), LeagueCrest()));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? "", r.Snapshot, r.Error, null));
            }
            catch (Exception ex) { _log.Warning("JoinLeague failed: {Error}", ex); }
        }

        async Task LeaveLeagueAsync(string code)
        {
            try
            {
                await EntityAskAsync<EntityAskOk>(LeagueActor.LeagueEntityId, new LeagueLeaveRequest(code));
                EnqueueServerAction(new PlayerSetLeagueState("", null, "", null));
            }
            catch (Exception ex) { _log.Warning("LeaveLeague failed: {Error}", ex); }
        }

        async Task StartLeagueSeasonAsync(string code)
        {
            try
            {
                LeagueOpResponse r = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueStartRequest(code));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? code, r.Snapshot, r.Error, null));
            }
            catch (Exception ex) { _log.Warning("StartLeagueSeason failed: {Error}", ex); }
        }

        async Task PlayLeagueFixtureAsync(string code)
        {
            try
            {
                LeaguePlayResponse r = await EntityAskAsync<LeaguePlayResponse>(LeagueActor.LeagueEntityId, new LeaguePlayRequest(code));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? code, r.Snapshot, r.Error, r.Result));
            }
            catch (Exception ex) { _log.Warning("PlayLeagueFixture failed: {Error}", ex); }
        }

        async Task RefreshLeagueAsync(string code)
        {
            try
            {
                LeagueOpResponse r = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueGetSnapshotRequest(code));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? code, r.Snapshot, r.Error, null));
            }
            catch (Exception ex) { _log.Warning("RefreshLeague failed: {Error}", ex); }
        }

        async Task SetLeagueFormationAsync(string code, string formationId)
        {
            try
            {
                LeagueOpResponse r = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueSetFormationRequest(code, formationId));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? code, r.Snapshot, r.Error, null));
            }
            catch (Exception ex) { _log.Warning("SetLeagueFormation failed: {Error}", ex); }
        }

        async Task LeagueSpinAsync(string code, bool elite)
        {
            // An elite spin was already paid for in Gems by the client-predicted action; if the league turns the
            // spin down (or the ask dies), give the Gems back.
            long gemCharge = elite ? LeagueEconomy.DefinitionFor(Model).EliteSpinGemCost : 0;
            try
            {
                LeagueOpResponse r = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueSpinRequest(code, elite));
                if (!string.IsNullOrEmpty(r.Error) && gemCharge > 0)
                    EnqueueServerAction(new PlayerRefundLeagueCharge(CurrencyType.Gems, gemCharge));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? code, r.Snapshot, r.Error, null));
            }
            catch (Exception ex)
            {
                _log.Warning("LeagueSpin failed: {Error}", ex);
                if (gemCharge > 0)
                    EnqueueServerAction(new PlayerRefundLeagueCharge(CurrencyType.Gems, gemCharge));
            }
        }

        async Task LeagueDraftPickAsync(string code, string legendId)
        {
            try
            {
                LeagueOpResponse r = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueDraftPickRequest(code, legendId));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? code, r.Snapshot, r.Error, null));
            }
            catch (Exception ex) { _log.Warning("LeagueDraftPick failed: {Error}", ex); }
        }

        async Task LeagueTransferSwapAsync(string code, string dropLegendId, string addLegendId, bool payWithGems)
        {
            // The signing fee was already charged from the wallet by the client-predicted action. Recompute it
            // from the same config the action used so a rejection refunds exactly what was paid.
            CurrencyType chargedCurrency = payWithGems ? CurrencyType.Gems : CurrencyType.Coins;
            long chargedAmount = 0;
            if (Model.GameConfig.Legends.TryGetValue(LegendId.FromString(addLegendId), out LegendPlayer addLegend))
            {
                LeagueDefinition def = LeagueEconomy.DefinitionFor(Model);
                chargedAmount = payWithGems ? def.MarqueeGemCostFor(addLegend.Ovr) : def.TransferCostFor(addLegend.Ovr);
            }
            try
            {
                LeagueOpResponse r = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueTransferSwapRequest(code, dropLegendId, addLegendId, payWithGems));
                if (string.IsNullOrEmpty(r.Error))
                    EnqueueServerAction(new PlayerRecordTransferMade());
                else if (chargedAmount > 0)
                    EnqueueServerAction(new PlayerRefundLeagueCharge(chargedCurrency, chargedAmount));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? code, r.Snapshot, r.Error, null));
            }
            catch (Exception ex)
            {
                _log.Warning("LeagueTransferSwap failed: {Error}", ex);
                if (chargedAmount > 0)
                    EnqueueServerAction(new PlayerRefundLeagueCharge(chargedCurrency, chargedAmount));
            }
        }

        async Task LeagueAutoPickAsync(string code, bool fillAll)
        {
            try
            {
                LeagueOpResponse r = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueAutoPickRequest(code, fillAll));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? code, r.Snapshot, r.Error, null));
            }
            catch (Exception ex) { _log.Warning("LeagueAutoPick failed: {Error}", ex); }
        }

        async Task SimulateLeagueAsync(string code)
        {
            try
            {
                LeagueOpResponse r = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueSimulateRequest(code));
                EnqueueServerAction(new PlayerSetLeagueState(r.Snapshot?.Code ?? code, r.Snapshot, r.Error, null));
            }
            catch (Exception ex) { _log.Warning("SimulateLeague failed: {Error}", ex); }
        }

        async Task RefreshFormAsync() => await RefreshFormSnapshotAsync();

        async Task RefreshFormSnapshotAsync()
        {
            try
            {
                FormGetResponse response = await EntityAskAsync<FormGetResponse>(FormActor.FormEntityId, new FormGetRequest());
                FormSnapshot snapshot = new FormSnapshot();
                if (response?.Deltas != null)
                {
                    foreach ((string name, int delta) in response.Deltas)
                        snapshot.Entries.Add(new FormEntry(name, delta));
                }
                EnqueueServerAction(new PlayerSetFormSnapshot(snapshot));
            }
            catch (Exception ex)
            {
                _log.Warning("Failed to refresh form snapshot: {Error}", ex);
            }
        }

        // FOOTDRAFT: roll a spin bucket for a draft slot server-side (cheat-proof randomization) and write it back
        // as the pending offer. The RNG runs only here, so a client cannot predict or airplane-mode-scum the spin.
        void SpinDraftSlotInternal(int slot)
        {
            DraftedSquad draft = Model.Draft;
            if (draft == null || draft.Locked || !draft.HasFormation)
                return;
            if (!Model.GameConfig.Formations.TryGetValue(draft.Formation, out FormationInfo formation))
                return;
            if (slot < 0 || slot >= formation.Slots.Count || draft.IsSlotFilled(slot))
                return;

            // Build buckets + a lookup from the legend corpus (static content in v1; production reads hot-config).
            List<LegendPlayer> corpus = new List<LegendPlayer>(LegendContent.Legends);
            Dictionary<string, LegendPlayer> byId = new Dictionary<string, LegendPlayer>();
            foreach (LegendPlayer p in corpus)
                byId[p.Id.Value] = p;
            Func<LegendId, LegendPlayer> lookup = id => byId.TryGetValue(id.Value, out LegendPlayer p) ? p : null;

            // The legends already drafted (excluded from candidacy).
            HashSet<string> picked = new HashSet<string>();
            foreach ((int _, LegendId id) in draft.Picks)
                picked.Add(id.Value);

            // Prefer buckets that actually offer an undrafted candidate for this slot's position (avoid dead spins),
            // falling back to all buckets if none qualify.
            Position need = formation.Slots[slot];
            List<SpinBucket> allBuckets = DraftEngine.BuildBuckets(corpus);
            List<SpinBucket> viable = new List<SpinBucket>();
            foreach (SpinBucket b in allBuckets)
                if (DraftEngine.CandidatesForSlot(b, need, lookup, picked).Count > 0)
                    viable.Add(b);
            List<SpinBucket> pool = viable.Count > 0 ? viable : allBuckets;
            if (pool.Count == 0)
                return;

            Metaplay.Core.RandomPCG rng = Metaplay.Core.RandomPCG.CreateNew();
            SpinBucket chosen = DraftEngine.Spin(pool, rng);
            EnqueueServerAction(new PlayerSetSpinOffer(slot, chosen));
        }

        void LeaveMatchInternal()
        {
            if (Model.CurrentMatch != EntityId.None)
            {
                Model.CurrentMatch = EntityId.None;
                RemoveEntityAssociation(ClientSlotGame.Match);
            }
        }

        /// <summary>
        /// Handles the lobby's request to place this player into a newly created match.
        /// </summary>
        [EntityAskHandler]
        public EntityAskOk HandlePlayerAssignToMatchRequest(EntityId lobbyId, PlayerAssignToMatchRequest request)
        {
            // Refuse if the player is already in a different match, so the lobby doesn't double-book them.
            if (Model.CurrentMatch != EntityId.None && Model.CurrentMatch != request.MatchId)
                throw new InvalidEntityAsk($"Player {_entityId} is already in match {Model.CurrentMatch}");

            Model.CurrentMatch = request.MatchId;
            Model.CurrentLobby = EntityId.None;

            // Switch the client's association from the lobby to the match.
            RemoveEntityAssociation(ClientSlotGame.Lobby);
            UpdateMatchAssociation();

            return EntityAskOk.Instance;
        }

        /// <summary>
        /// The match asks for this player's squad at setup. Resolve it from the player's selected team and card
        /// upgrades so the dice baked into the match reflect the manager's progression (server-authoritative).
        /// </summary>
        [EntityAskHandler]
        public PlayerGetSquadResponse HandlePlayerGetSquadRequest(EntityId matchId, PlayerGetSquadRequest request)
        {
            SharedGameConfig config = Model.GameConfig;

            // Crest = the manager's equipped avatar glyph, falling back to a ball.
            string crest = "⚽";
            if (!string.IsNullOrEmpty(Model.Cosmetics.EquippedAvatar)
                && config.Cosmetics.TryGetValue(Model.Cosmetics.EquippedAvatar, out CosmeticItem avatar))
                crest = avatar.Glyph;

            return new PlayerGetSquadResponse(Model.ResolveDraftRatings(), crest, Model.PlayerLevel);
        }

        /// <summary>
        /// The match reports its outcome for this player. Enqueue the configured reward grant on the player's own
        /// timeline (works whether or not the client is currently connected).
        /// </summary>
        [MessageHandler]
        public void HandleGrantMatchRewardsMessage(EntityId matchId, GrantMatchRewardsMessage message)
        {
            EnqueueServerAction(new PlayerGrantMatchRewards(message.Won, message.RoundsWon));

            // Report this match's Club Points to the Club League and refresh the standings snapshot.
            if (Model.Club.InClub)
            {
                GlobalConfig global = Model.GameConfig.Global;
                int  clubPoints = (message.Won ? global.ClubPointsPerWin : global.ClubPointsPerLoss) + message.RoundsWon * global.ClubPointsPerRoundWon;
                long weekId     = ClubWeek.CurrentWeekId(MetaTime.Now, global);
                string clubName = Model.Club.Name;
                CastMessage(ClubsActor.ClubsEntityId, new ClubReportPoints(clubName, clubPoints, weekId));
                EnqueueOnActorContext(() => RefreshClubSnapshotAsync(clubName));
            }
        }

        void UpdateLobbyAssociation()
        {
            AddEntityAssociation(
                new AssociatedEntityRefBase.Default(ClientSlotGame.Lobby, _entityId, Model.CurrentLobby),
                removeOnSessionEnd: true);
        }

        void UpdateMatchAssociation()
        {
            AddEntityAssociation(
                new AssociatedEntityRefBase.Default(ClientSlotGame.Match, _entityId, Model.CurrentMatch),
                removeOnSessionEnd: true);
        }

        protected override Task<bool> OnAssociatedEntityRefusalAsync(AssociatedEntityRefBase association, InternalEntitySubscribeRefusedBase refusal)
        {
            if (refusal is InternalEntitySubscribeRefusedBase.Builtins.EntityNotSetUp)
            {
                if (association.GetClientSlot() == ClientSlotGame.Match)
                {
                    // Match no longer exists (e.g. it ended). Clear it so the player returns to the lobby.
                    Model.CurrentMatch = EntityId.None;
                    return Task.FromResult(true);
                }
                if (association.GetClientSlot() == ClientSlotGame.Lobby)
                {
                    Model.CurrentLobby = EntityId.None;
                    return Task.FromResult(true);
                }
            }

            return base.OnAssociatedEntityRefusalAsync(association, refusal);
        }
    }
}
