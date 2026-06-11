// FOOTDRAFT — quests (WS4 retention + the interconnected meta loop). Two scopes:
//   Daily  — rotating per-day objectives (reset at UTC midnight); the first GlobalConfig.DailyQuestSlots
//            daily-scope quests (config order) are active, and an unclaimed one can be REROLLED for Gems.
//   Season — long-arc objectives spanning the whole 38-matchday league season (keyed to the league code,
//            so joining a new league starts a fresh season slate).
// Rewards feed the wallet (Coins = transfer money, occasional Gems), closing the play→earn→sign loop.

using System;
using System.Collections.Generic;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;

namespace Game.Logic
{
    /// <summary> A trackable metric a quest can target. </summary>
    [MetaSerializable]
    public enum QuestMetric
    {
        MatchesPlayed = 0,
        MatchesWon    = 1,
        GoalsScored   = 2,
        TransfersMade = 3,
    }

    /// <summary> Whether a quest resets daily or runs for the whole league season. </summary>
    [MetaSerializable]
    public enum QuestScope
    {
        Daily  = 0,
        Season = 1,
    }

    /// <summary> Identifier for a quest. </summary>
    [MetaSerializable]
    public class QuestId : StringId<QuestId> { }

    /// <summary> A quest definition: hit <see cref="Target"/> of <see cref="Metric"/> for <see cref="RewardCoins"/> (and possibly <see cref="RewardGems"/>). </summary>
    [MetaSerializable]
    public class QuestInfo : IGameConfigData<QuestId>
    {
        [MetaMember(1)] public QuestId     Id          { get; private set; }
        [MetaMember(2)] public string      Description { get; private set; } = "";
        [MetaMember(3)] public QuestMetric Metric      { get; private set; }
        [MetaMember(4)] public int         Target      { get; private set; }
        [MetaMember(5)] public int         RewardCoins { get; private set; }
        [MetaMember(6)] public QuestScope  Scope       { get; private set; } = QuestScope.Daily;
        [MetaMember(7)] public int         RewardGems  { get; private set; }

        public QuestId ConfigKey => Id;

        public QuestInfo() { }
        public QuestInfo(string id, string description, QuestMetric metric, int target, int rewardCoins, QuestScope scope = QuestScope.Daily, int rewardGems = 0)
        {
            Id          = QuestId.FromString(id);
            Description = description;
            Metric      = metric;
            Target      = target;
            RewardCoins = rewardCoins;
            Scope       = scope;
            RewardGems  = rewardGems;
        }
    }

    /// <summary> A manager's quest progress: daily slate rolls over each UTC day; season slate per league. </summary>
    [MetaSerializable]
    public class PlayerQuests
    {
        /// <summary> Days-since-epoch (UTC) of the day this daily progress belongs to. </summary>
        [MetaMember(1)] public long                          DayKey         { get; set; }
        /// <summary> questId → progress count for the current day (daily-scope quests). </summary>
        [MetaMember(2)] public MetaDictionary<string, int>   Progress       { get; set; } = new MetaDictionary<string, int>();
        /// <summary> questId → claimed-this-day (daily-scope quests). </summary>
        [MetaMember(3)] public MetaDictionary<string, bool>  Claimed        { get; set; } = new MetaDictionary<string, bool>();
        /// <summary> League code the season slate belongs to; a different code resets the season slate. </summary>
        [MetaMember(4)] public string                        SeasonScopeKey { get; set; } = "";
        /// <summary> questId → progress count for the current league season (season-scope quests). </summary>
        [MetaMember(5)] public MetaDictionary<string, int>   SeasonProgress { get; set; } = new MetaDictionary<string, int>();
        /// <summary> questId → claimed-this-season (season-scope quests). </summary>
        [MetaMember(6)] public MetaDictionary<string, bool>  SeasonClaimed  { get; set; } = new MetaDictionary<string, bool>();
        /// <summary> Daily reroll substitutions for today: base-slot questId → replacement questId. Cleared at day rollover. </summary>
        [MetaMember(7)] public MetaDictionary<string, string> Rerolled      { get; set; } = new MetaDictionary<string, string>();

        public PlayerQuests() { }
    }

