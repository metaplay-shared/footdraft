// FOOTDRAFT — WS4: quest progress, capping, daily rollover, season scope, new metrics and the daily reroll.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Metaplay.Core;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class QuestTests
    {
        const string League = "ABCD";

        static MetaTime Day(int year, int month, int day) =>
            MetaTime.FromDateTime(new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc));

        static QuestInfo Def(string id) => Array.Find(QuestContent.Quests, x => x.Id.Value == id);

        [Test]
        public void AdvanceIncrementsMatchingQuestsAndCapsAtTarget()
        {
            PlayerQuests q = new PlayerQuests();
            QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.MatchesPlayed, 5, Day(2026, 1, 1), League);

            Assert.That(QuestEngine.IsComplete(Def("daily_play_1"), q), Is.True, "play-1 completed");
            Assert.That(QuestEngine.IsComplete(Def("daily_play_3"), q), Is.True, "play-3 completed (capped at 3)");
            Assert.That(QuestEngine.IsComplete(Def("daily_win_1"), q), Is.False, "win quest untouched by plays");
        }

        [Test]
        public void DifferentMetricsAreTrackedSeparately()
        {
            PlayerQuests q = new PlayerQuests();
            QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.MatchesPlayed, 3, Day(2026, 1, 1), League);
            Assert.That(QuestEngine.IsComplete(Def("daily_play_3"), q), Is.True);
            Assert.That(QuestEngine.IsComplete(Def("daily_win_1"), q), Is.False);
        }

        [Test]
        public void ProgressResetsOnANewDay()
        {
            PlayerQuests q = new PlayerQuests();
            QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.MatchesPlayed, 3, Day(2026, 1, 1), League);
            Assert.That(QuestEngine.IsComplete(Def("daily_play_3"), q), Is.True);

            // A match the next day rolls the quest day over and starts fresh.
            QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.MatchesPlayed, 1, Day(2026, 1, 2), League);
            Assert.That(QuestEngine.IsComplete(Def("daily_play_3"), q), Is.False, "play-3 reset for the new day");
            Assert.That(QuestEngine.IsComplete(Def("daily_play_1"), q), Is.True, "play-1 met again with one match");
        }

        [Test]
        public void ClaimedFlagIsReadBack()
        {
            PlayerQuests q = new PlayerQuests();
            QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.MatchesWon, 1, Day(2026, 1, 1), League);
            Assert.That(QuestEngine.IsClaimed(Def("daily_win_1"), q), Is.False);
            q.Claimed["daily_win_1"] = true;
            Assert.That(QuestEngine.IsClaimed(Def("daily_win_1"), q), Is.True);
        }

        [Test]
        public void SeasonProgressSurvivesTheDayRollover()
        {
            PlayerQuests q = new PlayerQuests();
            for (int day = 1; day <= 10; day++)
                QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.MatchesPlayed, 1, Day(2026, 1, day), League);

            Assert.That(QuestEngine.IsComplete(Def("season_play_10"), q), Is.True, "10 matchdays accumulated across 10 days");
            Assert.That(QuestEngine.GetProgress(Def("season_play_38"), q), Is.EqualTo(10));
            Assert.That(QuestEngine.IsComplete(Def("daily_play_3"), q), Is.False, "daily slate reset each day");
        }

        [Test]
        public void SeasonProgressResetsWhenTheLeagueChanges()
        {
            PlayerQuests q = new PlayerQuests();
            QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.MatchesWon, 9, Day(2026, 1, 1), League);
            Assert.That(QuestEngine.GetProgress(Def("season_win_10"), q), Is.EqualTo(9));

            // Joining a NEW league starts a fresh season slate.
            QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.MatchesWon, 1, Day(2026, 1, 1), "WXYZ");
            Assert.That(QuestEngine.GetProgress(Def("season_win_10"), q), Is.EqualTo(1), "old league's progress discarded");
        }

        [Test]
        public void GoalsAndTransfersMetricsAdvanceTheirQuests()
        {
            PlayerQuests q = new PlayerQuests();
            QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.GoalsScored, 2, Day(2026, 1, 1), League);
            QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.TransfersMade, 1, Day(2026, 1, 1), League);

            Assert.That(QuestEngine.IsComplete(Def("daily_score_2"), q), Is.True);
            Assert.That(QuestEngine.IsComplete(Def("daily_transfer_1"), q), Is.True);
            Assert.That(QuestEngine.GetProgress(Def("season_goals_40"), q), Is.EqualTo(2));
            Assert.That(QuestEngine.GetProgress(Def("season_transfers_5"), q), Is.EqualTo(1));
        }

        [Test]
        public void SeasonProgressIsSkippedOutsideALeague()
        {
            PlayerQuests q = new PlayerQuests();
            QuestEngine.Advance(q, QuestContent.Quests, QuestMetric.MatchesPlayed, 1, Day(2026, 1, 1), "");
            Assert.That(QuestEngine.IsComplete(Def("daily_play_1"), q), Is.True, "daily still tracks");
            Assert.That(QuestEngine.GetProgress(Def("season_play_10"), q), Is.EqualTo(0), "season slate untouched");
        }

        [Test]
        public void EffectiveDailySetIsTheFirstSlotsInConfigOrder()
        {
            PlayerQuests q = new PlayerQuests();
            List<QuestInfo> set = QuestEngine.EffectiveDailySet(QuestContent.Quests, q, 3);
            Assert.That(set.Count, Is.EqualTo(3));
            Assert.That(set[0].Id.Value, Is.EqualTo("daily_play_1"));
            Assert.That(set[1].Id.Value, Is.EqualTo("daily_win_1"));
            Assert.That(set[2].Id.Value, Is.EqualTo("daily_play_3"));
        }

        [Test]
        public void RerollSubstitutesTheSlotAndChainsCorrectly()
        {
            PlayerQuests q = new PlayerQuests();

            // First reroll of the win-1 slot: replaced by the first daily def not in the active set.
            QuestInfo first = QuestEngine.NextRerollCandidate(QuestContent.Quests, q, 3);
            Assert.That(first.Id.Value, Is.EqualTo("daily_score_2"));
            q.Rerolled["daily_win_1"] = first.Id.Value;

            List<QuestInfo> set = QuestEngine.EffectiveDailySet(QuestContent.Quests, q, 3);
            Assert.That(set[1].Id.Value, Is.EqualTo("daily_score_2"), "slot 2 now holds the replacement");

            // Rerolling the REPLACEMENT updates the same slot (chained), not a second slot.
            Assert.That(QuestEngine.SlotKeyFor(QuestContent.Quests, q, 3, "daily_score_2"), Is.EqualTo("daily_win_1"));
            QuestInfo second = QuestEngine.NextRerollCandidate(QuestContent.Quests, q, 3);
            Assert.That(second.Id.Value, Is.EqualTo("daily_transfer_1"));
            q.Rerolled["daily_win_1"] = second.Id.Value;

            set = QuestEngine.EffectiveDailySet(QuestContent.Quests, q, 3);
            Assert.That(set[1].Id.Value, Is.EqualTo("daily_transfer_1"));
            Assert.That(set.Count, Is.EqualTo(3));

            // The rerolled-away quest is no longer in the effective set.
            Assert.That(QuestEngine.SlotKeyFor(QuestContent.Quests, q, 3, "daily_win_1"), Is.Null);
        }

        [Test]
        public void RerollsClearOnTheDayRollover()
        {
            PlayerQuests q = new PlayerQuests();
            QuestEngine.SyncDay(q, Day(2026, 1, 1));
            q.Rerolled["daily_win_1"] = "daily_score_2";
            QuestEngine.SyncDay(q, Day(2026, 1, 2));
            Assert.That(q.Rerolled.Count, Is.EqualTo(0));
        }
    }
}
