// FOOTDRAFT — WS2: the daily league now reads its size / sim time / formation / matchday events from config.

using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class LeagueDefinitionTests
    {
        [Test]
        public void DefaultDefinitionHasExpectedSeasonShape()
        {
            LeagueDefinition def = LeagueDefinitionContent.Default;
            Assert.That(def.Id.Value, Is.EqualTo(LeagueDefinitionContent.DefaultId));
            Assert.That(def.LeagueSize, Is.EqualTo(20), "20 teams → 38 matchdays (the 38-0)");
            Assert.That(def.DailySimHourUtc, Is.EqualTo(19), "the 7pm UTC ritual");
            Assert.That(def.DefaultFormation, Is.EqualTo("4-3-3"));
        }

        [Test]
        public void DefaultLibraryResolvesTheDefaultDefinition()
        {
            var library = LeagueDefinitionContent.CreateLibrary();
            Assert.That(library.TryGetValue(LeagueDefinitionId.FromString(LeagueDefinitionContent.DefaultId), out LeagueDefinition def), Is.True);
            Assert.That(def.LeagueSize, Is.EqualTo(20));
        }

        [Test]
        public void TransferWindowIsAlwaysOpenByDefault()
        {
            // The default 0:00 + 24h window keeps the transfer market open between every matchday —
            // the in-season metagame is always available; ops can shorten it via config / dashboard.
            LeagueDefinition def = LeagueDefinitionContent.Default;
            for (int hour = 0; hour < 24; hour++)
                Assert.That(def.IsTransferWindowHour(hour), Is.True, $"hour {hour}");
        }

        [Test]
        public void ShortTransferWindowWrapsMidnight()
        {
            // The window hours are designer-editable; a 23:00 + 2h window must stay open past midnight.
            LeagueDefinition def = LeagueDefinitionContent.Default;
            typeof(LeagueDefinition).GetProperty(nameof(LeagueDefinition.TransferWindowStartHourUtc))!.SetValue(def, 23);
            typeof(LeagueDefinition).GetProperty(nameof(LeagueDefinition.TransferWindowDurationHours))!.SetValue(def, 2);
            try
            {
                Assert.That(def.IsTransferWindowHour(23), Is.True);
                Assert.That(def.IsTransferWindowHour(0), Is.True, "wraps across midnight");
                Assert.That(def.IsTransferWindowHour(1), Is.False);
                Assert.That(def.IsTransferWindowHour(12), Is.False);
            }
            finally
            {
                // Default is a shared static — restore it.
                typeof(LeagueDefinition).GetProperty(nameof(LeagueDefinition.TransferWindowStartHourUtc))!.SetValue(def, 0);
                typeof(LeagueDefinition).GetProperty(nameof(LeagueDefinition.TransferWindowDurationHours))!.SetValue(def, 24);
            }
        }

        [Test]
        public void EventForMatchdayMatchesByNumber()
        {
            LeagueDefinition def = LeagueDefinitionContent.Default;

            MatchdayEvent goalRush = def.EventForMatchday(19);
            Assert.That(goalRush, Is.Not.Null);
            Assert.That(goalRush.GoalBonus, Is.EqualTo(1), "mid-season goal rush adds a goal to both sides");

            Assert.That(def.EventForMatchday(2), Is.Null, "no event scripted for matchday 2");
            Assert.That(def.EventForMatchday(1), Is.Not.Null, "opening day is scripted");
        }
    }
}
