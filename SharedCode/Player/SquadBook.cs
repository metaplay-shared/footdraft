// FOOTDRAFT — owned squad cards and their upgrade levels.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// Helpers for the string key that identifies a single squad card: a specific slot of a specific team
    /// (e.g. <c>"ARG#0"</c> = Argentina's first-choice player). Keeping it a plain string keeps the
    /// <see cref="SquadBook"/> serialization simple and stable.
    /// </summary>
    public static class CardKeys
    {
        public static string For(string teamId, int slot) => $"{teamId}#{slot}";
    }

    /// <summary>
    /// A player's squad collection: per-card upgrade levels. Upgrading a card (spending Coins + Shards) raises
    /// its effective rating, which can bump its die up a tier (d6 → d8 → d10) — so this is where the
    /// "build your dice pool" meta lives. Cards default to upgrade level 0 (the team's configured base rating).
    /// </summary>
    [MetaSerializable]
    public class SquadBook
    {
        [MetaMember(1)] public MetaDictionary<string, int> CardUpgradeLevels { get; private set; } = new MetaDictionary<string, int>();

        public SquadBook() { }

        /// <summary> The upgrade level of the card with key <paramref name="cardKey"/> (0 if never upgraded). </summary>
        public int UpgradeLevelOf(string cardKey) => CardUpgradeLevels.TryGetValue(cardKey, out int level) ? level : 0;

        public void SetUpgradeLevel(string cardKey, int level) => CardUpgradeLevels[cardKey] = level;
    }
}
