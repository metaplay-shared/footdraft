// FOOTDRAFT — a player's owned & equipped cosmetics.

using Metaplay.Core;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary>
    /// The player's cosmetics: which items they own and which are equipped per slot. Cosmetics are vanity only
    /// (they never affect a roll). Defaults (the free items) are granted + equipped on account creation.
    /// </summary>
    [MetaSerializable]
    public class PlayerCosmetics
    {
        [MetaMember(1)] public OrderedSet<string> Owned             { get; private set; } = new OrderedSet<string>();
        [MetaMember(2)] public string             EquippedAvatar    { get; set; } = "";
        [MetaMember(3)] public string             EquippedDiceSkin  { get; set; } = "";

        public PlayerCosmetics() { }

        public bool Owns(string id) => Owned.Contains(id);
    }
}
