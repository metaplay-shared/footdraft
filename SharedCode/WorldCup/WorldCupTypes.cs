// FOOTDRAFT — World Cup 2026 mode: national teams + their full 26-man squads, as game config.
//
// The squads are real (2026 FIFA World Cup, 48 nations × 26). Player ratings (Ovr) are FIFA-equivalent
// career values resolved by tools/wc-import/import.py — the SAME rating pipeline as the legend corpus, so a
// drafted World-Cup XI and a drafted legend XI live on the same scale and feed the same MatchSim.
//
// A World-Cup squad player is just a LegendPlayer (Name + Position + Ovr is all the draft/sim need), so the
// entire spin-draft + chemistry + line-rating + match-sim stack is reused unchanged. The only WC-specific
// glue is: the draft spins NATION buckets (not Club×Era), and the knockout opponents are real nations.

using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using System.Collections.Generic;
using System.Linq;

namespace Game.Logic
{
    /// <summary> Identifier for a World Cup nation (FIFA tri-code, e.g. "ARG", "FRA", "ENG"). </summary>
    [MetaSerializable]
    public class NationId : StringId<NationId> { }

    /// <summary>
    /// A World Cup 2026 nation and its full squad. The squad players are <see cref="LegendPlayer"/> records
    /// (Nation = this nation's display name, Club = real club, Era = E2020s, Season = "WC2026") so they drop
    /// straight into the existing draft + match sim. <see cref="Strength"/> drives knockout-bracket seeding.
    /// </summary>
    [MetaSerializable]
    public class NationInfo : IGameConfigData<NationId>
    {
        [MetaMember(1)] public NationId           Id          { get; private set; }
        [MetaMember(2)] public string             DisplayName { get; private set; }
        [MetaMember(3)] public string             FlagEmoji   { get; private set; }
        [MetaMember(4)] public string             Group       { get; private set; } // "A".."L"
        [MetaMember(5)] public List<LegendPlayer> Squad       { get; private set; } = new List<LegendPlayer>();

        public NationId ConfigKey => Id;

        public NationInfo() { }
        public NationInfo(string id, string displayName, string flagEmoji, string group, List<LegendPlayer> squad)
        {
            Id          = NationId.FromString(id);
            DisplayName = displayName;
            FlagEmoji   = flagEmoji;
            Group       = group;
            Squad       = squad ?? new List<LegendPlayer>();
        }

        /// <summary> Best-XI overall (avg of the best 11 by a 4-3-3 shape) — the bracket-seeding strength. </summary>
        public int Strength => WorldCup.BestXiOverall(this);

        public string Badge => $"{FlagEmoji} {DisplayName}";
    }

    /// <summary>
    /// Code-defined World Cup content: the 48 nations and their squads, populated by the importer-generated
    /// <c>WorldCupSquadsGenerated.cs</c> (a no-op if that file is absent — the mode just has no nations until
    /// the importer runs). Exposed as two code-only config libraries (nations + a flat player corpus).
    /// </summary>
    public static partial class WorldCupContent
    {
        // Implemented by the importer-generated file; appends every nation + its 26-man squad to the sink.
        static partial void AppendGeneratedNations(List<NationInfo> sink);

        /// <summary> A squad player before its nation/era/id are attached (the generated file emits these). </summary>
        public readonly struct WcSpec
        {
            public readonly string   Name;
            public readonly Position Position;
            public readonly int      Ovr;
            public readonly string   Club;
            public readonly int      Number;
            public WcSpec(string name, Position position, int ovr, string club, int number)
            {
                Name = name; Position = position; Ovr = ovr; Club = club; Number = number;
            }
        }

        static WcSpec W(string name, Position position, int ovr, string club, int number = 0)
            => new WcSpec(name, position, ovr, club, number);

        // ASCII-only slug for the LegendId (StringId rejects non-ASCII); WC ids are namespaced "wc__<code>__<name>".
        static string Slug(string s)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s.ToLowerInvariant())
            {
                bool asciiAlnum = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
                sb.Append(asciiAlnum ? c : '_');
            }
            return sb.ToString();
        }

        // Called by the generated file once per nation. Builds the squad as LegendPlayer records.
        static void AddNation(List<NationInfo> sink, string code, string name, string flag, string group, WcSpec[] players)
        {
            List<LegendPlayer> squad = new List<LegendPlayer>(players.Length);
            HashSet<string> seen = new HashSet<string>();
            foreach (WcSpec p in players)
            {
                string id = $"wc__{Slug(code)}__{Slug(p.Name)}";
                if (!seen.Add(id))
                    continue; // duplicate name within a squad — keep the first
                squad.Add(new LegendPlayer(id, p.Name, p.Position, p.Ovr, name, p.Club, Era.E2020s, "WC2026"));
            }
            sink.Add(new NationInfo(code, name, flag, group, squad));
        }

        static NationInfo[] BuildAll()
        {
            List<NationInfo> s = new List<NationInfo>();
            AppendGeneratedNations(s);
            return s.ToArray();
        }

        static readonly NationInfo[]   _nations = BuildAll();
        static readonly LegendPlayer[] _players = _nations.SelectMany(n => n.Squad).ToArray();
        static readonly Dictionary<string, LegendPlayer> _byId =
            _players.GroupBy(p => p.Id.Value).ToDictionary(g => g.Key, g => g.First());

        public static IReadOnlyList<NationInfo>   Nations => _nations;
        public static IReadOnlyList<LegendPlayer> Players => _players;

        /// <summary> Lookup a World-Cup squad player by id (used by the draft resolve/pick path in WC mode). </summary>
        public static LegendPlayer ById(string id) => _byId.TryGetValue(id, out LegendPlayer p) ? p : null;

        /// <summary> World-Cup players with an OVR in [minOvr, maxOvr], stable order — the Scout Pack star pool. </summary>
        public static IReadOnlyList<LegendPlayer> PlayersInBand(int minOvr, int maxOvr)
        {
            List<LegendPlayer> list = new List<LegendPlayer>();
            foreach (LegendPlayer p in _players)
                if (p.Ovr >= minOvr && p.Ovr <= maxOvr)
                    list.Add(p);
            return list;
        }

        /// <summary> Nations sorted strongest-first (best-XI overall) — the source for knockout seeding. </summary>
        public static IReadOnlyList<NationInfo> NationsByStrength =>
            _nationsByStrength ??= _nations.OrderByDescending(n => n.Strength).ThenBy(n => n.Id.Value).ToList();
        static List<NationInfo> _nationsByStrength;

        /// <summary>
        /// The spin buckets for a World-Cup draft: one per nation (instead of Club×Era), labelled with the flag
        /// + name. The slot-candidate filter (position + not-already-picked) is applied by the caller as usual.
        /// </summary>
        public static List<SpinBucket> BuildNationBuckets()
        {
            List<SpinBucket> buckets = new List<SpinBucket>(_nations.Length);
            foreach (NationInfo nation in _nations)
            {
                List<LegendId> ids = new List<LegendId>(nation.Squad.Count);
                foreach (LegendPlayer p in nation.Squad)
                    ids.Add(p.Id);
                buckets.Add(new SpinBucket(nation.Badge, Era.None, ids));
            }
            return buckets;
        }

        public static GameConfigLibrary<NationId, NationInfo> CreateNationsLibrary()
            => GameConfigLibrary<NationId, NationInfo>.CreateSolo(_nations);

        public static GameConfigLibrary<LegendId, LegendPlayer> CreatePlayersLibrary()
            => GameConfigLibrary<LegendId, LegendPlayer>.CreateSolo(_players);
    }
}
