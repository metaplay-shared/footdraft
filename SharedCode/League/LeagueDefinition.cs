// FOOTDRAFT — the daily season league, defined in game config (not hardcoded).
//
// The "default" league that runs every day at 7pm used to live as constants in LeagueActor. It now lives here
// as config data, so its schedule, size, formation default and per-matchday "events" can be tuned (and, once the
// config is Google-Sheet-backed, edited + published from the LiveOps Dashboard) without a redeploy. The actor
// reads the "default" definition to drive every league it runs; new definitions can be added later for variants.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using System.Collections.Generic;

namespace Game.Logic
{
    /// <summary> Identifier for a league definition (e.g. "default", "weekend-blitz"). </summary>
    [MetaSerializable]
    public class LeagueDefinitionId : StringId<LeagueDefinitionId> { }

    /// <summary>
    /// A scripted twist applied to one matchday of the season (the "events that take place during match days").
    /// Designer/LiveOps-tunable: a named effect attached to a 1-based matchday number. v1 effect is a goal bonus
    /// (added to BOTH sides of every fixture that matchday — a "goal rush"); the name is surfaced in the feed.
    /// </summary>
    [MetaSerializable]
    public class MatchdayEvent
    {
        /// <summary> 1-based matchday this event fires on (1..38 for a full 20-team season). </summary>
        [MetaMember(1)] public int    Matchday  { get; private set; }
        /// <summary> Display name shown in the matchday feed, e.g. "Mid-Season Goal Rush". </summary>
        [MetaMember(2)] public string Name      { get; private set; } = "";
        /// <summary> Extra goals added to both teams in every fixture this matchday (0 = no effect). </summary>
        [MetaMember(3)] public int    GoalBonus { get; private set; }

        public MatchdayEvent() { }
        public MatchdayEvent(int matchday, string name, int goalBonus = 0)
        {
            Matchday  = matchday;
            Name      = name;
            GoalBonus = goalBonus;
        }
    }

    /// <summary>
    /// A season-league template: the size, daily sim time, default formation and scripted matchday events.
    /// The <see cref="LeagueActor"/> reads the "default" definition to drive the leagues it runs.
    /// </summary>
    [MetaSerializable]
    public class LeagueDefinition : IGameConfigData<LeagueDefinitionId>
    {
        [MetaMember(1)] public LeagueDefinitionId   Id              { get; private set; }
        [MetaMember(2)] public string               DisplayName     { get; private set; } = "";
        /// <summary> Total teams in the league (humans + CPU fill). 20 → 38 matchdays (the "38-0"). </summary>
        [MetaMember(3)] public int                  LeagueSize      { get; private set; } = 20;
        /// <summary> Hour (UTC) at which one matchday is auto-simulated each day — the 7pm ritual. </summary>
        [MetaMember(4)] public int                  DailySimHourUtc { get; private set; } = 19;
        /// <summary> Formation id used when a manager hasn't chosen one, and for CPU sides (e.g. "4-3-3"). </summary>
        [MetaMember(5)] public string               DefaultFormation { get; private set; } = "4-3-3";
        /// <summary> Scripted events keyed to specific matchdays. </summary>
        [MetaMember(6)] public List<MatchdayEvent>  MatchdayEvents  { get; private set; } = new List<MatchdayEvent>();

        // --- Transfer windows (the metagame between match days) ---
        /// <summary> DEPRECATED — transfers now charge the player's wallet Coins (see TransferBaseCost below). Kept so old archives and the published sheet column keep deserializing. </summary>
        [MetaMember(7)]  public int TransferBudget              { get; private set; } = 300;
        /// <summary> DEPRECATED — replaced by the OVR-scaled cost (TransferBaseCost/TransferCostPerOvr). Kept for archive/sheet compat. </summary>
        [MetaMember(8)]  public int TransferSwapCost            { get; private set; } = 100;
        /// <summary> Hour (UTC) the daily transfer window opens. </summary>
        [MetaMember(9)]  public int TransferWindowStartHourUtc  { get; private set; } = 0;
        /// <summary>
        /// How many hours the daily transfer window stays open. Default 24 = ALWAYS open between matchdays —
        /// the swap market is the core metagame while the sims tick by. Set a shorter window (e.g. 17 + 2h)
        /// for a "deadline day" feel; admins can also force-open/close per league from the dashboard.
        /// </summary>
        [MetaMember(10)] public int TransferWindowDurationHours { get; private set; } = 24;

        // --- Wallet-coin transfer economy (signings charge the player's wallet, scaled by the incoming player's OVR) ---
        /// <summary> Minimum Coins a signing costs (also the cost at/below the pivot OVR). </summary>
        [MetaMember(11)] public int TransferBaseCost   { get; private set; } = 60;
        /// <summary> Coins added per OVR point above the pivot. </summary>
        [MetaMember(12)] public int TransferCostPerOvr { get; private set; } = 15;
        /// <summary> OVR at which a signing costs exactly the base cost. </summary>
        [MetaMember(13)] public int TransferOvrPivot   { get; private set; } = 70;