    /// <summary> Default quests (code-defined; sheet-backed via the "Quests" tab). </summary>
    public static class QuestContent
    {
        public static readonly QuestInfo[] Quests =
        {
            // --- Daily (first DailyQuestSlots = 3 are the base slate; the rest enter via reroll) ---
            // Tuned to the league cadence: one matchday sims per league per day, so a single-league manager can
            // clear the first two; "play 3" rewards multiple leagues / commissioner force-plays.
            new QuestInfo("daily_play_1",     "Play a league match",    QuestMetric.MatchesPlayed, 1,  100),
            new QuestInfo("daily_win_1",      "Win a league match",     QuestMetric.MatchesWon,    1,  150),
            new QuestInfo("daily_play_3",     "Play 3 league matches",  QuestMetric.MatchesPlayed, 3,  300),
            new QuestInfo("daily_score_2",    "Score 2 goals",          QuestMetric.GoalsScored,   2,  120),
            new QuestInfo("daily_transfer_1", "Sign a player",          QuestMetric.TransfersMade, 1,  150),
            new QuestInfo("daily_score_4",    "Score 4 goals",          QuestMetric.GoalsScored,   4,  250),

            // --- Season (the long arc across all 38 matchdays; gems on the milestones) ---
            new QuestInfo("season_play_10",     "Play 10 matchdays",      QuestMetric.MatchesPlayed, 10, 400,  QuestScope.Season),
            new QuestInfo("season_win_10",      "Win 10 matches",         QuestMetric.MatchesWon,    10, 600,  QuestScope.Season),
            new QuestInfo("season_transfers_5", "Make 5 transfers",       QuestMetric.TransfersMade, 5,  500,  QuestScope.Season),
            new QuestInfo("season_goals_40",    "Score 40 goals",         QuestMetric.GoalsScored,   40, 800,  QuestScope.Season),
            new QuestInfo("season_win_20",      "Win 20 matches",         QuestMetric.MatchesWon,    20, 1200, QuestScope.Season, rewardGems: 10),
            new QuestInfo("season_transfers_10","Make 10 transfers",      QuestMetric.TransfersMade, 10, 600,  QuestScope.Season, rewardGems: 10),
            new QuestInfo("season_goals_75",    "Score 75 goals",         QuestMetric.GoalsScored,   75, 1500, QuestScope.Season, rewardGems: 15),
            new QuestInfo("season_play_38",     "Complete all 38 matchdays", QuestMetric.MatchesPlayed, 38, 1500, QuestScope.Season, rewardGems: 10),
        };

        public static GameConfigLibrary<QuestId, QuestInfo> CreateLibrary()
            => GameConfigLibrary<QuestId, QuestInfo>.CreateSolo(Quests);
    }

    /// <summary> Pure quest logic. Time comes from the model (player.CurrentTime) so it stays deterministic. </summary>
    public static class QuestEngine
    {
        /// <summary> Days since the Unix epoch (UTC) for the given model time. Integer math — runs in client-predicted actions, so no floating point. </summary>
        public static long DayOf(MetaTime now) => now.MillisecondsSinceEpoch / 86_400_000;

        /// <summary> Reset daily progress + claims + rerolls when the UTC day has rolled over. </summary>
        public static void SyncDay(PlayerQuests quests, MetaTime now)
        {
            long today = DayOf(now);
            if (quests.DayKey != today)
            {
                quests.DayKey = today;
                quests.Progress.Clear();
                quests.Claimed.Clear();
                quests.Rerolled.Clear();
            }
        }

        /// <summary> Reset the season slate when the player's league changes (a new league = a new 38-matchday season). </summary>
        public static void SyncSeasonScope(PlayerQuests quests, string leagueCode)
        {
            string key = leagueCode ?? "";
            if (quests.SeasonScopeKey != key)
            {
                quests.SeasonScopeKey = key;
                quests.SeasonProgress.Clear();
                quests.SeasonClaimed.Clear();
            }
        }

        /// <summary>
        /// Add progress to every quest targeting <paramref name="metric"/>, capped at target. Daily-scope quests
        /// track in the per-day slate; season-scope quests track in the per-league slate (skipped when the player
        /// isn't in a league).
        /// </summary>
        public static void Advance(PlayerQuests quests, IEnumerable<QuestInfo> defs, QuestMetric metric, int amount, MetaTime now, string leagueCode)
        {
            if (amount <= 0 || defs == null)
                return;
            SyncDay(quests, now);
            bool inLeague = !string.IsNullOrEmpty(leagueCode);
            if (inLeague)
                SyncSeasonScope(quests, leagueCode);
            foreach (QuestInfo def in defs)
            {
                if (def.Metric != metric)
                    continue;
                if (def.Scope == QuestScope.Daily)
                {
                    quests.Progress.TryGetValue(def.Id.Value, out int cur);
                    quests.Progress[def.Id.Value] = Math.Min(def.Target, cur + amount);
                }
                else if (inLeague)
                {
                    quests.SeasonProgress.TryGetValue(def.Id.Value, out int cur);
                    quests.SeasonProgress[def.Id.Value] = Math.Min(def.Target, cur + amount);
                }
            }
        }

