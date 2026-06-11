// FOOTDRAFT — Phase 2 matchmaking unit tests.

using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    /// <summary>
    /// Unit tests for the Phase 2 matchmaking logic: private-room code validation and the level-scaled bot
    /// opponent selection (weaker bots early, ramping to an even match).
    /// </summary>
    [TestFixture]
    public class MatchmakingTests
    {
        [Test]
        public void FriendlyCodeValidation()
        {
            Assert.That(FriendlyCode.IsValid("ABCD"), Is.True);
            Assert.That(FriendlyCode.IsValid("AB12CD"), Is.True);
            Assert.That(FriendlyCode.IsValid("abc"), Is.False, "lowercase + too short");
            Assert.That(FriendlyCode.IsValid("AB"), Is.False, "too short");
            Assert.That(FriendlyCode.IsValid("ABCDEFGHI"), Is.False, "too long");
            Assert.That(FriendlyCode.IsValid("AB-CD"), Is.False, "bad character");
            Assert.That(FriendlyCode.IsValid(""), Is.False);
            Assert.That(FriendlyCode.IsValid(null), Is.False);
        }

        [Test]
        public void BotDifficultyRampsWithLevel()
        {
            GlobalConfig g = new GlobalConfig();
            Assert.That(g.BotDifficultyPct(1), Is.EqualTo(g.BotBaseDifficultyPct));
            Assert.That(g.BotDifficultyPct(g.BotFullDifficultyLevel), Is.EqualTo(100));
            Assert.That(g.BotDifficultyPct(g.BotFullDifficultyLevel + 5), Is.EqualTo(100));
            Assert.That(g.BotDifficultyPct(4), Is.GreaterThanOrEqualTo(g.BotDifficultyPct(1)));
            Assert.That(g.BotDifficultyPct(4), Is.LessThanOrEqualTo(g.BotDifficultyPct(8)));
        }

        [Test]
        public void HigherLevelFacesAtLeastAsStrongABot()
        {
            GlobalConfig g = new GlobalConfig();
            IReadOnlyList<TeamInfo> teams = TeamContent.Teams;
            int humanTotal = 50; // a strong (5×d10) squad

            TeamInfo low  = SquadBuilder.PickScaledBotTeam(teams, humanTotal, 1, g);
            TeamInfo high = SquadBuilder.PickScaledBotTeam(teams, humanTotal, g.BotFullDifficultyLevel, g);

            Assert.That(SquadBuilder.TeamBaseTotalSides(high),
                Is.GreaterThanOrEqualTo(SquadBuilder.TeamBaseTotalSides(low)),
                "a higher-level manager should not face a weaker bot than a level-1 manager");
        }
    }
}