        // --- Marquee signings (pay Gems instead of Coins for a star) ---
        /// <summary> Minimum OVR a player must have to be signed with Gems. </summary>
        [MetaMember(14)] public int MarqueeMinOvr    { get; private set; } = 85;
        /// <summary> Gem cost of a marquee signing at exactly MarqueeMinOvr. </summary>
        [MetaMember(15)] public int MarqueeGemBase   { get; private set; } = 25;
        /// <summary> Gems added per OVR point above MarqueeMinOvr. </summary>
        [MetaMember(16)] public int MarqueeGemPerOvr { get; private set; } = 5;

        // --- Elite spin (pay Gems during the draft for a guaranteed top-tier club spin) ---
        /// <summary> Gem cost of one elite spin. </summary>
        [MetaMember(17)] public int EliteSpinGemCost  { get; private set; } = 20;
        /// <summary> A club-season counts as elite when its squad's average OVR is at least this. </summary>
        [MetaMember(18)] public int EliteSpinMinAvgOvr { get; private set; } = 81;

        public LeagueDefinitionId ConfigKey => Id;

        /// <summary> Wallet-Coin cost of signing a player with the given OVR. Integer math — runs in client-predicted actions. </summary>
        public long TransferCostFor(int ovr)
            => System.Math.Max(TransferBaseCost, TransferBaseCost + (long)(ovr - TransferOvrPivot) * TransferCostPerOvr);

        /// <summary> Gem cost of a marquee signing for the given OVR (only meaningful when ovr >= MarqueeMinOvr). </summary>
        public long MarqueeGemCostFor(int ovr)
            => MarqueeGemBase + (long)System.Math.Max(0, ovr - MarqueeMinOvr) * MarqueeGemPerOvr;

        /// <summary> True if the daily transfer window is open at <paramref name="utcHour"/> (schedule only; admin overrides live on the league). </summary>
        public bool IsTransferWindowHour(int utcHour)
        {
            // Designer-editable values: normalize the start hour and clamp the duration, and wrap across midnight
            // (e.g. start 23h + 2h ⇒ open 23:00–01:00) so a late window doesn't silently never open.
            int start    = ((TransferWindowStartHourUtc % 24) + 24) % 24;
            int duration = System.Math.Clamp(TransferWindowDurationHours, 0, 24);
            int offset   = ((utcHour - start) + 24) % 24;
            return offset < duration;
        }

        public LeagueDefinition() { }
        public LeagueDefinition(string id, string displayName, int leagueSize, int dailySimHourUtc, string defaultFormation, List<MatchdayEvent> matchdayEvents)
        {
            Id               = LeagueDefinitionId.FromString(id);
            DisplayName      = displayName;
            LeagueSize       = leagueSize;
            DailySimHourUtc  = dailySimHourUtc;
            DefaultFormation = defaultFormation;
            MatchdayEvents   = matchdayEvents ?? new List<MatchdayEvent>();
        }

        /// <summary> The event scripted for the given 1-based matchday, or null if none. </summary>
        public MatchdayEvent EventForMatchday(int matchdayNumber)
        {
            foreach (MatchdayEvent e in MatchdayEvents)
                if (e.Matchday == matchdayNumber)
                    return e;
            return null;
        }
    }

    /// <summary>
    /// Shared lookup for the league definition that drives economy costs (transfer fees, marquee/elite-spin Gems).
    /// Client-predicted actions and the server resolve costs through this so the numbers always agree.
    /// </summary>
    public static class LeagueEconomy
    {
        public static LeagueDefinition DefinitionFor(PlayerModel player)
            => player.GameConfig.LeagueDefinitions.TryGetValue(LeagueDefinitionId.FromString(LeagueDefinitionContent.DefaultId), out LeagueDefinition def)
                ? def
                : LeagueDefinitionContent.Default;
    }

    /// <summary> Code-defined default league definitions (the fallback / seed until edited from a sheet). </summary>
    public static class LeagueDefinitionContent
    {
        public const string DefaultId = "default";

        /// <summary> The canonical daily 38-0 league: 20 teams, 7pm UTC sim, 4-3-3, three scripted matchday events. </summary>
        public static readonly LeagueDefinition Default = new LeagueDefinition(
            id: DefaultId,
            displayName: "Daily 38-0 League",
            leagueSize: 20,
            dailySimHourUtc: 19,
            defaultFormation: "4-3-3",
            matchdayEvents: new List<MatchdayEvent>
            {
                new MatchdayEvent(1,  "Opening Day"),
                new MatchdayEvent(19, "Mid-Season Goal Rush", goalBonus: 1),
                new MatchdayEvent(38, "Title Decider"),
            });

        public static GameConfigLibrary<LeagueDefinitionId, LeagueDefinition> CreateLibrary()
            => GameConfigLibrary<LeagueDefinitionId, LeagueDefinition>.CreateSolo(new[] { Default });
    }
}