        public static bool IsComplete(QuestInfo def, PlayerQuests quests)
        {
            int progress = GetProgress(def, quests);
            return progress >= def.Target;
        }

        public static bool IsClaimed(QuestInfo def, PlayerQuests quests)
        {
            if (def.Scope == QuestScope.Daily)
            {
                quests.Claimed.TryGetValue(def.Id.Value, out bool claimed);
                return claimed;
            }
            quests.SeasonClaimed.TryGetValue(def.Id.Value, out bool seasonClaimed);
            return seasonClaimed;
        }

        public static int GetProgress(QuestInfo def, PlayerQuests quests)
        {
            int progress;
            if (def.Scope == QuestScope.Daily)
                quests.Progress.TryGetValue(def.Id.Value, out progress);
            else
                quests.SeasonProgress.TryGetValue(def.Id.Value, out progress);
            return progress;
        }

        /// <summary>
        /// Today's active daily quests: the first <paramref name="slots"/> daily-scope definitions in config order,
        /// with the player's reroll substitutions applied. Deterministic from config + model, so it's safe in
        /// client-predicted actions.
        /// </summary>
        public static List<QuestInfo> EffectiveDailySet(IEnumerable<QuestInfo> defs, PlayerQuests quests, int slots)
        {
            List<QuestInfo> daily = new List<QuestInfo>();
            foreach (QuestInfo def in defs)
            {
                if (def.Scope == QuestScope.Daily)
                    daily.Add(def);
            }

            List<QuestInfo> result = new List<QuestInfo>();
            int take = Math.Min(slots, daily.Count);
            for (int i = 0; i < take; i++)
            {
                QuestInfo slotDef = daily[i];
                if (quests.Rerolled.TryGetValue(slotDef.Id.Value, out string replacementId))
                {
                    foreach (QuestInfo candidate in daily)
                    {
                        if (candidate.Id.Value == replacementId)
                        {
                            slotDef = candidate;
                            break;
                        }
                    }
                }
                result.Add(slotDef);
            }
            return result;
        }

        /// <summary>
        /// The next daily-scope definition (config order) not already used today — neither active in the effective
        /// set nor already rerolled away — or null if the config has no spares. Used by the Gem-paid reroll.
        /// </summary>
        public static QuestInfo NextRerollCandidate(IEnumerable<QuestInfo> defs, PlayerQuests quests, int slots)
        {
            List<QuestInfo> active = EffectiveDailySet(defs, quests, slots);
            foreach (QuestInfo def in defs)
            {
                if (def.Scope != QuestScope.Daily)
                    continue;
                if (quests.Rerolled.ContainsKey(def.Id.Value))
                    continue; // already rerolled away today — don't offer it back
                bool inSet = false;
                foreach (QuestInfo activeDef in active)
                {
                    if (activeDef.Id == def.Id) { inSet = true; break; }
                }
                if (!inSet)
                    return def;
            }
            return null;
        }

        /// <summary>
        /// The base-slot id the given active quest occupies (the Rerolled dictionary key): the quest's own id if
        /// it's an original slot, or the original slot's id if the quest arrived via reroll. Returns null if the
        /// quest isn't in the effective set.
        /// </summary>
        public static string SlotKeyFor(IEnumerable<QuestInfo> defs, PlayerQuests quests, int slots, string questId)
        {
            List<QuestInfo> daily = new List<QuestInfo>();
            foreach (QuestInfo def in defs)
            {
                if (def.Scope == QuestScope.Daily)
                    daily.Add(def);
            }
            int take = Math.Min(slots, daily.Count);
            for (int i = 0; i < take; i++)
            {
                string slotKey = daily[i].Id.Value;
                string currentId = quests.Rerolled.TryGetValue(slotKey, out string replacement) ? replacement : slotKey;
                if (currentId == questId)
                    return slotKey;
            }
            return null;
        }
    }
}
