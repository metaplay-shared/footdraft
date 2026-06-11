// FOOTDRAFT — team & squad config.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using System.Collections.Generic;

namespace Game.Logic
{
    /// <summary>
    /// Identifier for a national team (e.g. "ARG", "FRA").
    /// </summary>
    [MetaSerializable]
    public class TeamId : StringId<TeamId> { }

    /// <summary>
    /// One player in a team's squad. <see cref="Rating"/> is the designer/LiveOps-tunable knob: it maps to a
    /// die size via <see cref="TeamContent.DieSidesForRating"/> when a squad is built for a match. The
    /// real-world "form sync" LiveOps event edits this rating (or pushes an override) to buff/nerf a player live.
    /// </summary>
    [MetaSerializable]
    public class PlayerEntry
    {
        [MetaMember(1)] public string Name   { get; private set; }
        [MetaMember(2)] public int    Rating { get; private set; }

        public PlayerEntry() { }
        public PlayerEntry(string name, int rating)
        {
            Name = name;
            Rating = rating;
        }
    }

    /// <summary>
    /// A national team and its (best-five) squad. Referred to by <see cref="TeamId"/>.
    /// </summary>
    [MetaSerializable]
    public class TeamInfo : IGameConfigData<TeamId>
    {
        [MetaMember(1)] public TeamId            Id          { get; private set; }
        [MetaMember(2)] public string            DisplayName { get; private set; }
        [MetaMember(3)] public string            FlagEmoji   { get; private set; }
        [MetaMember(4)] public List<PlayerEntry> Squad       { get; private set; } = new List<PlayerEntry>();

        public TeamId ConfigKey => Id;

        public TeamInfo() { }
        public TeamInfo(string id, string displayName, string flagEmoji, List<PlayerEntry> squad)
        {
            Id = TeamId.FromString(id);
            DisplayName = displayName;
            FlagEmoji = flagEmoji;
            Squad = squad;
        }
    }

    /// <summary>
    /// Static content for the WC2026 teams. Real teams/players (hobby project). Ratings are illustrative and
    /// are the LiveOps "form sync" knob (see <see cref="DieSidesForRating"/>).
    /// </summary>
    public static class TeamContent
    {
        // Rating → die sides. Higher-rated players roll bigger dice. These thresholds are the simple tuning
        // lever the real-world form-sync event nudges; mirror them in GlobalConfig if/when they move to data.
        public const int RatingForD10 = 85;
        public const int RatingForD8  = 78;

        public static int DieSidesForRating(int rating)
        {
            if (rating >= RatingForD10) return 10;
            if (rating >= RatingForD8)  return 8;
            return 6;
        }

        static List<PlayerEntry> Squad(params (string name, int rating)[] players)
        {
            List<PlayerEntry> list = new List<PlayerEntry>(players.Length);
            foreach ((string name, int rating) in players)
                list.Add(new PlayerEntry(name, rating));
            return list;
        }

        /// <summary> All teams, in a stable order (used for default/round-robin assignment in v0 setup). </summary>
        public static readonly TeamInfo[] Teams =
        {
            new TeamInfo("ARG", "Argentina",  "🇦🇷", Squad(
                ("Messi", 91), ("Lautaro Martínez", 86), ("Julián Álvarez", 85), ("Mac Allister", 84), ("Enzo Fernández", 83))),
            new TeamInfo("FRA", "France",     "🇫🇷", Squad(
                ("Mbappé", 91), ("Griezmann", 85), ("Dembélé", 85), ("Tchouaméni", 84), ("Saliba", 84))),
            new TeamInfo("BRA", "Brazil",     "🇧🇷", Squad(
                ("Vinícius Jr", 89), ("Rodrygo", 85), ("Raphinha", 85), ("Bruno Guimarães", 84), ("Éder Militão", 83))),
            new TeamInfo("ENG", "England",    "🏴", Squad(
                ("Bellingham", 89), ("Kane", 89), ("Saka", 86), ("Foden", 86), ("Rice", 85))),
            new TeamInfo("ESP", "Spain",      "🇪🇸", Squad(
                ("Rodri", 90), ("Lamine Yamal", 86), ("Pedri", 85), ("Nico Williams", 83), ("Gavi", 82))),
            new TeamInfo("POR", "Portugal",   "🇵🇹", Squad(
                ("Bruno Fernandes", 87), ("Ronaldo", 86), ("Bernardo Silva", 86), ("Rafael Leão", 84), ("Vitinha", 83))),
            new TeamInfo("NED", "Netherlands","🇳🇱", Squad(
                ("Van Dijk", 87), ("Frenkie de Jong", 86), ("Gakpo", 83), ("Depay", 83), ("Xavi Simons", 82))),
            new TeamInfo("USA", "USA",        "🇺🇸", Squad(
                ("Pulisic", 85), ("McKennie", 78), ("Balogun", 78), ("Weah", 76), ("Musah", 76))),
        };

        public static GameConfigLibrary<TeamId, TeamInfo> CreateTeamsLibrary()
            => GameConfigLibrary<TeamId, TeamInfo>.CreateSolo(Teams);
    }
}
