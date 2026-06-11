// FOOTDRAFT — pure, deterministic draft logic: spin buckets, slot candidates, chemistry & line ratings.
// No state, no I/O — so the server, the client preview and the unit tests all share one implementation.

using Metaplay.Core;
using Metaplay.Core.Model;
using System;
using System.Collections.Generic;

namespace Game.Logic
{
    /// <summary>
    /// One spin outcome: a (Club, Era) pair and the legend ids available from it. The spin picks a bucket at
    /// random (server-authoritative RNG); the manager then drafts one position-eligible candidate from it.
    /// </summary>
    [MetaSerializable]
    public class SpinBucket
    {
        [MetaMember(1)] public string         Club        { get; private set; }
        [MetaMember(2)] public Era            Era         { get; private set; }
        [MetaMember(3)] public List<LegendId> CandidateIds { get; private set; } = new List<LegendId>();

        public SpinBucket() { }
        public SpinBucket(string club, Era era, List<LegendId> candidateIds)
        {
            Club         = club;
            Era          = era;
            CandidateIds = candidateIds;
        }

        public string Label => $"{Club} · {EraLabel(Era)}";

        public static string EraLabel(Era era) => era switch
        {
            Era.E1980s => "1980s",
            Era.E1990s => "1990s",
            Era.E2000s => "2000s",
            Era.E2010s => "2010s",
            _          => era.ToString(),
        };
    }

    /// <summary> A drafted XI's four line ratings plus its chemistry score — the input to the match sim (P2). </summary>
    [MetaSerializable]
    public class LineRatings
    {
        [MetaMember(1)] public int Attack      { get; set; } // avg OVR of forwards
        [MetaMember(2)] public int Midfield    { get; set; } // avg OVR of midfielders
        [MetaMember(3)] public int Defence     { get; set; } // avg OVR of defenders
        [MetaMember(4)] public int Goalkeeping { get; set; } // GK OVR
        [MetaMember(5)] public int Chemistry   { get; set; } // bonus from links + formation balance
    }

    public static class DraftEngine
    {
        /// <summary> Spin rerolls allowed per draft (config-driven later; the original 38-0 grants one). </summary>
        public const int DefaultRerollCap = 1;

        // Chemistry tuning (config-driven later; constants for v1).
        public const int SameClubPairPoints = 2; // per pair of picked players sharing a club
        public const int LegendLinkPoints   = 4; // per explicit ChemLink pair both present
        public const int NoGoalkeeperPenalty = 10;
        public const int ThinDefencePenalty  = 6; // applied if fewer than 3 defenders fielded

        /// <summary> Distinct (Club, Era) spin buckets present in the corpus, each with its candidate ids. </summary>
        public static List<SpinBucket> BuildBuckets(IReadOnlyList<LegendPlayer> corpus)
        {
            // Preserve a stable order: first appearance of each (club, era) key.
            List<string>                       order   = new List<string>();
            Dictionary<string, SpinBucket>     byKey   = new Dictionary<string, SpinBucket>();
            foreach (LegendPlayer player in corpus)
            {
                string key = $"{player.Club}|{(int)player.Era}";
                if (!byKey.TryGetValue(key, out SpinBucket bucket))
                {
                    bucket = new SpinBucket(player.Club, player.Era, new List<LegendId>());
                    byKey[key] = bucket;
                    order.Add(key);
                }
                bucket.CandidateIds.Add(player.Id);
            }

            List<SpinBucket> buckets = new List<SpinBucket>(order.Count);
            foreach (string key in order)
                buckets.Add(byKey[key]);
            return buckets;
        }

        /// <summary> Picks a random bucket. RNG is supplied by the caller (server seeds it deterministically). </summary>
        public static SpinBucket Spin(IReadOnlyList<SpinBucket> buckets, RandomPCG rng)
        {
            if (buckets == null || buckets.Count == 0)
                throw new ArgumentException("No spin buckets available", nameof(buckets));
            return buckets[rng.NextInt(buckets.Count)];
        }

        /// <summary>
        /// Legends in <paramref name="bucket"/> that can fill a <paramref name="slotPosition"/> slot and are not
        /// already drafted into the squad.
        /// </summary>
        public static List<LegendPlayer> CandidatesForSlot(SpinBucket bucket, Position slotPosition, Func<LegendId, LegendPlayer> lookup, ICollection<string> alreadyPickedIds)
        {
            List<LegendPlayer> candidates = new List<LegendPlayer>();
            foreach (LegendId id in bucket.CandidateIds)
            {
                if (alreadyPickedIds != null && alreadyPickedIds.Contains(id.Value))
                    continue;
                LegendPlayer player = lookup(id);
                if (player != null && player.Position == slotPosition)
                    candidates.Add(player);
            }
            return candidates;
        }

        /// <summary> Computes the four line ratings + chemistry for a (possibly partial) drafted XI. </summary>
        public static LineRatings ComputeLines(DraftedSquad squad, FormationInfo formation, Func<LegendId, LegendPlayer> lookup)
        {
            long sumFwd = 0, sumMid = 0, sumDef = 0, sumGk = 0;
            int  nFwd = 0,   nMid = 0,   nDef = 0,   nGk = 0;

            List<LegendPlayer> picked = new List<LegendPlayer>();
            foreach ((int _, LegendId id) in squad.Picks)
            {
                LegendPlayer player = lookup(id);
                if (player == null)
                    continue;
                picked.Add(player);
                switch (player.Position)
                {
                    case Position.FWD: sumFwd += player.Ovr; nFwd++; break;
                    case Position.MID: sumMid += player.Ovr; nMid++; break;
                    case Position.DEF: sumDef += player.Ovr; nDef++; break;
                    case Position.GK:  sumGk  += player.Ovr; nGk++;  break;
                }
            }

            LineRatings r = new LineRatings
            {
                Attack      = nFwd > 0 ? (int)(sumFwd / nFwd) : 0,
                Midfield    = nMid > 0 ? (int)(sumMid / nMid) : 0,
                Defence     = nDef > 0 ? (int)(sumDef / nDef) : 0,
                Goalkeeping = nGk  > 0 ? (int)(sumGk  / nGk)  : 0,
                Chemistry   = Chemistry(picked, formation),
            };
            return r;
        }

        /// <summary> Chemistry score: same-club pairs + explicit legend links, minus formation-balance penalties. </summary>
        public static int Chemistry(List<LegendPlayer> picked, FormationInfo formation)
        {
            int chem = 0;

            // Same-club pairs and explicit legendary links.
            for (int i = 0; i < picked.Count; i++)
            {
                for (int j = i + 1; j < picked.Count; j++)
                {
                    if (picked[i].Club == picked[j].Club)
                        chem += SameClubPairPoints;
                    if (LinkedPair(picked[i], picked[j]))
                        chem += LegendLinkPoints;
                }
            }

            // Formation balance: a fielded XI wants a keeper and a real back line.
            if (formation != null)
            {
                bool hasGk = false;
                int  defCount = 0;
                foreach (LegendPlayer p in picked)
                {
                    if (p.Position == Position.GK)  hasGk = true;
                    if (p.Position == Position.DEF) defCount++;
                }
                if (!hasGk)        chem -= NoGoalkeeperPenalty;
                if (defCount < 3)  chem -= ThinDefencePenalty;
            }

            return chem;
        }

        static bool LinkedPair(LegendPlayer a, LegendPlayer b)
        {
            if (a.ChemLinks != null && a.ChemLinks.Contains(b.Id.Value)) return true;
            if (b.ChemLinks != null && b.ChemLinks.Contains(a.Id.Value)) return true;
            return false;
        }
    }
}
