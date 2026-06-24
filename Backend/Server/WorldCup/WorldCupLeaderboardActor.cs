// FOOTDRAFT — singleton, persisted World Cup leaderboard. Managers report their best WC run when one finishes
// (a fire-and-forget cast) and fetch a ranked top-N snapshot for the WC hub (EntityAsk). Mirrors the persisted
// LeagueActor pattern so the board survives restarts/redeploys.

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Persistence;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading.Tasks;

namespace Game.Server
{
    // ---- Messages ----

    /// <summary> Report a manager's best World Cup run (sent when a run ends). EntityAsk (matches the codebase). </summary>
    [MetaMessage(MessageCodes.WcLeaderboardReport, MessageDirection.ServerInternal)]
    public class WcLeaderboardReport : EntityAskRequest<EntityAskOk>
    {
        public string Name      { get; private set; }
        public int    Titles    { get; private set; }
        public int    BestRound { get; private set; }
        public int    BestXiOvr { get; private set; }
        public int    Runs      { get; private set; }

        WcLeaderboardReport() { }
        public WcLeaderboardReport(string name, int titles, int bestRound, int bestXiOvr, int runs)
        {
            Name = name; Titles = titles; BestRound = bestRound; BestXiOvr = bestXiOvr; Runs = runs;
        }
    }

    /// <summary> Fetch the top-N World Cup leaderboard + the caller's rank. </summary>
    [MetaMessage(MessageCodes.WcLeaderboardGetSnapshotRequest, MessageDirection.ServerInternal)]
    public class WcLeaderboardGetSnapshotRequest : EntityAskRequest<WcLeaderboardGetSnapshotResponse>
    {
        public int TopN { get; private set; }
        WcLeaderboardGetSnapshotRequest() { }
        public WcLeaderboardGetSnapshotRequest(int topN) { TopN = topN; }
    }

    [MetaMessage(MessageCodes.WcLeaderboardGetSnapshotRequest + 100_000, MessageDirection.ServerInternal)]
    public class WcLeaderboardGetSnapshotResponse : EntityAskResponse
    {
        public WorldCupLeaderboardSnapshot Snapshot { get; private set; }
        WcLeaderboardGetSnapshotResponse() { }
        public WcLeaderboardGetSnapshotResponse(WorldCupLeaderboardSnapshot snapshot) { Snapshot = snapshot; }
    }

    // ---- Persistence ----

    [Table("WorldCupLeaderboards")]
    public class PersistedWcLeaderboard : IPersistedEntity
    {
        [Key] [PartitionKey] [Required] [MaxLength(64)] [Column(TypeName = "varchar(64)")]
        public string   EntityId      { get; set; }
        [Required] [Column(TypeName = "DateTime")]
        public DateTime PersistedAt   { get; set; }
        [Required] public byte[] Payload       { get; set; }
        [Required] public int    SchemaVersion { get; set; }
        [Required] public bool   IsFinal       { get; set; }
    }

    [MetaSerializable]
    public class PersistedWcEntry
    {
        [MetaMember(1)] public EntityId PlayerId  { get; set; }
        [MetaMember(2)] public string   Name      { get; set; } = "";
        [MetaMember(3)] public int      Titles    { get; set; }
        [MetaMember(4)] public int      BestRound { get; set; } = -1;
        [MetaMember(5)] public int      BestXiOvr { get; set; }
        [MetaMember(6)] public int      Runs      { get; set; }
    }

    [MetaSerializable]
    [SupportedSchemaVersions(1, 1)]
    public class WcLeaderboardModel : ISchemaMigratable
    {
        [MetaMember(1)] public List<PersistedWcEntry> Entries { get; set; } = new List<PersistedWcEntry>();
    }

    // ---- Actor ----

    [EntityConfig]
    public class WorldCupLeaderboardConfig : PersistedEntityConfig
    {
        public override EntityKind        EntityKind           => EntityKindGame.WorldCupLeaderboard;
        public override Type              EntityActorType      => typeof(WorldCupLeaderboardActor);
        public override NodeSetPlacement  NodeSetPlacement     => NodeSetPlacement.Service;
        public override IShardingStrategy ShardingStrategy     => ShardingStrategies.CreateSingletonService();
        public override TimeSpan          ShardShutdownTimeout => TimeSpan.FromSeconds(10);
    }

    public class WorldCupLeaderboardActor : PersistedEntityActor<PersistedWcLeaderboard, WcLeaderboardModel>
    {
        public static readonly EntityId LeaderboardEntityId = EntityId.Create(EntityKindGame.WorldCupLeaderboard, 0);

