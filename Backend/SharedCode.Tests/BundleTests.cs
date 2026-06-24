// FOOTDRAFT — Featured Offer bundle tests: buying grants the contents + guaranteed cosmetic, one-time bundles
// can't be re-bought, repeatable ones can. Drives a real PlayerModel.

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
    public class BundleTests
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
        static BundleDef Bundle(string id) { foreach (BundleDef b in _config.Global.Bundles) if (b.Id == id) return b; return null; }

        [Test]
        public void BuyBundle_GrantsCurrencyAndGuaranteedCosmetic()
        {
            PlayerModel p = NewPlayer();
            BundleDef b = Bundle("starter");
            Assert.That(b, Is.Not.Null);
            long coins = p.Wallet.Get(CurrencyType.Coins);
            long gems = p.Wallet.Get(CurrencyType.Gems);

            Assert.That(Run(p, new PlayerBuyBundle("starter")), Is.EqualTo(MetaActionResult.Success));
            Assert.That(p.Wallet.Get(CurrencyType.Coins), Is.EqualTo(coins + b.Coins));
            Assert.That(p.Wallet.Get(CurrencyType.Gems), Is.EqualTo(gems + b.Gems));
            if (!string.IsNullOrEmpty(b.CosmeticId))
                Assert.That(p.Cosmetics.Owns(b.CosmeticId), Is.True, "guaranteed cosmetic granted");
        }

        [Test]
        public void OneTimeBundle_CannotBeRebought()
        {
            PlayerModel p = NewPlayer();
            Assert.That(Run(p, new PlayerBuyBundle("starter")), Is.EqualTo(MetaActionResult.Success));
            Assert.That(p.Store.Owns("starter"), Is.True);
            Assert.That(Run(p, new PlayerBuyBundle("starter")), Is.EqualTo(PlayerActionResults.BundleAlreadyOwned));
        }

        [Test]
        public void RepeatableBundle_CanBeBoughtAgain()
        {
            PlayerModel p = NewPlayer();
            Assert.That(Run(p, new PlayerBuyBundle("gold_value")), Is.EqualTo(MetaActionResult.Success));
            Assert.That(Run(p, new PlayerBuyBundle("gold_value")), Is.EqualTo(MetaActionResult.Success), "repeatable bundle re-buys");
            Assert.That(p.Store.Owns("gold_value"), Is.False, "repeatable bundles aren't recorded as owned");
        }

        [Test]
        public void UnknownBundle_Fails()
        {
            PlayerModel p = NewPlayer();
            Assert.That(Run(p, new PlayerBuyBundle("nope")), Is.EqualTo(PlayerActionResults.UnknownProduct));
        }
    }
}
