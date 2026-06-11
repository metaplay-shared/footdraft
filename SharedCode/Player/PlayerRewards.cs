// FOOTDRAFT — reward types attachable to in-game mail / broadcasts sent from the LiveOps Dashboard.
//
// The dashboard's "Send Mail" / Broadcasts forms list every concrete MetaPlayerRewardBase in the game, so an
// operator can attach e.g. "500 Coins" to a mail; the player claims it from the in-game inbox (PlayerConsumeMail
// → Consume below).

using Metaplay.Core.Model;
using Metaplay.Core.Player;
using Metaplay.Core.Rewards;

namespace Game.Logic
{
    /// <summary> A currency grant (Coins / Gems / Shards) attached to an in-game mail or broadcast. </summary>
    [MetaSerializableDerived(1)]
    public class RewardCurrency : MetaPlayerReward<PlayerModel>
    {
        [MetaMember(1)] public CurrencyType Currency { get; private set; }
        [MetaMember(2)] public long         Amount   { get; private set; }

        public RewardCurrency() { }
        public RewardCurrency(CurrencyType currency, long amount)
        {
            Currency = currency;
            Amount   = amount;
        }

        public override void Consume(PlayerModel player, IRewardSource source)
        {
            if (Amount > 0)
                player.Wallet.Earn(Currency, Amount);
        }
    }
}
