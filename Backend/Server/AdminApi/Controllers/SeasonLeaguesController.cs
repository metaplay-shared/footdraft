// FOOTDRAFT — admin API for the LiveOps Dashboard "Season Leagues" page.
//
// Reads the singleton LeagueActor registry. (Named "SeasonLeagues" to avoid colliding with the SDK's built-in
// LeaguesController, which owns /api/leagues for the Divisions/Leagues feature.) Routes are auto-prefixed /api.

using System.Threading.Tasks;
using Game.Logic;
using Game.Server.AdminApi;
using Metaplay.Server.AdminApi;
using Metaplay.Server.AdminApi.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace Game.Server.AdminApi.Controllers
{
    public class SeasonLeaguesController : GameAdminApiController
    {
        /// <summary> GET /api/seasonLeagues — list every league in the registry. </summary>
        [HttpGet("seasonLeagues")]
        [RequirePermission(GamePermissions.ApiSeasonLeaguesView)]
        public async Task<ActionResult<LeagueRegistryListView>> GetLeagues()
        {
            LeagueListResponse resp = await EntityAskAsync<LeagueListResponse>(LeagueActor.LeagueEntityId, new LeagueListRequest());
            return resp.List;
        }

        /// <summary> GET /api/seasonLeagues/{code} — an admin (spectator) snapshot of one league. </summary>
        [HttpGet("seasonLeagues/{code}")]
        [RequirePermission(GamePermissions.ApiSeasonLeaguesView)]
        public async Task<ActionResult<LeagueSnapshot>> GetLeague(string code)
        {
            LeagueOpResponse resp = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueAdminSnapshotRequest(code));
            if (!string.IsNullOrEmpty(resp.Error))
                return NotFound(resp.Error);
            return resp.Snapshot;
        }

        /// <summary> Body for <see cref="SetTransferWindow"/>: 0 = follow schedule, 1 = force open, 2 = force closed. </summary>
        public class SetTransferWindowBody { public int Override { get; set; } }

        /// <summary> POST /api/seasonLeagues/{code}/advance — force-advance the next matchday now. </summary>
        [HttpPost("seasonLeagues/{code}/advance")]
        [RequirePermission(GamePermissions.ApiSeasonLeaguesAdmin)]
        public async Task<ActionResult<LeagueSnapshot>> AdvanceMatchday(string code)
        {
            LeagueOpResponse resp = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueAdminPlayMatchdayRequest(code));
            if (!string.IsNullOrEmpty(resp.Error))
                return Conflict(resp.Error);
            await WriteAuditLogEventAsync(new GameServerEventBuilder(new SeasonLeagueMatchdayAdvancedAuditEvent(code)));
            return resp.Snapshot;
        }

        /// <summary> POST /api/seasonLeagues/{code}/transferWindow — open/close the league's transfer window. </summary>
        [HttpPost("seasonLeagues/{code}/transferWindow")]
        [RequirePermission(GamePermissions.ApiSeasonLeaguesAdmin)]
        public async Task<ActionResult<LeagueSnapshot>> SetTransferWindow(string code, [FromBody] SetTransferWindowBody body)
        {
            int overrideValue = body?.Override ?? 0;
            if (overrideValue < 0 || overrideValue > 2)
                return BadRequest("override must be 0 (follow schedule), 1 (force open) or 2 (force closed)");
            LeagueOpResponse resp = await EntityAskAsync<LeagueOpResponse>(LeagueActor.LeagueEntityId, new LeagueSetTransferWindowRequest(code, overrideValue));
            if (!string.IsNullOrEmpty(resp.Error))
                return NotFound(resp.Error);
            await WriteAuditLogEventAsync(new GameServerEventBuilder(new SeasonLeagueTransferWindowAuditEvent(code, overrideValue)));
            return resp.Snapshot;
        }
    }
}
