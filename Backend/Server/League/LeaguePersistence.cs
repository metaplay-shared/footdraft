// FOOTDRAFT / 38-0-20 — database persistence for the league registry.
//
// The LeagueActor keeps all leagues in memory; to survive server restarts/redeploys (so a multi-day season
// isn't lost), it snapshots that registry into a single DB row (PersistedLeagueRegistry) on a timer + at
// shutdown, and reloads it on startup. Only the essential state is persisted — fixtures, the "played" set,
// the "taken" pool and locked line-ratings are all re-derived on load.

using Game.Logic;
using Metaplay.Cloud.Persistence;
using Metaplay.Core;
using Metaplay.Core.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Game.Server
{
    /// <summary> The single DB row holding the serialized league registry (singleton). </summary>
    [Table("LeagueRegistries")]
    public class PersistedLeagueRegistry : IPersistedEntity
    {
        [Key]
        [PartitionKey]
        [Required]
        [MaxLength(64)]
        [Column(TypeName = "varchar(64)")]
        public string   EntityId      { get; set; }

        [Required]
        [Column(TypeName = "DateTime")]
        public DateTime PersistedAt   { get; set; }

        [Required]
        public byte[]   Payload       { get; set; }

        [Required]
        public int      SchemaVersion { get; set; }

        [Required]
        public bool     IsFinal       { get; set; }
    }

    /// <summary> One persisted manager/CPU team: identity, chosen formation and drafted XI (slot → legend id). </summary>
    [MetaSerializable]
    public class PersistedLeagueMember
    {
        [MetaMember(1)] public EntityId                    Id        { get; set; }
        [MetaMember(2)] public string                      Name      { get; set; }
        [MetaMember(3)] public string                      Crest     { get; set; }
        [MetaMember(4)] public bool                        IsBot          { get; set; }
        [MetaMember(5)] public string                      Formation      { get; set; }
        [MetaMember(6)] public MetaDictionary<int, string> Roster         { get; set; } = new MetaDictionary<int, string>();
        [MetaMember(7)] public long                        TransferBudget { get; set; } // Coins this manager can spend on transfers
    }

    /// <summary> One persisted league. Fixtures / played-set / taken-pool / line-ratings are re-derived on load. </summary>
    [MetaSerializable]
    public class PersistedLeague
    {
        [MetaMember(1)]  public string                      Code               { get; set; }
        [MetaMember(2)]  public string                      Name               { get; set; }
        [MetaMember(3)]  public EntityId                    Commissioner       { get; set; }
        [MetaMember(4)]  public LeagueState                 State              { get; set; }
        [MetaMember(5)]  public List<PersistedLeagueMember> Members            { get; set; } = new List<PersistedLeagueMember>();
        [MetaMember(6)]  public List<LeagueResult>          Results            { get; set; } = new List<LeagueResult>();
        [MetaMember(7)]  public int                         DraftPick          { get; set; }
        [MetaMember(8)]  public int                         CurrentMatchday    { get; set; }
        [MetaMember(9)]  public MetaTime                    NextSimTime        { get; set; }
        [MetaMember(10)] public int                         LastMatchdayNumber { get; set; }
        [MetaMember(11)] public List<string>                LastMatchdayLines  { get; set; } = new List<string>();
        /// <summary> Admin transfer-window override: 0 = follow schedule, 1 = force open, 2 = force closed. </summary>
        [MetaMember(12)] public int                         TransferWindowOverride { get; set; }
    }

    /// <summary> The serialized payload of <see cref="PersistedLeagueRegistry"/>: every league in the registry. </summary>
    [MetaSerializable]
    [SupportedSchemaVersions(1, 1)]
    public class LeagueRegistryModel : ISchemaMigratable
    {
        [MetaMember(1)] public List<PersistedLeague> Leagues { get; set; } = new List<PersistedLeague>();
    }
}
