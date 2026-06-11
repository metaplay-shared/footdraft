// FOOTDRAFT — singleton Clubs registry + Club League standings.

using Game.Logic;
using Metaplay.Cloud.Entity;
using Metaplay.Cloud.Sharding;
using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity;
using Metaplay.Server.MultiplayerEntity;
using Metaplay.Server.MultiplayerEntity.InternalMessages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Game.Server
{
    [MetaSerializableDerived(3)]
    public class ClubsSetupParams : IMultiplayerEntitySetupParams
    {
        public ClubsSetupParams() { }
    }

    [EntityConfig]
    public class ClubsConfig : EphemeralEntityConfig
    {
        public override EntityKind        EntityKind           => EntityKindGame.Clubs;
        public override Type              EntityActorType      => typeof(ClubsActor);
        public override NodeSetPlacement  NodeSetPlacement     => NodeSetPlacement.Service;
        public override IShardingStrategy ShardingStrategy     => ShardingStrategies.CreateSingletonService();
        public override TimeSpan          ShardShutdownTimeout => TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Singleton service holding club membership and the weekly Club League standings in memory. Players join/
    /// leave via EntityAsk, report match Club Points via a cast, and fetch a standings snapshot for display.
    /// Weekly points reset when the league window rolls over. (A migration-free stand-in for Metaplay's
    /// first-party Guild + Leagues frameworks, which persist this in production.)
    /// </summary>
    public class ClubsActor : EphemeralMultiplayerEntityActorBase<ClubsModel, ClubsAction>
    {
        public static readonly EntityId ClubsEntityId = EntityId.Create(EntityKindGame.Clubs, 0);

        sealed class ClubReg
        {
            public readonly string                       Name;
            public readonly Dictionary<EntityId, string> Members = new Dictionary<EntityId, string>();
            public long                                  Points;
            public ClubReg(string name) { Name = name; }
        }

        readonly Dictionary<string, ClubReg> _clubs = new Dictionary<string, ClubReg>();
        long _weekId = -1;

        sealed class WeeklyCheckCommand { public static readonly WeeklyCheckCommand Instance = new WeeklyCheckCommand(); }

        protected override bool IsTicking => false;

        GlobalConfig Global => ((SharedGameConfig)_baselineGameConfig.SharedConfig).Global;

        protected override async Task Initialize()
        {
            await base.Initialize();
            if (Model == null)
                await SetUpEntity(new ClubsSetupParams());
            // Keep the singleton alive and check for the weekly reset.
            StartPeriodicTimer(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), WeeklyCheckCommand.Instance);
        }

        protected override Task SetUpModelAsync(ClubsModel model, IMultiplayerEntitySetupParams setupParams)
            => Task.CompletedTask;

        [CommandHandler]
        void HandleWeeklyCheck(WeeklyCheckCommand _) => SyncWeek();

        void SyncWeek()
        {
            long week = ClubWeek.CurrentWeekId(MetaTime.Now, Global);
            if (week == _weekId)
                return;
            _weekId = week;
            foreach (ClubReg club in _clubs.Values)
                club.Points = 0;
        }

        static string Key(string name) => (name ?? "").Trim().ToLowerInvariant();

        [EntityAskHandler]
        public EntityAskOk HandleClubJoinRequest(EntityId playerId, ClubJoinRequest request)
        {
            SyncWeek();
            RemoveFromAllClubs(playerId);

            string key = Key(request.ClubName);
            if (!_clubs.TryGetValue(key, out ClubReg club))
            {
                club = new ClubReg(request.ClubName.Trim());
                _clubs[key] = club;
            }
            club.Members[playerId] = request.PlayerName;
            return EntityAskOk.Instance;
        }

        [EntityAskHandler]
        public EntityAskOk HandleClubLeaveRequest(EntityId playerId, ClubLeaveRequest request)
        {
            RemoveFromAllClubs(playerId);
            return EntityAskOk.Instance;
        }

        [MessageHandler]
        public void HandleClubReportPoints(EntityId playerId, ClubReportPoints message)
        {
            SyncWeek();
            if (message.WeekId != _weekId)
                return; // stale report from a previous week
            if (_clubs.TryGetValue(Key(message.ClubName), out ClubReg club) && club.Members.ContainsKey(playerId))
                club.Points += message.Points;
        }

        [EntityAskHandler]
        public ClubGetSnapshotResponse HandleClubGetSnapshotRequest(EntityId playerId, ClubGetSnapshotRequest request)
        {
            SyncWeek();

            List<ClubReg> ranked = _clubs.Values
                .OrderByDescending(c => c.Points)
                .ThenBy(c => c.Name, StringComparer.Ordinal)
                .ToList();

            ClubSnapshot snapshot = new ClubSnapshot { WeekId = _weekId, TotalClubs = ranked.Count };
            for (int i = 0; i < ranked.Count && i < request.TopN; i++)
                snapshot.Top.Add(new ClubStanding(ranked[i].Name, ranked[i].Points, ranked[i].Members.Count));

            string myKey = Key(request.ClubName);
            for (int i = 0; i < ranked.Count; i++)
            {
                if (Key(ranked[i].Name) == myKey)
                {
                    snapshot.MyRank      = i + 1;
                    snapshot.MyPoints    = ranked[i].Points;
                    snapshot.MemberCount = ranked[i].Members.Count;
                    break;
                }
            }
            return new ClubGetSnapshotResponse(snapshot);
        }

        void RemoveFromAllClubs(EntityId playerId)
        {
            List<string> emptied = new List<string>();
            foreach ((string key, ClubReg club) in _clubs)
            {
                if (club.Members.Remove(playerId) && club.Members.Count == 0)
                    emptied.Add(key);
            }
            foreach (string key in emptied)
                _clubs.Remove(key);
        }
    }
}
