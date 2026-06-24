// FOOTDRAFT — Scout Pack definitions + the pure, deterministic roll. A pack is opened BEFORE a draft and
// grants draft-edge consumables (rerolls + elite spins) plus some coins — the "open a pack, then go play" loop
// adapted to a draft core (no permanent players; the edge applies to the draft you're about to do). The roll is
// deterministic from the open counter so the client predicts the exact pull the server applies.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> A Scout Pack tier. Coins roll in a range; rerolls + elite spins are the fixed draft-edge payout. </summary>
    [MetaSerializable]
    public class PackDef
    {
        [MetaMember(1)] public string Id         { get; private set; }
        [MetaMember(2)] public string Name       { get; private set; }
        [MetaMember(3)] public string Icon       { get; private set; }
        [MetaMember(4)] public int    GemCost    { get; private set; } // 0 = free daily pack
        [MetaMember(5)] public int    CoinsMin   { get; private set; }
        [MetaMember(6)] public int    CoinsMax   { get; private set; }
        /// <summary> Extra draft rerolls this pack grants. </summary>
        [MetaMember(7)] public int    Rerolls    { get; private set; }
        /// <summary> Guaranteed top-tier (elite) spins this pack grants. </summary>
        [MetaMember(8)] public int    EliteSpins { get; private set; }

        public bool IsFreeDaily => GemCost <= 0;

        public PackDef() { }
        public PackDef(string id, string name, string icon, int gemCost, int coinsMin, int coinsMax, int rerolls, int eliteSpins)
        {
            Id = id; Name = name; Icon = icon; GemCost = gemCost;
            CoinsMin = coinsMin; CoinsMax = coinsMax; Rerolls = rerolls; EliteSpins = eliteSpins;
        }
    }

    /// <summary> Pure, deterministic pack rolling. The caller spends currency + applies the reward to the model. </summary>
    public static class PackEngine
    {
        /// <summary> Stable per-(open, pack) seed — independent of runtime hashing, so client + server agree. </summary>
        public static ulong SeedFor(int openedCount, string packId)
            => (ulong)(openedCount + 1) * 0x9E3779B97F4A7C15ul ^ (Fnv1a(packId) * 0xC2B2AE3D27D4EB4Ful);

        static ulong Fnv1a(string s)
        {
            ulong h = 14695981039346656037ul;
            if (s != null)
                foreach (char c in s) { h ^= c; h *= 1099511628211ul; }
            return h;
        }

        /// <summary> Rolls a pack's contents: coins in the tier's range + its fixed reroll / elite-spin payout. </summary>
        public static PackReward Roll(PackDef def, ulong seed)
        {
            RandomPCG rng = RandomPCG.CreateFromSeed(seed);
            return new PackReward
            {
                Coins      = RoundTo(Range(rng, def.CoinsMin, def.CoinsMax), 10),
                Rerolls    = def.Rerolls,
                EliteSpins = def.EliteSpins,
            };
        }

        static int Range(RandomPCG rng, int min, int max) => max <= min ? min : min + rng.NextInt(max - min + 1);
        static int RoundTo(int v, int step) => step <= 1 ? v : (v / step) * step;
    }
}
