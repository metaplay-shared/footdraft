// FOOTDRAFT — pure, deterministic league-draft logic: the turn-based snake draft that fills every manager's XI
// from a SHARED pool of legends (so no legend can appear on two teams).
//
// No state, no I/O — the LeagueActor holds the mutable draft state (order, rosters, taken pool) and calls these
// helpers; the unit tests drive the exact same functions. The three guarantees the vision asks for live here:
//   • turns      — CurrentDrafterIndex walks a snake order (1..N, N..1, …) so picking is fair and sequential.
//   • uniqueness — a pick is only valid if the legend is not already in the shared `taken` set.
//   • valid XI   — a pick is only valid if the manager's formation still has an open slot for that position;
//                  NextOpenSlotForPosition returns -1 when the position is already full.

using System.Collections.Generic;

namespace Game.Logic
{
    public static class LeagueDraftEngine
    {
        /// <summary> Every manager drafts a full XI. </summary>
        public const int PicksPerTeam = 11;

        /// <summary>
        /// The draftable pool for a league: the full corpus deduplicated to ONE entry per real player (by name),
        /// keeping that player's highest-rated variant. The corpus carries a separate entry per (Club, Era) a
        /// player featured in — great for the solo spin-draft, but in a shared-pool league it would let two
        /// managers each draft a different "Buffon". Deduping by name enforces the real rule: a given footballer
        /// can be on only one team. Deterministic (first-seen order; highest OVR wins ties by appearance).
        /// </summary>
        public static List<LegendPlayer> BuildDraftPool(IReadOnlyList<LegendPlayer> corpus)
        {
            Dictionary<string, LegendPlayer> bestByName = new Dictionary<string, LegendPlayer>();
            List<string> order = new List<string>();
            foreach (LegendPlayer p in corpus)
            {
                if (p == null)
                    continue;
                string key = p.Name ?? p.Id.Value;
                if (!bestByName.TryGetValue(key, out LegendPlayer cur))
                {
                    bestByName[key] = p;
                    order.Add(key);
                }
                else if (p.Ovr > cur.Ovr)
                {
                    bestByName[key] = p;
                }
            }
            List<LegendPlayer> pool = new List<LegendPlayer>(order.Count);
            foreach (string key in order)
                pool.Add(bestByName[key]);
            return pool;
        }

        /// <summary> Total picks in a league draft = one full XI per manager. </summary>
        public static int TotalPicks(int memberCount) => memberCount * PicksPerTeam;

        /// <summary> True once every manager has drafted their full XI. </summary>
        public static bool IsComplete(int pick, int memberCount) => pick >= TotalPicks(memberCount);

        /// <summary>
        /// The member index whose turn it is at global pick number <paramref name="pick"/> (0-based), using a
        /// snake order over join order: round 0 goes 0→N-1, round 1 goes N-1→0, and so on. The snake keeps the
        /// draft fair (the manager who picks last in a round picks first in the next). Returns -1 if no members.
        /// </summary>
        public static int CurrentDrafterIndex(int pick, int memberCount)
        {
            if (memberCount <= 0 || IsComplete(pick, memberCount))
                return -1;
            int round = pick / memberCount;
            int pos   = pick % memberCount;
            return (round % 2 == 0) ? pos : (memberCount - 1 - pos);
        }

        /// <summary> The 1-based draft round at pick number <paramref name="pick"/> (round 1 = each manager's first pick). </summary>
        public static int RoundNumber(int pick, int memberCount)
            => memberCount <= 0 ? 0 : (pick / memberCount) + 1;

        /// <summary>
        /// The lowest formation slot index of <paramref name="position"/> not yet filled in <paramref name="roster"/>,
        /// or -1 if every slot of that position is already taken. This is both the position-eligibility check and
        /// the slot assignment for a pick: a manager can only draft a position their formation still needs.
        /// </summary>
        public static int NextOpenSlotForPosition(FormationInfo formation, IReadOnlyDictionary<int, string> roster, Position position)
        {
            if (formation == null)
                return -1;
            for (int slot = 0; slot < formation.Slots.Count; slot++)
            {
                if (formation.Slots[slot] == position && (roster == null || !roster.ContainsKey(slot)))
                    return slot;
            }
            return -1;
        }

        /// <summary> The set of positions the manager's formation still has open slots for (drives the pick list + autopick). </summary>
        public static HashSet<Position> OpenPositions(FormationInfo formation, IReadOnlyDictionary<int, string> roster)
        {
            HashSet<Position> open = new HashSet<Position>();
            if (formation == null)
                return open;
            for (int slot = 0; slot < formation.Slots.Count; slot++)
                if (roster == null || !roster.ContainsKey(slot))
                    open.Add(formation.Slots[slot]);
            return open;
        }

