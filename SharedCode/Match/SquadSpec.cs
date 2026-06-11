// FOOTDRAFT — resolved squad passed into a match at setup.

using Metaplay.Core.Model;
using System.Collections.Generic;

namespace Game.Logic
{
    /// <summary>
    /// A fully-resolved dice squad: the team identity plus the five dice (one per player) the manager brings to
    /// a match. The server resolves this from a player's selected team and card upgrades (see
    /// <see cref="SquadBuilder"/>) so the dice baked into the match reflect the player's progression. This is the
    /// authoritative squad snapshot the <c>MatchActor</c> writes into the match model.
    /// </summary>
    [MetaSerializable]
    public class SquadSpec
    {
        [MetaMember(1)] public string            TeamId    { get; private set; }
        [MetaMember(2)] public string            Name      { get; private set; }
        [MetaMember(3)] public string            FlagEmoji { get; private set; }
        [MetaMember(4)] public List<SquadPlayer> Players   { get; private set; } = new List<SquadPlayer>();

        public SquadSpec() { }
        public SquadSpec(string teamId, string name, string flagEmoji, List<SquadPlayer> players)
        {
            TeamId    = teamId;
            Name      = name;
            FlagEmoji = flagEmoji;
            Players   = players;
        }
    }

    /// <summary>
    /// Resolves a <see cref="SquadSpec"/> for a team. The effective rating of each card is its configured base
    /// rating plus an upgrade bonus (<see cref="GlobalConfig.RatingPerUpgradeLevel"/> per upgrade level), which
    /// maps to a die size via <see cref="TeamContent.DieSidesForRating"/>. Shared so the server (real squads) and
    /// tests use exactly the same resolution.
    /// </summary>
    public static class SquadBuilder
    {
        /// <summary> Builds a squad applying a player's card upgrades from <paramref name="book"/>. </summary>
        public static SquadSpec Build(TeamInfo team, SquadBook book, GlobalConfig global)
        {
            List<SquadPlayer> players = new List<SquadPlayer>(team.Squad.Count);
            for (int slot = 0; slot < team.Squad.Count; slot++)
            {
                PlayerEntry entry          = team.Squad[slot];
                int         upgradeLevel   = book != null ? book.UpgradeLevelOf(CardKeys.For(team.Id.Value, slot)) : 0;
                int         effectiveRating = entry.Rating + upgradeLevel * global.RatingPerUpgradeLevel;
                players.Add(new SquadPlayer(entry.Name, TeamContent.DieSidesForRating(effectiveRating)));
            }
            return new SquadSpec(team.Id.Value, team.DisplayName, team.FlagEmoji, players);
        }

        /// <summary> Builds a team's base squad (no upgrades) — used for bot opponents. </summary>
        public static SquadSpec BuildBase(TeamInfo team)
        {
            List<SquadPlayer> players = new List<SquadPlayer>(team.Squad.Count);
            foreach (PlayerEntry entry in team.Squad)
                players.Add(new SquadPlayer(entry.Name, TeamContent.DieSidesForRating(entry.Rating)));
            return new SquadSpec(team.Id.Value, team.DisplayName, team.FlagEmoji, players);
        }

        /// <summary> Total dice "strength" of a squad (sum of die sides) — a simple matchmaking strength metric. </summary>
        public static int TotalSides(SquadSpec squad)
        {
            int total = 0;
            foreach (SquadPlayer player in squad.Players)
                total += player.Sides;
            return total;
        }

        /// <summary> Total base dice strength of a configured team (sum of die sides at base ratings). </summary>
        public static int TeamBaseTotalSides(TeamInfo team)
        {
            int total = 0;
            foreach (PlayerEntry entry in team.Squad)
                total += TeamContent.DieSidesForRating(entry.Rating);
            return total;
        }

        /// <summary>
        /// Picks the configured team whose base squad strength best matches a target derived from the human's
        /// squad strength and manager level (see <see cref="GlobalConfig.BotDifficultyPct"/>): weaker bots early,
        /// ramping to an even match. Gives a fair, level-scaled bot opponent without bespoke bot dice.
        /// </summary>
        public static TeamInfo PickScaledBotTeam(IReadOnlyList<TeamInfo> teams, int humanTotalSides, int humanLevel, GlobalConfig global)
        {
            int target = humanTotalSides * global.BotDifficultyPct(humanLevel) / 100;
            TeamInfo best = teams[0];
            int bestDelta = int.MaxValue;
            foreach (TeamInfo team in teams)
            {
                int delta = System.Math.Abs(TeamBaseTotalSides(team) - target);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = team;
                }
            }
            return best;
        }
    }
}
