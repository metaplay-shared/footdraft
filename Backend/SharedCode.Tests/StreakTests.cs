// FOOTDRAFT — WS4: daily-login streak logic + the streak reward curve.

using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class StreakTests
    {
        [Test]
        public void FirstLoginStartsStreakAtOne()
        {
            LoginStreak s = new LoginStreak();
            Assert.That(LoginStreakEngine.Advance(s, 20000), Is.True);
            Assert.That(s.CurrentStreak, Is.EqualTo(1));
            Assert.That(s.BestStreak, Is.EqualTo(1));
        }

        [Test]
        public void SameDayLoginDoesNotDoubleCount()
        {
            LoginStreak s = new LoginStreak();
            LoginStreakEngine.Advance(s, 20000);
            Assert.That(LoginStreakEngine.Advance(s, 20000), Is.False, "no reward twice in a day");
            Assert.That(s.CurrentStreak, Is.EqualTo(1));
        }

        [Test]
        public void ConsecutiveDaysIncrementStreak()
        {
            LoginStreak s = new LoginStreak();
            LoginStreakEngine.Advance(s, 20000);
            LoginStreakEngine.Advance(s, 20001);
            Assert.That(s.CurrentStreak, Is.EqualTo(2));
        }

        [Test]
        public void OneMissedDayIsForgiven()
        {
            LoginStreak s = new LoginStreak();
            LoginStreakEngine.Advance(s, 20000);
            LoginStreakEngine.Advance(s, 20001);
            LoginStreakEngine.Advance(s, 20003); // skipped day 20002 — forgiven
            Assert.That(s.CurrentStreak, Is.EqualTo(3));
        }

        [Test]
        public void TwoMissedDaysResetStreak()
        {
            LoginStreak s = new LoginStreak();
            LoginStreakEngine.Advance(s, 20000);
            LoginStreakEngine.Advance(s, 20001);
            LoginStreakEngine.Advance(s, 20004); // skipped 2 days — streak breaks
            Assert.That(s.CurrentStreak, Is.EqualTo(1));
            Assert.That(s.BestStreak, Is.EqualTo(2), "best is retained");
        }

        [Test]
        public void ForgivenessIsSpentOncePerStreak()
        {
            LoginStreak s = new LoginStreak();
            LoginStreakEngine.Advance(s, 20000);
            LoginStreakEngine.Advance(s, 20002); // first gap — forgiven
            Assert.That(s.CurrentStreak, Is.EqualTo(2));
            LoginStreakEngine.Advance(s, 20004); // second gap in the same streak — NOT forgiven
            Assert.That(s.CurrentStreak, Is.EqualTo(1), "every-other-day must not sustain a streak forever");
        }

        [Test]
        public void ForgivenessResetsWithANewStreak()
        {
            LoginStreak s = new LoginStreak();
            LoginStreakEngine.Advance(s, 20000);
            LoginStreakEngine.Advance(s, 20002); // forgiveness spent
            LoginStreakEngine.Advance(s, 20010); // long gap — streak resets, forgiveness restored
            Assert.That(s.CurrentStreak, Is.EqualTo(1));
            LoginStreakEngine.Advance(s, 20012); // gap of one again — forgiven on the new streak
            Assert.That(s.CurrentStreak, Is.EqualTo(2));
        }

        [Test]
        public void ClockSkewBackwardsNeverCounts()
        {
            LoginStreak s = new LoginStreak();
            LoginStreakEngine.Advance(s, 20005);
            Assert.That(LoginStreakEngine.Advance(s, 20004), Is.False);
            Assert.That(s.CurrentStreak, Is.EqualTo(1));
        }

        [Test]
        public void RewardGrowsWithStreakAndCaps()
        {
            GlobalConfig g = new GlobalConfig(); // base 50, +15/day, cap 7 bonus days
            Assert.That(g.DailyStreakReward(1), Is.EqualTo(50));
            Assert.That(g.DailyStreakReward(2), Is.EqualTo(65));
            Assert.That(g.DailyStreakReward(8), Is.EqualTo(50 + 7 * 15));
            Assert.That(g.DailyStreakReward(50), Is.EqualTo(50 + 7 * 15), "capped at max bonus days");
        }
    }
}
