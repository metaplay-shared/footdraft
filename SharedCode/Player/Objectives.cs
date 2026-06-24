// FOOTDRAFT — Objectives: a claimable career-milestone reward track spanning every mode (matches, World Cup,
// Draft Cup, league, scouting, packs). Each pays out once. Progress is computed from the live model (like
// Achievements) so there's no per-objective persisted progress — only the claimed set on PlayerObjectives.

using System;

namespace Game.Logic
{
    /// <summary> A claimable objective: a target + the reward granted once on claim. </summary>
    public sealed class ObjectiveDef
    {
        public string Id     { get; init; }
        public string Name   { get; init; }
        public string Desc   { get; init; }
        public string Icon   { get; init; }
        public int    Target { get; init; }
        public Func<PlayerModel, int> Current { get; init; }
        public int    Coins  { get; init; }
        public int    Gems   { get; init; }
        public int    Shards { get; init; }
    }

    public static class Objectives
    {
        public static readonly ObjectiveDef[] All =
        {
            new ObjectiveDef { Id = "win1",      Name = "First Blood",        Desc = "Win your first match",          Icon = "⚽", Target = 1,   Current = p => p.Progression.MatchesWon,        Coins = 150 },
            new ObjectiveDef { Id = "win10",     Name = "Finding Form",       Desc = "Win 10 matches",                Icon = "📈", Target = 10,  Current = p => p.Progression.MatchesWon,        Coins = 400, Gems = 5 },
            new ObjectiveDef { Id = "win50",     Name = "Serial Winner",      Desc = "Win 50 matches",                Icon = "🏅", Target = 50,  Current = p => p.Progression.MatchesWon,        Coins = 1200, Gems = 20 },
            new ObjectiveDef { Id = "lvl10",     Name = "Up the Ranks",       Desc = "Reach manager level 10",        Icon = "🎖️", Target = 10,  Current = p => p.PlayerLevel,                  Coins = 400 },
            new ObjectiveDef { Id = "lvl25",     Name = "Seasoned Boss",      Desc = "Reach manager level 25",        Icon = "🏛️", Target = 25,  Current = p => p.PlayerLevel,                  Coins = 1500, Gems = 30 },
            new ObjectiveDef { Id = "wc_r16",    Name = "Into the 16",        Desc = "Reach the WC Round of 16",      Icon = "🌍", Target = 2,   Current = p => p.Honours.WorldCupBestRound + 1, Coins = 300 },
            new ObjectiveDef { Id = "wc_qf",     Name = "Last Eight",         Desc = "Reach a WC Quarter-final",      Icon = "🌐", Target = 3,   Current = p => p.Honours.WorldCupBestRound + 1, Coins = 500, Gems = 8 },
            new ObjectiveDef { Id = "wc_win",    Name = "Lift the Cup",       Desc = "Win the World Cup",             Icon = "🏆", Target = 1,   Current = p => p.Honours.WorldCupTitles,       Coins = 1500, Gems = 60 },
            new ObjectiveDef { Id = "dc_win",    Name = "Draft Cup Glory",    Desc = "Win a Draft Cup",               Icon = "🥇", Target = 1,   Current = p => p.Honours.DraftCupTitles,       Coins = 600, Gems = 12 },
            new ObjectiveDef { Id = "league",    Name = "League Legend",      Desc = "Win a league title",            Icon = "👑", Target = 1,   Current = p => p.Honours.LeagueTitles,         Coins = 1000, Gems = 30 },
            new ObjectiveDef { Id = "scout25",   Name = "Talent Spotter",     Desc = "Scout 25 different players",    Icon = "🔭", Target = 25,  Current = p => p.Collection.UniqueScouted,     Coins = 250 },
            new ObjectiveDef { Id = "scout100",  Name = "Chief Scout",        Desc = "Scout 100 different players",   Icon = "📋", Target = 100, Current = p => p.Collection.UniqueScouted,     Coins = 700, Gems = 15 },
            new ObjectiveDef { Id = "packs5",    Name = "Unboxing",           Desc = "Open 5 Scout Packs",            Icon = "📦", Target = 5,   Current = p => p.Packs.OpenedCount,            Coins = 200 },
            new ObjectiveDef { Id = "packs25",   Name = "Pack Fiend",         Desc = "Open 25 Scout Packs",           Icon = "🎁", Target = 25,  Current = p => p.Packs.OpenedCount,            Coins = 500, Gems = 15 },
            new ObjectiveDef { Id = "galactico", Name = "Dream Team",         Desc = "Assemble an 88-rated XI",       Icon = "✨", Target = 88,  Current = p => p.Honours.BestDraftedXiOvr,     Coins = 600, Gems = 10 },
        };

        public static bool IsComplete(ObjectiveDef o, PlayerModel p) => o.Current(p) >= o.Target;
        public static bool IsClaimed(ObjectiveDef o, PlayerModel p) => p.Objectives.IsClaimed(o.Id);
        public static bool IsClaimable(ObjectiveDef o, PlayerModel p) => IsComplete(o, p) && !IsClaimed(o, p);

        public static int ClaimableCount(PlayerModel p)
        {
            int c = 0;
            foreach (ObjectiveDef o in All)
                if (IsClaimable(o, p)) c++;
            return c;
        }

        public static ObjectiveDef Find(string id)
        {
            foreach (ObjectiveDef o in All)
                if (o.Id == id) return o;
            return null;
        }
    }
}
