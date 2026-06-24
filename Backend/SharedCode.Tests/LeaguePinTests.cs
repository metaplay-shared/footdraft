// FOOTDRAFT — pin-draft rule parsing tests (the shared encode/decode the client UI + LeagueActor spin agree on).

using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class LeaguePinTests
    {
        [Test]
        public void EmptyMeansNoPin()
        {
            (string era, bool elite) = LeaguePin.Parse("");
            Assert.That(era, Is.Empty);
            Assert.That(elite, Is.False);
            Assert.That(LeaguePin.IsSet(""), Is.False);
            Assert.That(LeaguePin.IsSet(null), Is.False);
        }

        [Test]
        public void ParsesEra()
        {
            (string era, bool elite) = LeaguePin.Parse("era:E2010s");
            Assert.That(era, Is.EqualTo("E2010s"));
            Assert.That(elite, Is.False);
            Assert.That(LeaguePin.IsSet("era:E2010s"), Is.True);
        }

        [Test]
        public void ParsesEliteAndBoth()
        {
            Assert.That(LeaguePin.Parse("elite:1").Elite, Is.True);
            (string era, bool elite) = LeaguePin.Parse("era:E1990s,elite:1");
            Assert.That(era, Is.EqualTo("E1990s"));
            Assert.That(elite, Is.True);
        }

        [Test]
        public void DescribeReadsHumanFriendly()
        {
            Assert.That(LeaguePin.Describe(""), Is.Empty);
            Assert.That(LeaguePin.Describe("era:E2020s"), Does.Contain("20s"));
            string both = LeaguePin.Describe("era:E1990s,elite:1");
            Assert.That(both, Does.Contain("90s"));
            Assert.That(both, Does.Contain("elite"));
        }
    }
}
