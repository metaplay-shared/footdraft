// This file is part of Metaplay SDK which is released under the Metaplay SDK License.

using Metaplay.Core;

namespace Game.Logic
{
    /// <summary>
    /// Registry for game-specific <see cref="EntityKind"/> values. The game-specific range is [100, 300).
    /// </summary>
    [EntityKindRegistry(100, 300)]
    public static class EntityKindGame
    {
        /// <summary>
        /// The global matchmaking lobby. A singleton entity that all queuing players associate with.
        /// </summary>
        public static readonly EntityKind Lobby = EntityKind.FromValue(100);

        /// <summary>
        /// A single multiplayer match (one ~5 minute round). Created on demand by the lobby.
        /// </summary>
        public static readonly EntityKind Match = EntityKind.FromValue(101);

        /// <summary>
        /// The global Clubs registry: a singleton service that holds club membership and the weekly Club League
        /// standings. (Mirrors what Metaplay's first-party Guild + Leagues frameworks provide; this in-memory
        /// coordinator keeps the demo migration-free.)
        /// </summary>
        public static readonly EntityKind Clubs = EntityKind.FromValue(102);

        /// <summary>
        /// The live "form sync" registry: a singleton service holding per-player die-tier overrides that an
        /// operator sets at runtime (no redeploy) to mirror real WC2026 form. Read by matches at setup.
        /// </summary>
        public static readonly EntityKind Form = EntityKind.FromValue(103);

        /// <summary>
        /// The season-league registry: a singleton service holding private leagues (≤20 managers) by invite code —
        /// members, locked drafted-XI ratings, the double round-robin fixtures, results and standings. Matches are
        /// resolved async (ghost) via MatchSim against each opponent's locked XI. (In-memory coordinator: the
        /// migration-free stand-in for the persisted Guild + Leagues frameworks.)
        /// </summary>
        public static readonly EntityKind League = EntityKind.FromValue(104);

        /// <summary>
        /// The global World Cup leaderboard: a singleton, persisted service ranking managers by their best World
        /// Cup run (titles, then deepest round, then best-XI overall). Players report on finishing a run and fetch
        /// a top-N snapshot for the World Cup hub's leaderboard view.
        /// </summary>
        public static readonly EntityKind WorldCupLeaderboard = EntityKind.FromValue(105);
    }
}
