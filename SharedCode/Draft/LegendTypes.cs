// 38-0-20 — the player corpus the spin-the-wheel draft picks from, organised as per-(Club, Season) squads.
//
// ⚠ Internal demo only: real player names. Do not redistribute or ship publicly without a likeness licence.
// The draft SPINS a random (Club, Season) and offers that squad; a manager picks one player into an open slot.
// A real footballer can appear in many (club, season) squads — uniqueness in a league draft is by player NAME,
// so a given person can only be on one team.
//
// The squads themselves live in SeasonSquadsGenerated.cs, produced by tools/squad-import/import.py from the
// Premier League squad dataset (DATA_CSV). Re-run that importer to refresh the database.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using System.Collections.Generic;

namespace Game.Logic
{
    /// <summary> On-pitch role. Coarse for v1. </summary>
    [MetaSerializable]
    public enum Position
    {
        GK  = 0,
        DEF = 1,
        MID = 2,
        FWD = 3,
    }

    /// <summary> Decade bucket, derived from the season — used for grouping/labels. </summary>
    [MetaSerializable]
    public enum Era
    {
        E1980s = 0,
        E1990s = 1,
        E2000s = 2,
        E2010s = 3,
        E2020s = 4,
    }

    /// <summary> Identifier for a player-season entry, e.g. "peter_schmeichel__manchester_united__1998_99". </summary>
    [MetaSerializable]
    public class LegendId : StringId<LegendId> { }

    /// <summary>
    /// One draftable player-season. <see cref="Club"/> + <see cref="Season"/> form the spin bucket; <see cref="Name"/>
    /// is the real-player identity used for draft uniqueness; <see cref="Ovr"/> drives the match sim.
    /// </summary>
    [MetaSerializable]
    public class LegendPlayer : IGameConfigData<LegendId>
    {
        [MetaMember(1)] public LegendId     Id        { get; private set; }
        [MetaMember(2)] public string       Name      { get; private set; }
        [MetaMember(3)] public Position     Position  { get; private set; }
        [MetaMember(4)] public int          Ovr       { get; private set; }
        [MetaMember(5)] public string       Nation    { get; private set; }
        [MetaMember(6)] public string       Club      { get; private set; }
        [MetaMember(7)] public Era          Era       { get; private set; }
        [MetaMember(8)] public List<string> ChemLinks { get; private set; } = new List<string>();
        [MetaMember(9)] public string       Season    { get; private set; } = "";

        public LegendId ConfigKey => Id;

        public LegendPlayer() { }
        public LegendPlayer(string id, string name, Position position, int ovr, string nation, string club, Era era, string season)
        {
            Id       = LegendId.FromString(id);
            Name     = name;
            Position = position;
            Ovr      = ovr;
            Nation   = nation;
            Club     = club;
            Era      = era;
            Season   = season;
        }

        /// <summary> Club/season-agnostic key for the real person (draft uniqueness is by this). </summary>
        public string IdentityKey => Name;
    }

    public static partial class LegendContent
    {
        // Implemented by the importer-generated file (SeasonSquadsGenerated.cs); a no-op if that file is absent.
        static partial void AppendGeneratedSquads(List<LegendPlayer> sink);

        /// <summary> A player within a squad, before its club/season are attached. </summary>
        public readonly struct PSpec
        {
            public readonly string   Name;
            public readonly Position Position;
            public readonly int      Ovr;
            public readonly string   Nation;
            public PSpec(string name, Position position, int ovr, string nation)
            {
                Name = name; Position = position; Ovr = ovr; Nation = nation;
            }
        }

        static PSpec P(string name, Position position, int ovr, string nation) => new PSpec(name, position, ovr, nation);

        static Era EraForSeason(string season)
        {
            int year = 0;
            if (season != null && season.Length >= 4)
                int.TryParse(season.Substring(0, 4), out year);
            if (year < 1990) return Era.E1980s;
            if (year < 2000) return Era.E1990s;
            if (year < 2010) return Era.E2000s;
            if (year < 2020) return Era.E2010s;
            return Era.E2020s;
        }

        static string Slug(string s)
        {
            // ASCII-only: LegendId is a StringId, which rejects non-ASCII. Accented letters (é, ñ, ø…)
            // collapse to '_' — fine, since draft uniqueness is by Name, not by id.
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant())
            {
                bool asciiAlnum = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                sb.Append(asciiAlnum ? c : '_');
            }
            return sb.ToString();
        }

        // Called by the generated file once per club-season. Skips entries whose generated id collides (defensive).
        static readonly HashSet<string> _seenIds = new HashSet<string>();
        static void AddSquad(List<LegendPlayer> sink, string club, string season, PSpec[] players)
        {
            Era era = EraForSeason(season);
            foreach (PSpec p in players)
            {
                string id = $"{Slug(p.Name)}__{Slug(club)}__{Slug(season)}";
                if (!_seenIds.Add(id))
                    continue; // duplicate (name, club, season) — keep the first
                sink.Add(new LegendPlayer(id, p.Name, p.Position, p.Ovr, p.Nation, club, era, season));
            }
        }

        static LegendPlayer[] BuildAll()
        {
            List<LegendPlayer> s = new List<LegendPlayer>();
            AppendGeneratedSquads(s); // the full PL database (tools/squad-import)
            return s.ToArray();
        }

        static readonly LegendPlayer[] _legends = BuildAll();
        public static IReadOnlyList<LegendPlayer> Legends => _legends;

        public static GameConfigLibrary<LegendId, LegendPlayer> CreateLibrary()
            => GameConfigLibrary<LegendId, LegendPlayer>.CreateSolo(_legends);
    }
}