        /// <summary>
        /// True if <paramref name="legend"/> can be drafted now: the real player isn't already taken (uniqueness is by
        /// player NAME, since the same footballer appears in many club-season squads), and the manager's formation
        /// still has an open slot for that position. <paramref name="taken"/> holds taken player names.
        /// </summary>
        public static bool CanPick(FormationInfo formation, IReadOnlyDictionary<int, string> roster, ICollection<string> taken, LegendPlayer legend)
        {
            if (legend == null)
                return false;
            if (taken != null && taken.Contains(legend.Name))
                return false;
            return NextOpenSlotForPosition(formation, roster, legend.Position) >= 0;
        }

        /// <summary>
        /// The best available pick for a manager: the highest-OVR legend in <paramref name="corpus"/> that is not
        /// taken and fits an open slot in their formation. Deterministic (first of equal OVR in corpus order wins).
        /// Returns null if the pool is exhausted for every open position. Used for "auto-pick" / unstalling.
        /// </summary>
        public static LegendPlayer BestAvailablePick(FormationInfo formation, IReadOnlyDictionary<int, string> roster, ICollection<string> taken, IReadOnlyList<LegendPlayer> corpus)
        {
            HashSet<Position> open = OpenPositions(formation, roster);
            if (open.Count == 0 || corpus == null)
                return null;

            LegendPlayer best = null;
            foreach (LegendPlayer legend in corpus)
            {
                if (!open.Contains(legend.Position))
                    continue;
                if (taken != null && taken.Contains(legend.Name)) // uniqueness by real-player name
                    continue;
                if (best == null || legend.Ovr > best.Ovr)
                    best = legend;
            }
            return best;
        }

        /// <summary>
        /// Resolves a drafted roster (formation slot → legend id) to the <see cref="LineRatings"/> the match sim
        /// consumes, reusing the same chemistry/line maths as the solo draft (<see cref="DraftEngine.ComputeLines"/>).
        /// </summary>
        public static LineRatings ResolveRosterRatings(FormationInfo formation, IReadOnlyDictionary<int, string> roster, System.Func<LegendId, LegendPlayer> lookup)
        {
            DraftedSquad squad = new DraftedSquad { Formation = formation?.Id };
            if (roster != null)
            {
                foreach ((int slot, string legendId) in roster)
                    squad.Picks[slot] = LegendId.FromString(legendId);
            }
            return DraftEngine.ComputeLines(squad, formation, lookup);
        }
    }

    /// <summary>
    /// Pure validation for a transfer-window swap: drop one drafted legend and add another into the same slot.
    /// Same two guarantees as the draft — uniqueness (the added player isn't on another team) and a valid XI
    /// (the added player fits the freed slot's position). Affordability is the PlayerAction's job: the signing
    /// fee is charged from the player's wallet inside PlayerLeagueTransferSwap, not from a league budget. No state.
    /// </summary>
    public static class LeagueTransferEngine
    {
        /// <summary>
        /// Returns "" if the swap is allowed (and sets <paramref name="slot"/> to the roster slot being changed),
        /// otherwise a human-readable error. <paramref name="taken"/> holds the player NAMES taken across the league.
        /// </summary>
        public static string ValidateSwap(
            FormationInfo formation,
            IReadOnlyDictionary<int, string> roster,
            ICollection<string> taken,
            LegendPlayer dropLegend, LegendPlayer addLegend,
            out int slot)
        {
            slot = -1;
            if (formation == null) return "League not ready";
            if (dropLegend == null) return "Unknown player to drop";
            if (addLegend == null)  return "Unknown player to add";
            if (dropLegend.Name == addLegend.Name) return "Pick a different player";

            // Find the slot currently holding the dropped legend.
            if (roster != null)
            {
                foreach ((int s, string id) in roster)
                {
                    if (id == dropLegend.Id.Value) { slot = s; break; }
                }
            }
            if (slot < 0) return "That player isn't in your XI";

            // The added legend must match the freed slot's position and not be on any team.
            if (slot >= formation.Slots.Count) return "Invalid slot";
            if (addLegend.Position != formation.Slots[slot]) return $"Need a {formation.Slots[slot]} for that slot";
            if (taken != null && taken.Contains(addLegend.Name)) return $"{addLegend.Name} is already on another team";

            return "";
        }
    }
}
