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
        /// <summary> Prestige items granted only by an achievement (e.g. winning the World Cup) — hidden from the
        /// shop + never pulled from packs, so owning one actually means something. </summary>
        [MetaMember(6)] public bool         Exclusive { get; private set; }

        public string ConfigKey => Id;

        public CosmeticItem() { }
        public CosmeticItem(string id, CosmeticKind kind, string name, string glyph, int gemCost, bool exclusive = false)
        {
            Id        = id;
            Kind      = kind;
            Name      = name;
            Glyph     = glyph;
            GemCost   = gemCost;
            Exclusive = exclusive;
        }
    }

    /// <summary> Static cosmetic catalog (code-only config). </summary>
    public static class CosmeticContent
    {
        public static readonly CosmeticItem[] Items =
        {
            // Avatars (free default + gem unlocks; also pulled from Scout Packs / granted by bundles)
            new CosmeticItem("avatar_default", CosmeticKind.Avatar, "Rookie",      "🧑‍💼", 0),
            new CosmeticItem("avatar_fox",     CosmeticKind.Avatar, "The Fox",     "🦊", 40),
            new CosmeticItem("avatar_lion",    CosmeticKind.Avatar, "Lion",        "🦁", 40),
            new CosmeticItem("avatar_wolf",    CosmeticKind.Avatar, "Lone Wolf",   "🐺", 40),
            new CosmeticItem("avatar_eagle",   CosmeticKind.Avatar, "The Eagle",   "🦅", 60),
            new CosmeticItem("avatar_shark",   CosmeticKind.Avatar, "The Shark",   "🦈", 60),
            new CosmeticItem("avatar_brain",   CosmeticKind.Avatar, "The Professor","🧠", 80),
            new CosmeticItem("avatar_crown",   CosmeticKind.Avatar, "Gaffer",      "🎩", 80),
            new CosmeticItem("avatar_dragon",  CosmeticKind.Avatar, "Dragon",      "🐉", 120),
            new CosmeticItem("avatar_goat",    CosmeticKind.Avatar, "G.O.A.T.",    "🐐", 150),
            new CosmeticItem("avatar_alien",   CosmeticKind.Avatar, "The Alien",   "👽", 200),
            // Prestige avatars — earned only (not sold, never pulled from packs).
            new CosmeticItem("avatar_wc_champion", CosmeticKind.Avatar, "World Champion", "🏆", 0, exclusive: true),
            new CosmeticItem("avatar_cup_king",    CosmeticKind.Avatar, "Cup King",       "🥇", 0, exclusive: true),
            new CosmeticItem("avatar_invincible",  CosmeticKind.Avatar, "The Invincible", "🛡️", 0, exclusive: true),
            // Dice skins (free default + gem unlocks; Glyph = CSS color)
            new CosmeticItem("dice_default",   CosmeticKind.DiceSkin, "Classic",   "#ffffff", 0),
            new CosmeticItem("dice_gold",      CosmeticKind.DiceSkin, "Gold",      "#ffd23f", 60),
            new CosmeticItem("dice_emerald",   CosmeticKind.DiceSkin, "Emerald",   "#2ecc71", 60),
            new CosmeticItem("dice_sapphire",  CosmeticKind.DiceSkin, "Sapphire",  "#3b82f6", 80),
            new CosmeticItem("dice_amethyst",  CosmeticKind.DiceSkin, "Amethyst",  "#a855f7", 100),
            new CosmeticItem("dice_ruby",      CosmeticKind.DiceSkin, "Ruby",      "#ff2e43", 120),
            new CosmeticItem("dice_onyx",      CosmeticKind.DiceSkin, "Onyx",      "#11151b", 150),
        };

        public static GameConfigLibrary<string, CosmeticItem> CreateLibrary()
            => GameConfigLibrary<string, CosmeticItem>.CreateSolo(Items);
    }
}
