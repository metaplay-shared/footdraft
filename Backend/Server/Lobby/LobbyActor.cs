// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity;
using Metaplay.Server;
using Metaplay.Server.MultiplayerEntity;
using Metaplay.Server.MultiplayerEntity.InternalMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Game.Server
{
    /// <summary>
    /// Setup parameters for the (singleton) lobby. The lobby self-initializes with an empty queue, so no
    /// parameters are required.
    /// </summary>
    [MetaSerializableDerived(2)]
    public class LobbySetupParams : IMultiplayerEntitySetupParams
    {
        public LobbySetupParams() { }
    }

    [EntityConfig]
    public class LobbyConfig : EphemeralEntityConfig
    {
        public override EntityKind        EntityKind           => EntityKindGame.Lobby;
        public override Type              EntityActorType      => typeof(LobbyActor);
        // Singleton service: exactly one lobby instance exists across the whole cluster.
        public override NodeSetPlacement  NodeSetPlacement     => NodeSetPlacement.Service;
        public override IShardingStrategy ShardingStrategy     => ShardingStrategies.CreateSingletonService();
        public override TimeSpan          ShardShutdownTimeout => TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Server actor for the global matchmaking lobby. Players enqueue via <see cref="LobbyEnqueuePlayerRequest"/>;
    /// the actor runs a periodic check that starts a countdown once the minimum number of players is queued and
    /// forms a <see cref="MatchActor"/> when the countdown elapses (or the queue fills to the maximum). Players
    /// may also force an immediate start via <see cref="LobbyStartNowRequest"/>, in which case the match is
    /// padded with bots up to the minimum player count.
    /// </summary>
    public class LobbyActor : EphemeralMultiplayerEntityActorBase<LobbyModel, LobbyAction>
    {
        /// <summary> Well-known EntityId of the singleton lobby. </summary>
        public static readonly EntityId LobbyEntityId = EntityId.Create(EntityKindGame.Lobby, 0);

        sealed class CheckMatchmakingCommand { public static readonly CheckMatchmakingCommand Instance = new CheckMatchmakingCommand(); }

        /// <summary> An open private room awaiting a second player, keyed by its shared code. </summary>
        sealed class FriendlyRoom
        {
            public readonly EntityId Creator;
            public readonly string   CreatorName;
            public readonly MetaTime CreatedAt;
            public FriendlyRoom(EntityId creator, string creatorName, MetaTime createdAt)
            {
                Creator     = creator;
                CreatorName = creatorName;
                CreatedAt   = createdAt;
            }
        }

        // Private "play a friend" rooms held in actor memory (not persisted). The lobby's periodic timer keeps
        // the singleton alive, so rooms survive between the create and the join.
        readonly Dictionary<string, FriendlyRoom> _friendlyRooms = new Dictionary<string, FriendlyRoom>();
        const int FriendlyRoomTimeoutSeconds = 120;

        // The lobby has no tick-based game logic; matchmaking is driven by the periodic timer below.
        protected override bool IsTicking => false;

        protected override async Task Initialize()
        {
            await base.Initialize();

            // The lobby is a singleton that is never explicitly set up via a setup request, so set it up here
            // on first wake-up. (After all players leave, the actor shuts down and re-initializes when woken.)
            if (Model == null)
                await SetUpEntity(new LobbySetupParams());

            // Evaluate matchmaking once per second.
            StartPeriodicTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), CheckMatchmakingCommand.Instance);
        }

        protected override Task SetUpModelAsync(LobbyModel model, IMultiplayerEntitySetupParams setupParams)
        {
            // Nothing to initialize; the queue starts empty.
            return Task.CompletedTask;
        }

        [EntityAskHandler]
        public EntityAskOk HandleLobbyEnqueuePlayerRequest(EntityId playerId, LobbyEnqueuePlayerRequest request)
        {
            if (Model == null)
                throw new InternalEntityAskNotSetUpRefusal();

            ExecuteAction(new LobbyAddQueuedPlayer(playerId, request.PlayerName));
            return EntityAskOk.Instance;
        }

        [EntityAskHandler]
        public EntityAskOk HandleLobbyStartNowRequest(EntityId playerId, LobbyStartNowRequest request)
        {
            if (Model == null)
                throw new InternalEntityAskNotSetUpRefusal();

            // Force an immediate start with whoever is queued, padding with bots up to the minimum.
            StartMatch(SnapshotQueue(), padWithBots: true);
            return EntityAskOk.Instance;
        }

        protected override void OnParticipantSessionEnded(EntitySubscriber session)
        {
            // When a queued player disconnects, drop them from the queue so they aren't matched while offline.
            if (Model == null)
                return;

            EntityId playerId = SessionIdUtil.ToPlayerId(session.EntityId);
            if (Model.QueuedPlayers.ContainsKey(playerId))
                ExecuteAction(new LobbyRemoveQueuedPlayer(playerId));
        }

        [EntityAskHandler]
        public EntityAskOk HandleLobbyCreateFriendlyRequest(EntityId playerId, LobbyCreateFriendlyRequest request)
        {
            // Open (or refresh) the room for this code. Last creator of a given code wins; collisions are rare
            // with random codes and harmless for a private-room flow.
            _friendlyRooms[request.Code] = new FriendlyRoom(playerId, request.PlayerName, MetaTime.Now);
            return EntityAskOk.Instance;
        }

        [EntityAskHandler]
        public EntityAskOk HandleLobbyJoinFriendlyRequest(EntityId playerId, LobbyJoinFriendlyRequest request)
        {
            if (Model == null)
                throw new InternalEntityAskNotSetUpRefusal();
            if (!_friendlyRooms.TryGetValue(request.Code, out FriendlyRoom room))
                throw new InvalidEntityAsk($"No open room with code {request.Code}");
            if (room.Creator == playerId)
                throw new InvalidEntityAsk("Cannot join your own room");

            _friendlyRooms.Remove(request.Code);

            // Form the 1v1 match from the two friends (no bot padding).
            MetaDictionary<EntityId, string> humans = new MetaDictionary<EntityId, string>();
            humans[room.Creator] = room.CreatorName;
            humans[playerId]     = request.PlayerName;
            StartMatch(humans, padWithBots: false);
            return EntityAskOk.Instance;
        }

        [EntityAskHandler]
        public EntityAskOk HandleLobbyCancelFriendlyRequest(EntityId playerId, LobbyCancelFriendlyRequest request)
        {
            RemoveFriendlyRoomsOf(playerId);
            return EntityAskOk.Instance;
        }

        void RemoveFriendlyRoomsOf(EntityId playerId)
        {
            List<string> toRemove = new List<string>();
            foreach ((string code, FriendlyRoom room) in _friendlyRooms)
                if (room.Creator == playerId)
                    toRemove.Add(code);
            foreach (string code in toRemove)
                _friendlyRooms.Remove(code);
        }

        void PruneStaleFriendlyRooms()
        {
            MetaTime cutoff = MetaTime.Now - MetaDuration.FromSeconds(FriendlyRoomTimeoutSeconds);
            List<string> toRemove = new List<string>();
            foreach ((string code, FriendlyRoom room) in _friendlyRooms)
                if (room.CreatedAt < cutoff)
                    toRemove.Add(code);
            foreach (string code in toRemove)
                _friendlyRooms.Remove(code);
        }

        [CommandHandler]
        void HandleCheckMatchmaking(CheckMatchmakingCommand _)
        {
            if (Model == null)
                return;

            PruneStaleFriendlyRooms();

            int queuedCount = Model.QueuedPlayers.Count;

            if (queuedCount >= LobbyModel.MaxPlayers)
            {
                StartMatch(SnapshotQueue(), padWithBots: false);
            }
            else if (queuedCount >= LobbyModel.MinPlayers)
            {
                if (!Model.HasCountdown)
                    ExecuteAction(new LobbySetCountdown(MetaTime.Now + MetaDuration.FromSeconds(LobbyModel.CountdownSeconds)));
                else if (MetaTime.Now >= Model.CountdownEndsAt)
                    StartMatch(SnapshotQueue(), padWithBots: false);
            }
            else
            {
                // Not enough players queued; cancel any pending countdown.
                if (Model.HasCountdown)
                    ExecuteAction(new LobbySetCountdown(MetaTime.Epoch));
            }
        }

        /// <summary>
        /// Snapshots up to <see cref="LobbyModel.MaxPlayers"/> currently-queued players.
        /// </summary>
        MetaDictionary<EntityId, string> SnapshotQueue()
        {
            MetaDictionary<EntityId, string> humans = new MetaDictionary<EntityId, string>();
            foreach ((EntityId playerId, string playerName) in Model.QueuedPlayers)
            {
                humans[playerId] = playerName;
                if (humans.Count >= LobbyModel.MaxPlayers)
                    break;
            }
            return humans;
        }

        /// <summary>
        /// Forms a match from the given human players. If <paramref name="padWithBots"/> is set, the match is
        /// padded with bot players up to <see cref="LobbyModel.MaxPlayers"/>; otherwise the match is only formed
        /// if there are already at least <see cref="LobbyModel.MinPlayers"/> human players.
        /// </summary>
        void StartMatch(MetaDictionary<EntityId, string> humans, bool padWithBots)
        {
            if (humans.Count == 0)
                return;
            if (!padWithBots && humans.Count < LobbyModel.MinPlayers)
                return;

            // Remove the chosen players from the queue and clear the countdown immediately so the next
            // periodic check doesn't form a duplicate match for the same players.
            foreach ((EntityId playerId, string _) in humans)
                ExecuteAction(new LobbyRemoveQueuedPlayer(playerId));
            ExecuteAction(new LobbySetCountdown(MetaTime.Epoch));

            MetaDictionary<EntityId, string> allPlayers = new MetaDictionary<EntityId, string>();
            foreach ((EntityId playerId, string playerName) in humans)
                allPlayers[playerId] = playerName;

            List<EntityId> humanIds = humans.Keys.ToList();
            List<EntityId> botIds = new List<EntityId>();
            if (padWithBots)
            {
                int botNumber = 1;
                while (allPlayers.Count < LobbyModel.MaxPlayers)
                {
                    EntityId botId = EntityId.CreateRandom(EntityKindCore.Player);
                    allPlayers[botId] = $"CPU {botNumber}";
                    botIds.Add(botId);
                    botNumber++;
                }
            }

            EnqueueOnActorContext(() => CreateMatchAsync(allPlayers, humanIds, botIds, numRetries: 3));
        }

        async Task CreateMatchAsync(MetaDictionary<EntityId, string> allPlayers, List<EntityId> humanIds, List<EntityId> botIds, int numRetries)
        {
            EntityId matchId = EntityId.CreateRandom(EntityKindGame.Match);

            try
            {
                await EntityAskAsync<InternalEntitySetupResponse>(matchId, new InternalEntitySetupRequest(new MatchSetupParams(allPlayers, botIds)));
            }
            catch (InternalEntitySetupRefusal) when (numRetries > 0)
            {
                // EntityId collision (extremely unlikely): retry with a fresh id.
                EnqueueOnActorContext(() => CreateMatchAsync(allPlayers, humanIds, botIds, numRetries - 1));
                return;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to set up match {MatchId}; returning human players to the queue", matchId);
                foreach (EntityId playerId in humanIds)
                {
                    if (allPlayers.TryGetValue(playerId, out string playerName))
                        ExecuteAction(new LobbyAddQueuedPlayer(playerId, playerName));
                }
                return;
            }

            // Assign each human participant to the match via their PlayerActor. (Bots have no PlayerActor.)
            foreach (EntityId playerId in humanIds)
            {
                try
                {
                    await EntityAskAsync<EntityAskOk>(playerId, new PlayerAssignToMatchRequest(matchId));
                }
                catch (Exception ex)
                {
                    _log.Warning("Failed to assign player {PlayerId} to match {MatchId}: {Error}", playerId, matchId, ex);
                }
            }

            _log.Info("Created match {MatchId} with {HumanCount} player(s) and {BotCount} bot(s)", matchId, humanIds.Count, botIds.Count);
        }
    }
}
