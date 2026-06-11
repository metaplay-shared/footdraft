// FOOTDRAFT — cosmetic catalog shared types.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> The kind of cosmetic slot an item occupies. </summary>
    [MetaSerializable]
    public enum CosmeticKind
    {
        Avatar   = 0,
        DiceSkin = 1,
    }

    /// <summary>
    /// A purchasable cosmetic (manager avatar or dice skin). Pure vanity — bought with Gems, never affects a
    /// roll (the fairness story). <see cref="Glyph"/> is an emoji (avatar) or a CSS color (dice skin) the client
    /// renders.
    /// </summary>
    [MetaSerializable]
    public class CosmeticItem : IGameConfigData<string>
    {
        [MetaMember(1)] public string       Id      { get; private set; }
        [MetaMember(2)] public CosmeticKind Kind    { get; private set; }
        [MetaMember(3)] public string       Name    { get; private set; }
        [MetaMember(4)] public string       Glyph   { get; private set; }
        [MetaMember(5)] public int          GemCost { get; private set; }

        public string ConfigKey => Id;

        public CosmeticItem() { }
        public CosmeticItem(string id, CosmeticKind kind, string name, string glyph, int gemCost)
        {
            Id      = id;
            Kind    = kind;
            Name    = name;
            Glyph   = glyph;
            GemCost = gemCost;
        }
    }

    /// <summary> Static cosmetic catalog (code-only config). </summary>
    public static class CosmeticContent
    {
        public static readonly CosmeticItem[] Items =
        {
            // Avatars (free default + gem unlocks)
            new CosmeticItem("avatar_default", CosmeticKind.Avatar, "Rookie",     "🧑‍💼", 0),
            new CosmeticItem("avatar_fox",     CosmeticKind.Avatar, "The Fox",    "🦊", 40),
            new CosmeticItem("avatar_lion",    CosmeticKind.Avatar, "Lion",       "🦁", 40),
            new CosmeticItem("avatar_crown",   CosmeticKind.Avatar, "Gaffer",     "🎩", 80),
            new CosmeticItem("avatar_goat",    CosmeticKind.Avatar, "G.O.A.T.",   "🐐", 150),
            // Dice skins (free default + gem unlocks; Glyph = CSS color)
            new CosmeticItem("dice_default",   CosmeticKind.DiceSkin, "Classic",  "#ffffff", 0),
            new CosmeticItem("dice_gold",      CosmeticKind.DiceSkin, "Gold",     "#ffd23f", 60),
            new CosmeticItem("dice_emerald",   CosmeticKind.DiceSkin, "Emerald",  "#2ecc71", 60),
            new CosmeticItem("dice_ruby",      CosmeticKind.DiceSkin, "Ruby",     "#ff2e43", 120),
        };

        public static GameConfigLibrary<string, CosmeticItem> CreateLibrary()
            => GameConfigLibrary<string, CosmeticItem>.CreateSolo(Items);
    }
}
