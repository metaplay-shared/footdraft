// FOOTDRAFT — flat registry views for the LiveOps Dashboard "Season Leagues" page.
//
// The per-viewer LeagueSnapshot is relative to one manager; the dashboard needs a flat, admin-wide listing of
// every league the singleton is running. These [MetaSerializable] views are returned over the admin API
// (EntityAsk → controller → dashboard JSON).

using System.Collections.Generic;
using Game.Logic;
using Metaplay.Core.Model;

namespace Game.Server
{
    /// <summary> One row in the dashboard's all-leagues list. </summary>
    [MetaSerializable]
    public class LeagueRegistryEntryView
    {
        [MetaMember(1)] public string      Code            { get; set; } = "";
        [MetaMember(2)] public string      Name            { get; set; } = "";
        [MetaMember(3)] public LeagueState State           { get; set; }
        [MetaMember(4)] public int         MemberCount     { get; set; } // humans + CPU fill
        [MetaMember(5)] public int         HumanCount      { get; set; }
        [MetaMember(6)] public int         CurrentMatchday { get; set; }
        [MetaMember(7)] public int         TotalMatchdays  { get; set; }
        [MetaMember(8)] public long        NextSimAtMillis { get; set; } // 0 when not actively running
    }

    /// <summary> The whole registry, flattened for the dashboard list page. </summary>
    [MetaSerializable]
    public class LeagueRegistryListView
    {
        [MetaMember(1)] public List<LeagueRegistryEntryView> Leagues { get; set; } = new List<LeagueRegistryEntryView>();
    }
}
