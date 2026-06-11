// FOOTDRAFT — head-to-head match model. Two managers' drafted XIs (resolved to LineRatings) play a single
// deterministic football match (see MatchSim): goals are precomputed at kick-off and revealed minute-by-minute.

using Metaplay.Core;
using Metaplay.Core.Model;
using Metaplay.Core.MultiplayerEntity;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Game.Logic
{
    /// <summary> Phase of a match. </summary>
    [MetaSerializable]
    public enum MatchPhase
    {
        /// <summary> Pre-match briefing: both XIs are shown before kick-off. </summary>
        Starting = 0,
        /// <summary> In progress: the 90 minutes play out and goals are revealed. </summary>
        Active = 1,
        /// <summary> Full time; <see cref="MatchModel.Winner"/> holds the winning manager. </summary>
        Ended = 2,
    }

    /// <summary>
    /// One player in a national-team dice squad (legacy squad path; still used by <see cref="SquadSpec"/>
    /// and the national-team manager UI). FOOTDRAFT matches use <see cref="LineRatings"/>, not these.
    /// </summary>
    [MetaSerializable]
    public class SquadPlayer
    {
        [MetaMember(1)] public string Name  { get; set; }
        [MetaMember(2)] public int    Sides { get; set; }

        public SquadPlayer() { }
        public SquadPlayer(string name, int sides)
        {
            Name = name;
            Sides = sides;
        }
    }

    /// <summary>
    /// Per-manager state within a match: their identity, their XI's <see cref="LineRatings"/>, and goals scored.
    /// </summary>
    [MetaSerializable]
    public class MatchSquadState
    {
        [MetaMember(1)] public string      Name    { get; set; }
        [MetaMember(2)] public string      Crest   { get; set; }   // emoji shown for the manager / XI
        [MetaMember(3)] public bool        IsBot   { get; set; }
        [MetaMember(4)] public LineRatings Ratings { get; set; } = new LineRatings();
        [MetaMember(5)] public int         Goals   { get; set; }

        public MatchSquadState() { }
        public MatchSquadState(string name, string crest, bool isBot, LineRatings ratings)
        {
            Name    = name;
            Crest   = crest;
            IsBot   = isBot;
            Ratings = ratings;
        }

        /// <summary> A single squad-strength number (weighted line blend) — for display + the draw tiebreak. </summary>
        public int Overall => (Ratings.Attack * 3 + Ratings.Midfield * 3 + Ratings.Defence * 3 + Ratings.Goalkeeping) / 10
                              + (Ratings.Chemistry > 0 ? Ratings.Chemistry / 4 : 0);
    }

    /// <summary> A goal in the match timeline: the minute it falls and which manager scored it. </summary>
    [MetaSerializable]
    public class MatchGoal
    {
        [MetaMember(1)] public int      Minute { get; set; }
        [MetaMember(2)] public EntityId Scorer { get; set; }

        public MatchGoal() { }
        public MatchGoal(int minute, EntityId scorer)
        {
            Minute = minute;
            Scorer = scorer;
        }
    }

    /// <summary>
    /// Shared model for a single FOOTDRAFT match. Owned and ticked by the server; observed by both clients. At
    /// kick-off the full 90-minute scoreline is computed once via <see cref="MatchSim"/> (seeded from synchronized
    /// model state, so server and every client produce the identical match), then goals are revealed minute by
    /// minute as the clock runs. A drawn sim gets a deterministic stoppage-time winner so 1v1s are always decisive.
    /// </summary>
    [MetaSerializableDerived(100)]
    [SupportedSchemaVersions(1, 1)]
    public class MatchModel : MultiplayerModelBase<MatchModel>
    {
        public const int TicksPerSecondConst = 10;
        public override int TicksPerSecond => TicksPerSecondConst;

        // Pre-match briefing window before kick-off.
        public const int StartDelaySeconds = 3;
        public const int StartDelayTicks   = StartDelaySeconds * TicksPerSecondConst;

        // Wall-clock length of the played-out 90 minutes (the goals reveal across this window).
        public const int MatchDurationSeconds = 14;
        public const int MatchDurationTicks   = MatchDurationSeconds * TicksPerSecondConst;
        public const int FullTimeMinute       = 90;

        [IgnoreDataMember] public IMatchModelClientListener ClientListener { get; set; } = EmptyMatchModelClientListener.Instance;
        [IgnoreDataMember] public IMatchModelServerListener ServerListener { get; set; } = EmptyMatchModelServerListener.Instance;

        [MetaMember(1)] public MatchPhase                                Phase           { get; set; } = MatchPhase.Starting;
        [MetaMember(2)] public MetaDictionary<EntityId, MatchSquadState> Squads          { get; set; } = new MetaDictionary<EntityId, MatchSquadState>();
        /// <summary> The full precomputed goal timeline, built at kick-off and revealed over the clock. </summary>
        [MetaMember(3)] public List<MatchGoal>                           Goals           { get; set; } = new List<MatchGoal>();
        [MetaMember(4)] public bool                                      TimelineBuilt   { get; set; }
        [MetaMember(5)] public long                                      ActiveStartTick { get; set; }
        /// <summary> How many timeline goals have been revealed (and tallied) so far. </summary>
        [MetaMember(6)] public int                                       RevealedGoals   { get; set; }
        /// <summary> The current match minute (0..90), for the clock display. </summary>
        [MetaMember(7)] public int                                       CurrentMinute   { get; set; }
        [MetaMember(8)] public EntityId                                  Winner          { get; set; } = EntityId.None;

        public override void OnTick()
        {
            if (Phase == MatchPhase.Starting)
            {
                if (CurrentTick >= StartDelayTicks)
                {
                    Phase           = MatchPhase.Active;
                    ActiveStartTick = CurrentTick;
                    BuildTimeline();
                }
                return;
            }

            if (Phase != MatchPhase.Active)
                return;

            long elapsed = CurrentTick - ActiveStartTick;
            int  minute  = (int)(elapsed * FullTimeMinute / MatchDurationTicks);
            if (minute < 0) minute = 0;
            if (minute > FullTimeMinute) minute = FullTimeMinute;
            CurrentMinute = minute;

            // Reveal any goals whose minute has now passed.
            while (RevealedGoals < Goals.Count && Goals[RevealedGoals].Minute <= minute)
            {
                MatchGoal goal = Goals[RevealedGoals];
                if (Squads.TryGetValue(goal.Scorer, out MatchSquadState scorer))
                    scorer.Goals++;
                RevealedGoals++;
                ClientListener.GoalScored(goal.Scorer, goal.Minute);
            }

            // Full time once the clock has run out and every goal is shown.
            if (elapsed >= MatchDurationTicks && RevealedGoals >= Goals.Count)
                EndMatch();
        }

        public override void OnFastForwardTime(MetaDuration elapsedTime)
        {
            // Per-tick logic is keyed purely on CurrentTick + synchronized state, so re-simulation reproduces it.
        }

        #region Match flow

        /// <summary>
        /// The squads in a stable order: human managers first (join order), then bots. The first is treated as
        /// "home" by the sim (the split is rating-based, so this carries no advantage — just a fixed orientation).
        /// </summary>
        IEnumerable<EntityId> OrderedSquadIds()
        {
            foreach ((EntityId id, MatchSquadState squad) in Squads)
                if (!squad.IsBot)
                    yield return id;
            foreach ((EntityId id, MatchSquadState squad) in Squads)
                if (squad.IsBot)
                    yield return id;
        }

        /// <summary> Computes the whole match once at kick-off (deterministic), filling the goal timeline. </summary>
        void BuildTimeline()
        {
            if (TimelineBuilt)
                return;
            TimelineBuilt = true;

            EntityId homeId = EntityId.None, awayId = EntityId.None;
            foreach (EntityId id in OrderedSquadIds())
            {
                if (homeId == EntityId.None) homeId = id;
                else { awayId = id; break; }
            }
            if (homeId == EntityId.None || awayId == EntityId.None)
                return; // not a 2-squad match; nothing to play

            MatchSquadState home = Squads[homeId];
            MatchSquadState away = Squads[awayId];

            ulong seed = (ulong)TimeAtFirstTick.MillisecondsSinceEpoch * 0x9E3779B97F4A7C15ul ^ 0xD1B54A32D192ED03ul;
            MatchResult result = MatchSim.Resolve(home.Ratings, away.Ratings, seed);

            foreach (GoalEvent ev in result.Goals)
                Goals.Add(new MatchGoal(ev.Minute, ev.HomeScored ? homeId : awayId));

            // A drawn sim gets a deterministic stoppage-time winner (stronger XI; tie → home) so 1v1s decide.
            if (result.IsDraw)
            {
                EntityId clincher = home.Overall >= away.Overall ? homeId : awayId;
                Goals.Add(new MatchGoal(FullTimeMinute, clincher));
            }

            Goals.Sort((a, b) => a.Minute.CompareTo(b.Minute));
        }

        void EndMatch()
        {
            // Safety: ensure every goal is tallied even if the reveal loop was interrupted.
            while (RevealedGoals < Goals.Count)
            {
                MatchGoal goal = Goals[RevealedGoals];
                if (Squads.TryGetValue(goal.Scorer, out MatchSquadState scorer))
                    scorer.Goals++;
                RevealedGoals++;
            }

            EntityId best = EntityId.None;
            int bestGoals = -1, bestOverall = -1;
            foreach (EntityId id in OrderedSquadIds())
            {
                MatchSquadState squad = Squads[id];
                if (squad.Goals > bestGoals || (squad.Goals == bestGoals && squad.Overall > bestOverall))
                {
                    best        = id;
                    bestGoals   = squad.Goals;
                    bestOverall = squad.Overall;
                }
            }

            Phase  = MatchPhase.Ended;
            Winner = best;
            ClientListener.MatchEnded(Winner);
            ServerListener.MatchEnded(Winner);
        }

        #endregion

        #region Queries

        public int GoalsOf(EntityId squadId) => Squads.TryGetValue(squadId, out MatchSquadState squad) ? squad.Goals : 0;

        #endregion

        public override string GetDisplayNameForDashboard() => $"FootDraft Match ({Squads.Count} squads)";
    }
}
