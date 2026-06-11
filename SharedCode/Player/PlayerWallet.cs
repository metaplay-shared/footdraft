// FOOTDRAFT — player currency wallet.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// The game's currencies. <see cref="Coins"/> is the soft currency (earned from play, spent on squad-card
    /// upgrades and entry fees); <see cref="Gems"/> is the hard/premium currency (bought via IAP, spent on the
    /// season pass, cosmetics, and convenience); <see cref="Shards"/> is card-upgrade material earned from play.
    /// </summary>
    [MetaSerializable]
    public enum CurrencyType
    {
        Coins  = 0,
        Gems   = 1,
        Shards = 2,
    }

    /// <summary>
    /// A player's currency balances. Mutated only from within player actions (transactionally) so the balances
    /// stay server-authoritative and checksum-consistent across client and server.
    /// </summary>
    [MetaSerializable]
    public class PlayerWallet
    {
        [MetaMember(1)] public MetaDictionary<CurrencyType, long> Balances { get; private set; } = new MetaDictionary<CurrencyType, long>();

        public PlayerWallet() { }

        /// <summary> Current balance of <paramref name="currency"/> (0 if none held). </summary>
        public long Get(CurrencyType currency) => Balances.TryGetValue(currency, out long value) ? value : 0;

        /// <summary> Adds <paramref name="amount"/> of <paramref name="currency"/>. No-op for non-positive amounts. </summary>
        public void Earn(CurrencyType currency, long amount)
        {
            if (amount <= 0)
                return;
            Balances[currency] = Get(currency) + amount;
        }

        /// <summary> True if the player holds at least <paramref name="amount"/> of <paramref name="currency"/>. </summary>
        public bool CanAfford(CurrencyType currency, long amount) => Get(currency) >= amount;

        /// <summary> Deducts <paramref name="amount"/> if affordable. Returns true on success, false (no change) otherwise. </summary>
        public bool TrySpend(CurrencyType currency, long amount)
        {
            if (amount < 0 || Get(currency) < amount)
                return false;
            Balances[currency] = Get(currency) - amount;
            return true;
        }
    }
}
