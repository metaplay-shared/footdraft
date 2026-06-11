// FOOTDRAFT — Phase 4 clubs unit tests.

using Metaplay.Core;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    /// <summary>
    /// Unit tests for the Phase 4 club logic: club-name validation and the weekly Club League window rollover.
    /// </summary>
    [TestFixture]
    public class ClubTests
    {
        static MetaTime At(long ms) => MetaTime.FromMillisecondsSinceEpoch(ms);

        [Test]
        public void ClubNameValidation()
        {
            Assert.That(ClubName.IsValid("FC Dice"), Is.True);
            Assert.That(ClubName.IsValid("Goal Diggers 99"), Is.True);
            Assert.That(ClubName.IsValid("AB"), Is.True);
            Assert.That(ClubName.IsValid("A"), Is.False, "too short");
            Assert.That(ClubName.IsValid(new string('x', 21)), Is.False, "too long");
            Assert.That(ClubName.IsValid("bad@name"), Is.False, "invalid char");
            Assert.That(ClubName.IsValid("   "), Is.False);
            Assert.That(ClubName.IsValid(null), Is.False);
        }

        [Test]
        public void ClubLeagueWeekRollsOver()
        {
            GlobalConfig g = new GlobalConfig();
            long windowMs = (long)g.ClubLeagueWindowDays * 24L * 60L * 60L * 1000L;

            long w0 = ClubWeek.CurrentWeekId(At(windowMs * 3 + 100), g);
            long w1 = ClubWeek.CurrentWeekId(At(windowMs * 4 + 100), g);
            Assert.That(w1, Is.EqualTo(w0 + 1));
            Assert.That(ClubWeek.WeekEndsAt(w0, g).MillisecondsSinceEpoch, Is.EqualTo(windowMs * 4));
        }
    }
}
