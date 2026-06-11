// FOOTDRAFT — SDK LiveOps Events integration: the "Coin Rush" event.
//
// Defining a LiveOpsEventContent type lights up the LiveOps Dashboard's "LiveOps Events" page + Timeline, so
// an operator can schedule a Coin Rush (e.g. "2× Coins this weekend") with audience targeting and NO deploy.
// While a Coin Rush is in an active phase for a player, every Coins income from the match-reward pipeline and
// daily-quest claims is multiplied. The event state lives inside the checksummed PlayerModel (member 55 on the
// SDK base), so the boost is deterministic on client and server.

using System;
using Metaplay.Core;
using Metaplay.Core.LiveOpsEvent;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> Dashboard-schedulable "Coin Rush": multiplies Coins earned from matches and quest claims. </summary>
    [MetaSerializable]
    [LiveOpsEvent(1, "Coin Rush")]
    public class CoinRushEventContent : LiveOpsEventContent
    {
        /// <summary> Coins multiplier while the event is active (2 = double coins). </summary>
        [MetaMember(1)] public int Multiplier { get; private set; } = 2;

        public CoinRushEventContent() { }
        public CoinRushEventContent(int multiplier) { Multiplier = multiplier; }

        public override void Validate(ILiveOpsEventValidationLog log, Metaplay.Core.Config.FullGameConfig activeGameConfig)
        {
            if (Multiplier < 1 || Multiplier > 10)
                log.Error("Multiplier must be between 1 and 10", nameof(Multiplier));
        }
    }

    /// <summary> Per-player state of a Coin Rush (none needed beyond the SDK's phase tracking). </summary>
    [MetaSerializable]
    [MetaSerializableDerived(2000)]
    public class PlayerCoinRushEventModel : PlayerLiveOpsEventModel<CoinRushEventContent, PlayerModel>
    {
        public PlayerCoinRushEventModel() { }
        public PlayerCoinRushEventModel(PlayerLiveOpsEventInfo info) : base(info) { }
    }

    /// <summary> Deterministic queries over the player's active LiveOps events (usable inside actions). </summary>
    public static class LiveOpsBoosts
    {
        /// <summary> The strongest active Coin Rush multiplier for this player, or 1 when none is running. </summary>
        public static int CoinMultiplier(PlayerModel player)
        {
            int multiplier = 1;
            foreach ((MetaGuid _, PlayerLiveOpsEventModel eventModel) in player.LiveOpsEvents.EventModels)
            {
                if (eventModel is PlayerCoinRushEventModel rush && rush.Phase.IsActivePhase())
                    multiplier = Math.Max(multiplier, Math.Max(1, rush.Content.Multiplier));
            }
            return multiplier;
        }
    }
}
