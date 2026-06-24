// FOOTDRAFT — Featured Offers: value bundles (sim-IAP, like the gem packs). A bundle grants a fixed mix of
// currency + a guaranteed cosmetic for a display price; one-time bundles (starter/event) can only be bought
// once. Real builds route through Metaplay IAP validation; here the contents are granted server-side (demo).

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> A purchasable value bundle shown in the Featured Offers row. </summary>
    [MetaSerializable]
    public class BundleDef
    {
        [MetaMember(1)] public string Id         { get; private set; }
        [MetaMember(2)] public string Name       { get; private set; }
        [MetaMember(3)] public string Icon       { get; private set; }
        /// <summary> Display-only price label (no real money in the demo). </summary>
        [MetaMember(4)] public string PriceLabel { get; private set; }
        [MetaMember(5)] public int    Coins      { get; private set; }
        [MetaMember(6)] public int    Gems       { get; private set; }
        [MetaMember(7)] public int    Shards     { get; private set; }
        /// <summary> A guaranteed cosmetic granted with the bundle, or "" for none. </summary>
        [MetaMember(8)] public string CosmeticId { get; private set; }
        /// <summary> One-time bundles (starter/event) can be bought only once. </summary>
        [MetaMember(9)] public bool   OneTime    { get; private set; }

        public BundleDef() { }
        public BundleDef(string id, string name, string icon, string priceLabel, int coins, int gems, int shards, string cosmeticId, bool oneTime)
        {
            Id = id; Name = name; Icon = icon; PriceLabel = priceLabel;
            Coins = coins; Gems = gems; Shards = shards; CosmeticId = cosmeticId; OneTime = oneTime;
        }
    }

    /// <summary> The manager's store state — which one-time bundles they've already bought. </summary>
    [MetaSerializable]
    public class PlayerStore
    {
        [MetaMember(1)] public OrderedSet<string> BundlesPurchased { get; private set; } = new OrderedSet<string>();

        public PlayerStore() { }
        public bool Owns(string id) => BundlesPurchased.Contains(id);
    }
}
