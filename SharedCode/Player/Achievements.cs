// FOOTDRAFT — achievements: milestone badges computed from the manager's lifetime stats + honours. Pure
// (no extra persisted state) so the profile screen and any future reward hooks share one definition.

using System;

namespace Game.Logic
{
    /// <summary> A single achievement: a target the manager works toward, evaluated against the live model. </summary>
    public sealed class AchievementDef
    {
        public string Id      { get; init; }
        public string Name    { get; init; }
        public string Desc    { get; init; }
        public string Icon    { get; init; }
        public int    Target  { get; init; }
        /// <summary> Current progress toward <see cref="Target"/> for a given model. </summary>
        public Func<PlayerModel, int> Current { get; init; }

        public bool IsEarned(PlayerModel p) => Current(p) >= Target;
    }

    public static class Achievements
    {
        public static readonly AchievementDef[] All =
        {
            new AchievementDef { Id = "kickoff",    Name = "Kick-off",          Desc = "Play your first match",        Icon = "⚽", Target = 1,   Current = p => p.Progression.MatchesPlayed },
            new AchievementDef { Id = "winner",     Name = "Winning Habit",     Desc = "Win 25 matches",               Icon = "✅", Target = 25,  Current = p => p.Progression.MatchesWon },
            new AchievementDef { Id = "streak",     Name = "On Fire",           Desc = "Reach a 5-win streak",         Icon = "🔥", Target = 5,   Current = p => p.Progression.BestWinStreak },
            new AchievementDef { Id = "veteran",    Name = "Veteran Gaffer",    Desc = "Reach manager level 10",       Icon = "🎖️", Target = 10,  Current = p => p.PlayerLevel },
            new AchievementDef { Id = "wc_final",   Name = "On the World Stage",Desc = "Reach the World Cup final",    Icon = "🌍", Target = 1,   Current = p => p.Honours.WorldCupBestRound >= WorldCup.RoundsTotal(p.GameConfig.Global) - 1 ? 1 : 0 },
            new AchievementDef { Id = "wc_win",     Name = "World Champions",   Desc = "Win the World Cup",            Icon = "🌐", Target = 1,   Current = p => p.Honours.WorldCupTitles },
            new AchievementDef { Id = "cup_win",    Name = "Cup Specialist",    Desc = "Win a Draft Cup",              Icon = "🏆", Target = 1,   Current = p => p.Honours.DraftCupTitles },
            new AchievementDef { Id = "bracket_win",Name = "Bracket Buster",    Desc = "Win the weekly Bracket Cup",   Icon = "🥇", Target = 1,   Current = p => p.Honours.BracketTitles },
            new AchievementDef { Id = "league_win", Name = "League Champions",  Desc = "Win a league title",           Icon = "👑", Target = 1,   Current = p => p.Honours.LeagueTitles },
            new AchievementDef { Id = "invincible", Name = "The Invincibles",   Desc = "Finish a season unbeaten",     Icon = "🛡️", Target = 1,   Current = p => p.Honours.InvincibleSeasons },
            new AchievementDef { Id = "galactico",  Name = "Galácticos",        Desc = "Assemble an 88-rated XI",      Icon = "✨", Target = 88,  Current = p => p.Honours.BestDraftedXiOvr },
            new AchievementDef { Id = "scout",      Name = "Super Scout",       Desc = "Scout 100 different players",  Icon = "🔭", Target = 100, Current = p => p.Collection.UniqueScouted },
            new AchievementDef { Id = "gold",       Name = "Top Flight",        Desc = "Reach the Gold division",      Icon = "🏅", Target = 2,   Current = p => p.Honours.BestRankDivision },
            new AchievementDef { Id = "hunter",     Name = "Trophy Hunter",     Desc = "Win 5 trophies in total",      Icon = "🏆", Target = 5,   Current = p => p.Honours.TotalTrophies },
        };

        public static int EarnedCount(PlayerModel p)
        {
            int c = 0;
            foreach (AchievementDef a in All)
                if (a.IsEarned(p)) c++;
            return c;
        }
    }
}
