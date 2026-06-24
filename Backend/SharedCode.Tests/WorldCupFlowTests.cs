// FOOTDRAFT — World Cup + Scout Pack integration tests: drive a real PlayerModel through the actions (entry,
// drafting gate, knockout state machine, honours/collection side-effects, pack opening) against the built
// game config. Complements the pure-logic WorldCupTests/PackTests by exercising the full action wiring.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Metaplay.Core;
using Metaplay.Core.Config;
using Metaplay.Core.Model;
using Metaplay.Core.Player;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class WorldCupFlowTests
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

        // Fill the player's draft with a valid 4-3-3 XI of distinct World Cup players (bypasses the server spin).
        static void FillWcXi(PlayerModel p)
        {
            FormationInfo f = FormationContent.Formations[0]; // 4-3-3
            p.Draft.Formation = f.Id;
            p.Draft.Picks = new MetaDictionary<int, LegendId>();

            Dictionary<Position, List<LegendPlayer>> byPos = new Dictionary<Position, List<LegendPlayer>>();
            foreach (LegendPlayer pl in WorldCupContent.Players)
            {
                if (!byPos.TryGetValue(pl.Position, out List<LegendPlayer> list))
                    byPos[pl.Position] = list = new List<LegendPlayer>();
                list.Add(pl);
            }
            Dictionary<Position, int> used = new Dictionary<Position, int>();
            for (int slot = 0; slot < f.Slots.Count; slot++)
            {
                Position pos = f.Slots[slot];
                int idx = used.TryGetValue(pos, out int u) ? u : 0;
                used[pos] = idx + 1;
                p.Draft.Picks[slot] = byPos[pos][idx].Id;
            }
        }

        [Test]
        public void EnterWorldCup_ChargesEntryAndStartsDrafting()
        {
            PlayerModel p = NewPlayer();
            p.Wallet.Earn(CurrencyType.Coins, 10000);
            long before = p.Wallet.Get(CurrencyType.Coins);

            MetaActionResult r = Run(p, new PlayerEnterWorldCup(premium: false));
            Assert.That(r, Is.EqualTo(MetaActionResult.Success));
            Assert.That(p.WorldCup.State, Is.EqualTo(WorldCupState.Drafting));
            Assert.That(p.Honours.WorldCupRuns, Is.EqualTo(1));
            Assert.That(p.Wallet.Get(CurrencyType.Coins), Is.EqualTo(before - p.GameConfig.Global.WorldCupEntryCoins));
        }

        [Test]
        public void CannotEnterWorldCupWhileDraftCupBusy()
        {
            PlayerModel p = NewPlayer();
            p.Wallet.Earn(CurrencyType.Coins, 10000);
            Run(p, new PlayerEnterDraftCup(false));
            MetaActionResult r = Run(p, new PlayerEnterWorldCup(false));
            Assert.That(r, Is.EqualTo(PlayerActionResults.DraftCupBusy));
        }

        [Test]
        public void PlayBeforeDraftComplete_Fails()
        {
            PlayerModel p = NewPlayer();
            p.Wallet.Earn(CurrencyType.Coins, 10000);
            Run(p, new PlayerEnterWorldCup(false));
            MetaActionResult r = Run(p, new PlayerPlayWorldCupRound());
            Assert.That(r, Is.EqualTo(PlayerActionResults.DraftNotComplete));
        }

        [Test]
        public void FullRun_RecordsCollectionHonoursAndReachesTerminalState()
        {
            PlayerModel p = NewPlayer();
            p.Wallet.Earn(CurrencyType.Coins, 10000);
            Run(p, new PlayerEnterWorldCup(false));
            FillWcXi(p);

            // First play locks the XI (Drafting → Active) and resolves round 0.
            MetaActionResult first = Run(p, new PlayerPlayWorldCupRound());
            Assert.That(first, Is.EqualTo(MetaActionResult.Success));
            Assert.That(p.Collection.UniqueScouted, Is.EqualTo(11), "the locked XI is scouted into My Club");
            Assert.That(p.Honours.BestDraftedXiOvr, Is.GreaterThan(0));
            Assert.That(p.WorldCup.LastOpponent, Is.Not.Empty, "faced a real nation");

            // Play out the run to a terminal state.
            int guard = 0;
            while ((p.WorldCup.State == WorldCupState.Active) && guard++ < 20)
                Run(p, new PlayerPlayWorldCupRound());

            Assert.That(p.WorldCup.State, Is.AnyOf(WorldCupState.Eliminated, WorldCupState.Champion));
            if (p.WorldCup.State == WorldCupState.Champion)
            {
                Assert.That(p.Honours.WorldCupTitles, Is.EqualTo(1));
                Assert.That(p.Honours.WorldCupBestRound, Is.EqualTo(WorldCup.RoundsTotal(p.GameConfig.Global) - 1));
            }
        }

        [Test]
        public void OpenGoldPack_SpendsGemsAndGrantsReward()
        {
            PlayerModel p = NewPlayer();
            p.Wallet.Earn(CurrencyType.Gems, 1000);
            long coinsBefore = p.Wallet.Get(CurrencyType.Coins);

            MetaActionResult r = Run(p, new PlayerOpenPack("gold"));
            Assert.That(r, Is.EqualTo(MetaActionResult.Success));
            Assert.That(p.Packs.OpenedCount, Is.EqualTo(1));
            Assert.That(p.Packs.LastPackId, Is.EqualTo("gold"));
            Assert.That(p.Packs.LastReward, Is.Not.Null);
            Assert.That(p.Wallet.Get(CurrencyType.Coins), Is.GreaterThan(coinsBefore), "gold pack grants coins");
        }

        [Test]
        public void FreeDailyPack_OncePerDay()
        {
            PlayerModel p = NewPlayer();
            Assert.That(Run(p, new PlayerOpenPack("daily")), Is.EqualTo(MetaActionResult.Success));
            Assert.That(Run(p, new PlayerOpenPack("daily")), Is.EqualTo(PlayerActionResults.PackAlreadyClaimedToday));
        }

        [Test]
        public void UnknownPack_Fails()
        {
            PlayerModel p = NewPlayer();
            Assert.That(Run(p, new PlayerOpenPack("nope")), Is.EqualTo(PlayerActionResults.UnknownProduct));
        }
    }
}