        // Player id → their best reported run. In memory; snapshotted to the DB.
        readonly Dictionary<EntityId, PersistedWcEntry> _entries = new Dictionary<EntityId, PersistedWcEntry>();

        sealed class KeepAliveCommand { public static readonly KeepAliveCommand Instance = new KeepAliveCommand(); }

        protected override TimeSpan           SnapshotInterval => TimeSpan.FromSeconds(30);
        protected override AutoShutdownPolicy ShutdownPolicy   => AutoShutdownPolicy.ShutdownNever();

        protected override async Task Initialize()
        {
            PersistedWcLeaderboard persisted = await MetaDatabase.Get().TryGetAsync<PersistedWcLeaderboard>(_entityId.ToString());
            await InitializePersisted(persisted);
            StartPeriodicTimer(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30), KeepAliveCommand.Instance);
        }

        [CommandHandler]
        void HandleKeepAlive(KeepAliveCommand _) { }

        protected override Task<WcLeaderboardModel> InitializeNew() => Task.FromResult(new WcLeaderboardModel());

        protected override Task<WcLeaderboardModel> RestoreFromPersisted(PersistedWcLeaderboard persisted)
            => Task.FromResult(DeserializePersistedPayload<WcLeaderboardModel>(persisted.Payload, resolver: null, logicVersion: null));

        protected override Task PostLoad(WcLeaderboardModel payload, DateTime persistedAt, TimeSpan elapsedTime)
        {
            _entries.Clear();
            if (payload?.Entries != null)
                foreach (PersistedWcEntry e in payload.Entries)
                    if (e.PlayerId != EntityId.None)
                        _entries[e.PlayerId] = e;
            return Task.CompletedTask;
        }

        protected override async Task PersistStateImpl(bool isInitial, bool isFinal)
        {
            WcLeaderboardModel model = new WcLeaderboardModel();
            foreach (PersistedWcEntry e in _entries.Values)
                model.Entries.Add(e);
            byte[] payload = SerializeToPersistedPayload(model, resolver: null, logicVersion: null);
            await MetaDatabase.Get().InsertOrUpdateAsync(new PersistedWcLeaderboard
            {
                EntityId      = _entityId.ToString(),
                PersistedAt   = DateTime.UtcNow,
                Payload       = payload,
                SchemaVersion = CurrentSchemaVersion,
                IsFinal       = isFinal,
            }).ConfigureAwait(false);
        }

        [EntityAskHandler]
        public EntityAskOk HandleReport(EntityId playerId, WcLeaderboardReport msg)
        {
            // Honours are monotonic all-time bests, so the latest report is always >= the stored one — overwrite.
            _entries[playerId] = new PersistedWcEntry
            {
                PlayerId  = playerId,
                Name      = string.IsNullOrEmpty(msg.Name) ? "Manager" : msg.Name,
                Titles    = msg.Titles,
                BestRound = msg.BestRound,
                BestXiOvr = msg.BestXiOvr,
                Runs      = msg.Runs,
            };
            return EntityAskOk.Instance;
        }

        [EntityAskHandler]
        public WcLeaderboardGetSnapshotResponse HandleGetSnapshot(EntityId playerId, WcLeaderboardGetSnapshotRequest request)
        {
            // Build + rank entries (titles → deepest round → best XI → fewer runs).
            List<EntityId>           ids     = new List<EntityId>(_entries.Count);
            List<WcLeaderboardEntry> ranked  = new List<WcLeaderboardEntry>(_entries.Count);
            foreach ((EntityId pid, PersistedWcEntry e) in _entries)
            {
                ids.Add(pid);
                ranked.Add(new WcLeaderboardEntry(e.Name, e.Titles, e.BestRound, e.BestXiOvr, e.Runs));
            }
            // Sort ids + entries together by the shared comparison.
            int[] order = new int[ranked.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (x, y) => WcLeaderboard.Compare(ranked[x], ranked[y]));

            WorldCupLeaderboardSnapshot snap = new WorldCupLeaderboardSnapshot { TotalPlayers = ranked.Count };
            int topN = request.TopN > 0 ? request.TopN : 50;
            for (int rank = 0; rank < order.Length; rank++)
            {
                int idx = order[rank];
                WcLeaderboardEntry e = ranked[idx];
                e.Rank = rank + 1;
                if (rank < topN)
                    snap.Top.Add(e);
                if (ids[idx] == playerId)
                {
                    snap.MyRank      = rank + 1;
                    snap.MyTitles    = e.Titles;
                    snap.MyBestRound = e.BestRound;
                }
            }
            return new WcLeaderboardGetSnapshotResponse(snap);
        }
    }
}
