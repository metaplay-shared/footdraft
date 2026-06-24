// FOOTDRAFT — Objectives claim-flow tests: completion gating, one-time claim, reward grant. Drives a real
// PlayerModel (so the computed progress + claimed-set persistence are exercised end-to-end).

using System;
using System.Threading.Tasks;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class ObjectiveTests
    {
        const string StaticGameConfigPath = "../Server/GameConfig/StaticGameConfig.mpa";
        static SharedGameConfig _config;

        [OneTimeSetUp]
        public async Task SetUp()
        {
            ConfigArchive archive = await ConfigArchive.FromFileAsync(StaticGameConfigPath);
            ReadOnlyMemory<byte> sharedBytes = archive.GetEntryByName("Shared.mpa").Bytes;
            _config = (SharedGameConfig)GameConfigUtil.ImportSharedConfig(ConfigArchive.FromBytes(sharedBytes));
        }

        static PlayerModel NewPlayer()
        {
            PlayerModel p = PlayerModelUtil.CreateNewPlayerModel<PlayerModel>(
                MetaTime.FromDateTime(new DateTime(2026, 6, 23, 12, 0, 0, DateTimeKind.Utc)),
                _config, EntityId.CreateRandom(EntityKindCore.Player), "Tester");
            p.LogicVersion = IntegrationRegistry.Get<IMetaplayCoreOptionsProvider>().Options.SupportedLogicVersions.MaxVersion;
            p.OnInitialLogin();
            return p;
        }

        static MetaActionResult Run(PlayerModel p, PlayerAction a) => a.InvokeExecute(p, commit: true);

        [Test]
        public void EveryObjectiveHasARewardAndUniqueId()
        {
            System.Collections.Generic.HashSet<string> ids = new System.Collections.Generic.HashSet<string>();
            foreach (ObjectiveDef o in Objectives.All)
            {
                Assert.That(ids.Add(o.Id), Is.True, $"duplicate objective id {o.Id}");
                Assert.That(o.Coins + o.Gems + o.Shards, Is.GreaterThan(0), $"{o.Id} pays nothing");
                Assert.That(o.Target, Is.GreaterThan(0));
            }
        }

        [Test]
        public void ClaimCompletedObjective_GrantsRewardOnce()
        {
            PlayerModel p = NewPlayer();
            p.Progression.MatchesWon = 1; // completes "win1" (reward 150 coins)
            long before = p.Wallet.Get(CurrencyType.Coins);

            Assert.That(Run(p, new PlayerClaimObjective("win1")), Is.EqualTo(MetaActionResult.Success));
            Assert.That(p.Wallet.Get(CurrencyType.Coins), Is.EqualTo(before + 150));
            Assert.That(p.Objectives.IsClaimed("win1"), Is.True);

            // Second claim is rejected (no double-dip).
            Assert.That(Run(p, new PlayerClaimObjective("win1")), Is.EqualTo(PlayerActionResults.ObjectiveAlreadyClaimed));
        }

        [Test]
        public void ClaimIncompleteObjective_Fails()
        {
            PlayerModel p = NewPlayer();
            Assert.That(Run(p, new PlayerClaimObjective("win10")), Is.EqualTo(PlayerActionResults.ObjectiveNotComplete));
        }

        [Test]
        public void ClaimUnknownObjective_Fails()
        {
            PlayerModel p = NewPlayer();
            Assert.That(Run(p, new PlayerClaimObjective("does_not_exist")), Is.EqualTo(PlayerActionResults.UnknownObjective));
        }

        [Test]
        public void ClaimableCountTracksProgress()
        {
            PlayerModel p = NewPlayer();
            Assert.That(Objectives.ClaimableCount(p), Is.EqualTo(0));
            p.Progression.MatchesWon = 1;
            Assert.That(Objectives.ClaimableCount(p), Is.GreaterThanOrEqualTo(1));
            Run(p, new PlayerClaimObjective("win1"));
            // Claiming removes it from the claimable set.
            Assert.That(Objectives.IsClaimable(Objectives.Find("win1"), p), Is.False);
        }
    }
}
