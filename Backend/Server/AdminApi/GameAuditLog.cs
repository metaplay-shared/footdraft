// FOOTDRAFT — audit-log events for the dashboard's season-league write actions. These appear in the dashboard
// Audit Log so operator actions (advancing a matchday, opening/closing a transfer window) are traceable.
// (Grant-currency is auto-audited by the SDK via the [PlayerDashboardAction] path, so it needs no event here.)

using Metaplay.Core.Model;
using Metaplay.Server.AdminApi.Controllers;

namespace Game.Server.AdminApi
{
    public static class GameAuditLogEventCodes
    {
        // Game-specific range (SDK reserves up to ~9700; start well above it).
        public const int SeasonLeagueMatchdayAdvanced  = 10_500;
        public const int SeasonLeagueTransferWindowSet = 10_501;
    }

    [MetaSerializableDerived(GameAuditLogEventCodes.SeasonLeagueMatchdayAdvanced)]
    public class SeasonLeagueMatchdayAdvancedAuditEvent : GameServerEventPayloadBase
    {
        [MetaMember(1)] public string Code { get; private set; }

        SeasonLeagueMatchdayAdvancedAuditEvent() { }
        public SeasonLeagueMatchdayAdvancedAuditEvent(string code) { Code = code; }

        public override string SubsystemName    => "SeasonLeagues";
        public override string EventTitle        => "Season league matchday advanced";
        public override string EventDescription  => $"Forced the next matchday for season league {Code}.";
    }

    [MetaSerializableDerived(GameAuditLogEventCodes.SeasonLeagueTransferWindowSet)]
    public class SeasonLeagueTransferWindowAuditEvent : GameServerEventPayloadBase
    {
        [MetaMember(1)] public string Code     { get; private set; }
        [MetaMember(2)] public int    Override { get; private set; }

        SeasonLeagueTransferWindowAuditEvent() { }
        public SeasonLeagueTransferWindowAuditEvent(string code, int @override) { Code = code; Override = @override; }

        public override string SubsystemName    => "SeasonLeagues";
        public override string EventTitle        => "Season league transfer window set";
        public override string EventDescription  => $"Set transfer window for season league {Code} to {Describe(Override)}.";

        static string Describe(int o) => o == 1 ? "open" : o == 2 ? "closed" : "follow schedule";
    }
}
