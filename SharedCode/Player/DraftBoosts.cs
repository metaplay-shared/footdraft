// FOOTDRAFT — draft-edge consumables earned from Scout Packs and spent on the NEXT draft (Draft Cup / World
// Cup spin-draft). This is the FUT "open a pack before you play" loop adapted to a draft core: packs never give
// you permanent players (you draft fresh every time) — they give you an EDGE on the draft you're about to do.

using Metaplay.Core.Model;

namespace Game.Logic
{
    [MetaSerializable]
    public class DraftBoosts
    {
        /// <summary> Extra spin rerolls beyond the free cap, spent one-per-reroll while drafting. </summary>
        [MetaMember(1)] public int Rerolls    { get; set; }
        /// <summary> Guaranteed top-tier spins remaining — the server rolls an elite (Club×Era / nation) bucket and decrements. </summary>
        [MetaMember(2)] public int EliteSpins { get; set; }

        public DraftBoosts() { }

        public bool Any => Rerolls > 0 || EliteSpins > 0;
    }
}
