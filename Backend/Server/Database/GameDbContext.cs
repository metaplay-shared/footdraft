// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Cloud.Persistence;

namespace Game.Server.Database
{
    /// <summary>
    /// Game-specific EFCore database context. Used to declare the database tables.
    /// </summary>
    public class GameDbContext : MetaDbContext
    {
        // PersistedLeagueRegistry (38-0-20 league persistence) is auto-registered via the SDK's IPersistedItem
        // scan, so no explicit DbSet is needed here.
    }
}
