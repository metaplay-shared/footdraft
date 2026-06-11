// FOOTDRAFT — a player's drafted XI (replaces the older card-collection squad system).

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// A manager's drafted eleven: the chosen <see cref="Formation"/> and the <see cref="LegendId"/> picked into
    /// each slot. Built up over the spin-draft (one pick per slot), then resolved to a match squad. Locked when
    /// the squad is committed into a season league (so a league fixture always uses the same XI).
    ///
    /// Server-authoritative: every mutation flows through draft <c>PlayerAction</c>s (P1b), and the spin RNG is
    /// seeded server-side so the offered candidate pool can't be reroll-scummed.
    /// </summary>
    [MetaSerializable]
    public class DraftedSquad
    {
        /// <summary> Chosen formation; empty until the manager picks one. </summary>
        [MetaMember(1)] public FormationId Formation { get; set; }

        /// <summary> slot index → drafted legend id. A slot absent from the map is still open. </summary>
        [MetaMember(2)] public MetaDictionary<int, LegendId> Picks { get; set; } = new MetaDictionary<int, LegendId>();

        /// <summary> Spin rerolls consumed this draft (capped by GlobalConfig). </summary>
        [MetaMember(3)] public int RerollsUsed { get; set; }

        /// <summary> True once committed into a season league; further edits are rejected. </summary>
        [MetaMember(4)] public bool Locked { get; set; }

        /// <summary>
        /// The slot a pending spin offer is for, or -1 if no spin is pending. Written by the server (the spin RNG
        /// runs server-side, see cheat-proof randomization), claimed by the client picking a candidate from it.
        /// </summary>
        [MetaMember(5)] public int PendingOfferSlot { get; set; } = -1;

        /// <summary> The (Club, Era) bucket + candidate ids the server rolled for <see cref="PendingOfferSlot"/>. </summary>
        [MetaMember(6)] public SpinBucket PendingOffer { get; set; }

        public DraftedSquad() { }

        public bool HasFormation => Formation != null;

        public bool HasPendingOffer => PendingOffer != null && PendingOfferSlot >= 0;

        public void ClearOffer()
        {
            PendingOfferSlot = -1;
            PendingOffer     = null;
        }

        public bool IsSlotFilled(int slot) => Picks.ContainsKey(slot);

        /// <summary> True when every slot of the chosen formation is filled. </summary>
        public bool IsComplete(FormationInfo formation)
        {
            if (formation == null)
                return false;
            for (int slot = 0; slot < formation.Slots.Count; slot++)
                if (!Picks.ContainsKey(slot))
                    return false;
            return true;
        }
    }
}
