// FOOTDRAFT — season, season-pass, shop & ranked-division shared types.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// The monthly Season schedule: contiguous <see cref="GlobalConfig.SeasonWindowDays"/>-day windows, each with
    /// a stable id. A new season resets the Season Pass and the ranked ladder. (v1 stand-in for a dashboard-
    /// scheduled LiveOps season.)
    /// </summary>
    public static class SeasonSchedule
    {
        static long WindowMs(GlobalConfig global) => (long)global.SeasonWindowDays * 24L * 60L * 60L * 1000L;

        public static long CurrentSeasonId(MetaTime now, GlobalConfig global)
        {
            long windowMs = WindowMs(global);
            return windowMs <= 0 ? 0 : now.MillisecondsSinceEpoch / windowMs;
        }

        public static MetaTime SeasonEndsAt(long seasonId, GlobalConfig global)
            => MetaTime.FromMillisecondsSinceEpoch((seasonId + 1) * WindowMs(global));
    }

    /// <summary> A reward at one Season Pass tier (free or premium track). </summary>
    [MetaSerializable]
    public class PassReward
    {
        [MetaMember(1)] public int Coins  { get; private set; }
        [MetaMember(2)] public int Gems   { get; private set; }
        [MetaMember(3)] public int Shards { get; private set; }

        public PassReward() { }
        public PassReward(int coins, int gems, int shards)
        {
            Coins  = coins;
            Gems   = gems;
            Shards = shards;
        }
    }

    /// <summary> A purchasable gem pack in the shop. In a real build this maps to a Metaplay InAppProduct + store
    /// validation; here the purchase is simulated server-side (no real money). </summary>
    [MetaSerializable]
    public class ShopProduct
    {
        [MetaMember(1)] public string Id         { get; private set; }
        [MetaMember(2)] public int    Gems       { get; private set; }
        [MetaMember(3)] public string PriceLabel { get; private set; }

        public ShopProduct() { }
        public ShopProduct(string id, int gems, string priceLabel)
        {
            Id         = id;
            Gems       = gems;
            PriceLabel = priceLabel;
        }
    }

    /// <summary> A purchasable Coins pack: the hard→soft currency bridge (Gems buy transfer money). </summary>
    [MetaSerializable]
    public class CoinProduct
    {
        [MetaMember(1)] public string Id      { get; private set; }
        [MetaMember(2)] public int    Coins   { get; private set; }
        [MetaMember(3)] public int    GemCost { get; private set; }

        public CoinProduct() { }
        public CoinProduct(string id, int coins, int gemCost)
        {
            Id      = id;
            Coins   = coins;
            GemCost = gemCost;
        }
    }

    /// <summary> A ranked-ladder division: reached at <see cref="MinPoints"/> Season Rank Points. </summary>
    [MetaSerializable]
    public class RankDivision
    {
        [MetaMember(1)] public string Name      { get; private set; }
        [MetaMember(2)] public int    MinPoints { get; private set; }
        [MetaMember(3)] public string Emoji     { get; private set; }

        public RankDivision() { }
        public RankDivision(string name, int minPoints, string emoji)
        {
            Name      = name;
            MinPoints = minPoints;
            Emoji     = emoji;
        }
    }
}
