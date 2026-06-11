// FOOTDRAFT — game-specific LiveOps Dashboard permissions (the "Season Leagues" page + its admin actions).

using Metaplay.Core;
using Metaplay.Server.AdminApi;

namespace Game.Server.AdminApi
{
    [AdminApiPermissionGroup("FOOTDRAFT game-specific permissions")]
    public static class GamePermissions
    {
        [MetaDescription("View season leagues, their members and standings in the dashboard.")]
        [Permission(DefaultRole.GameAdmin, DefaultRole.GameViewer, DefaultRole.CustomerSupportSenior, DefaultRole.CustomerSupportAgent)]
        public const string ApiSeasonLeaguesView = "api.season_leagues.view";

        [MetaDescription("Run admin actions on season leagues (advance a matchday, open/close a transfer window).")]
        [Permission(DefaultRole.GameAdmin)]
        public const string ApiSeasonLeaguesAdmin = "api.season_leagues.admin";
    }
}
