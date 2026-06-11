// FOOTDRAFT — Phase 1 progression/economy unit tests.

using System.Collections.Generic;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    /// <summary>
    /// Unit tests for the Phase 1 meta-game logic: the currency wallet, the manager-level XP curve and level-up
    /// carry/cap, lifetime match stats, the config cost curves, and the squad-card upgrade → die-size mapping
    /// (the "build your dice pool" core). These exercise the pure logic directly, no server or game-config load
    /// required (mirrors the config-free style of <see cref="MatchModelTests"/>).
    /// </summary>
    [TestFixture]
    public class ProgressionTests
    {
        #region Wallet

        [Test]
        public void WalletEarnsAndReportsBalances()
        {
            PlayerWallet wallet = new PlayerWallet();
            Assert.That(wallet.Get(CurrencyType.Coins), Is.EqualTo(0));

            wallet.Earn(CurrencyType.Coins, 100);
            wallet.Earn(CurrencyType.Coins, 50);
            Assert.That(wallet.Get(CurrencyType.Coins), Is.EqualTo(150));
            Assert.That(wallet.Get(CurrencyType.Gems), Is.EqualTo(0));
        }

        [Test]
        public void WalletEarnIgnoresNonPositiveAmounts()
        {
            PlayerWallet wallet = new PlayerWallet();
            wallet.Earn(CurrencyType.Gems, 0);
            wallet.Earn(CurrencyType.Gems, -10);
            Assert.That(wallet.Get(CurrencyType.Gems), Is.EqualTo(0));
        }

        [Test]
        public void WalletSpendSucceedsOnlyWhenAffordable()
        {
            PlayerWallet wallet = new PlayerWallet();
            wallet.Earn(CurrencyType.Coins, 100);

            Assert.That(wallet.CanAfford(CurrencyType.Coins, 100), Is.True);
            Assert.That(wallet.CanAfford(CurrencyType.Coins, 101), Is.False);

            Assert.That(wallet.TrySpend(CurrencyType.Coins, 40), Is.True);
            Assert.That(wallet.Get(CurrencyType.Coins), Is.EqualTo(60));

            Assert.That(wallet.TrySpend(CurrencyType.Coins, 61), Is.False, "must not overspend");
            Assert.That(wallet.Get(CurrencyType.Coins), Is.EqualTo(60), "failed spend leaves balance untouched");
        }

        #endregion

        #region Config cost curves

        [Test]
        public void XpAndUpgradeCurvesIncreaseWithLevel()
        {
            GlobalConfig g = new GlobalConfig();

            // XP to next level grows linearly: Base + (level-1)*Step.
            Assert.That(g.XpToReachNextLevel(1), Is.EqualTo(g.BaseXpPerLevel));
            Assert.That(g.XpToReachNextLevel(2), Is.EqualTo(g.BaseXpPerLevel + g.XpPerLevelStep));
            Assert.That(g.XpToReachNextLevel(3), Is.GreaterThan(g.XpToReachNextLevel(2)));

            // Upgrade costs grow with the card's current level.
            Assert.That(g.CardUpgradeCoinCost(0), Is.EqualTo(g.CardUpgradeBaseCoins));
            Assert.That(g.CardUpgradeCoinCost(1), Is.GreaterThan(g.CardUpgradeCoinCost(0)));
            Assert.That(g.CardUpgradeShardCost(1), Is.GreaterThan(g.CardUpgradeShardCost(0)));
        }

        #endregion

        #region Squad building (upgrade → die size)

        static TeamInfo TestTeam() => new TeamInfo("TST", "Testers", "\U0001F3F3", new List<PlayerEntry>
        {
            new PlayerEntry("Low",  76), // below the d8 threshold (78) → d6
            new PlayerEntry("Mid",  83), // ≥78 → d8
            new PlayerEntry("High", 90), // ≥85 → d10
            new PlayerEntry("Sub1", 70),
            new PlayerEntry("Sub2", 70),
        });

        [Test]
        public void BaseSquadMapsRatingToDieSize()
        {
            SquadSpec squad = SquadBuilder.BuildBase(TestTeam());
            Assert.That(squad.Players[0].Sides, Is.EqualTo(6),  "76 → d6");
            Assert.That(squad.Players[1].Sides, Is.EqualTo(8),  "83 → d8");
            Assert.That(squad.Players[2].Sides, Is.EqualTo(10), "90 → d10");
        }

        [Test]
        public void UpgradingACardBumpsItsDieAcrossAThreshold()
        {
            GlobalConfig g    = new GlobalConfig();
            TeamInfo     team = TestTeam();

            // One upgrade adds RatingPerUpgradeLevel (2): 76 → 78 crosses the d8 threshold.
            SquadBook book = new SquadBook();
            book.SetUpgradeLevel(CardKeys.For("TST", 0), 1);
            SquadSpec squad = SquadBuilder.Build(team, book, g);
            Assert.That(squad.Players[0].Sides, Is.EqualTo(8), "76 + 2 = 78 → d8");

            // 83 → 85 crosses the d10 threshold.
            SquadBook book2 = new SquadBook();
            book2.SetUpgradeLevel(CardKeys.For("TST", 1), 1);
            SquadSpec squad2 = SquadBuilder.Build(team, book2, g);
            Assert.That(squad2.Players[1].Sides, Is.EqualTo(10), "83 + 2 = 85 → d10");
        }

        [Test]
        public void UnUpgradedCardsKeepBaseDie()
        {
            GlobalConfig g    = new GlobalConfig();
            TeamInfo     team = TestTeam();
            SquadSpec    full = SquadBuilder.Build(team, new SquadBook(), g);
            SquadSpec    bas  = SquadBuilder.BuildBase(team);
            for (int slot = 0; slot < full.Players.Count; slot++)
                Assert.That(full.Players[slot].Sides, Is.EqualTo(bas.Players[slot].Sides));
        }

        #endregion

        #region Manager XP & level-up

        static PlayerModel ManagerAtLevel(int level)
        {
            PlayerModel model = new PlayerModel();
            model.PlayerLevel = level;
            return model;
        }

        [Test]
        public void GrantXpAdvancesOneLevelAndCarriesRemainder()
        {
            GlobalConfig g     = new GlobalConfig();
            PlayerModel  model = ManagerAtLevel(1);

            model.GrantXp(g.XpToReachNextLevel(1) + 20, g); // enough for one level, +20 spare
            Assert.That(model.PlayerLevel, Is.EqualTo(2));
            Assert.That(model.Progression.Xp, Is.EqualTo(20));
        }

        [Test]
        public void GrantXpCanCrossMultipleLevelsAtOnce()
        {
            GlobalConfig g     = new GlobalConfig();
            PlayerModel  model = ManagerAtLevel(1);

            long bigGrant = g.XpToReachNextLevel(1) + g.XpToReachNextLevel(2) + 10;
            model.GrantXp(bigGrant, g);
            Assert.That(model.PlayerLevel, Is.EqualTo(3));
            Assert.That(model.Progression.Xp, Is.EqualTo(10));
        }

        [Test]
        public void GrantXpCapsAtMaxManagerLevel()
        {
            GlobalConfig g     = new GlobalConfig();
            PlayerModel  model = ManagerAtLevel(1);

            model.GrantXp(long.MaxValue / 2, g);
            Assert.That(model.PlayerLevel, Is.EqualTo(g.MaxManagerLevel));
            Assert.That(model.Progression.Xp, Is.EqualTo(0), "XP no longer accrues at max level");
        }

        #endregion

        #region Match stats

        [Test]
        public void RecordMatchResultTracksStreaks()
        {
            PlayerModel model = ManagerAtLevel(1);

            model.RecordMatchResult(won: true);
            model.RecordMatchResult(won: true);
            Assert.That(model.Progression.MatchesPlayed, Is.EqualTo(2));
            Assert.That(model.Progression.MatchesWon, Is.EqualTo(2));
            Assert.That(model.Progression.CurrentWinStreak, Is.EqualTo(2));
            Assert.That(model.Progression.BestWinStreak, Is.EqualTo(2));

            model.RecordMatchResult(won: false);
            Assert.That(model.Progression.CurrentWinStreak, Is.EqualTo(0), "loss resets the current streak");
            Assert.That(model.Progression.BestWinStreak, Is.EqualTo(2), "best streak is retained");
            Assert.That(model.Progression.MatchesPlayed, Is.EqualTo(3));
        }

        #endregion
    }
}
