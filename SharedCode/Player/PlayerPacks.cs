// FOOTDRAFT — Scout Packs: the FUT-style pack-opening monetisation, adapted to a draft core. A pack grants a
// bundle of currency + (chance of) a cosmetic + (chance of) a "scouted" star added to the manager's club —
// never anything that changes a draft or a match, so the draft core stays fair. Opens are client-predicted +
// deterministic (seeded by the open counter), so the reveal is instant and the server recomputes the same pull.

using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> The contents pulled from one pack open — shown in the reveal, applied to coins + draft boosts. </summary>
    [MetaSerializable]
    public class PackReward
    {
        [MetaMember(1)] public int    Coins        { get; set; }
        [MetaMember(2)] public int    Gems         { get; set; } // retired (kept so old persisted pulls deserialize)
        [MetaMember(3)] public int    Shards       { get; set; } // retired
        [MetaMember(4)] public string CosmeticId   { get; set; } = ""; // retired
        [MetaMember(5)] public string StarPlayerId { get; set; } = ""; // retired
        /// <summary> Extra draft rerolls granted. </summary>
        [MetaMember(6)] public int    Rerolls      { get; set; }
        /// <summary> Guaranteed elite spins granted. </summary>
        [MetaMember(7)] public int    EliteSpins   { get; set; }

        public PackReward() { }
    }

    /// <summary> The manager's pack state: how many opened, the daily-free claim day, and the last pull (for the reveal). </summary>
    [MetaSerializable]
    public class PlayerPacks
    {
        /// <summary> Total packs opened (also the deterministic roll counter). </summary>
        [MetaMember(1)] public int        OpenedCount    { get; set; }
        /// <summary> UTC day index the free daily pack was last claimed (-1 = never). </summary>
        [MetaMember(2)] public long       LastFreePackDay { get; set; } = -1;
        /// <summary> The most-recent pull, surfaced to the reveal overlay. </summary>
        [MetaMember(3)] public PackReward LastReward     { get; set; }
        /// <summary> The id of the pack that produced <see cref="LastReward"/>. </summary>
        [MetaMember(4)] public string     LastPackId     { get; set; } = "";

        public PlayerPacks() { }
    }
}
