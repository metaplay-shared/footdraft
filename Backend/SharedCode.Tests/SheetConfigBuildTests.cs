// FOOTDRAFT — proves the "Footdraft Game Config" sheet format round-trips through the REAL config-build
// pipeline: export the code-defined content to per-tab CSVs (exactly what gets imported into the Google
// Sheet), build a config archive from them via FileSystemBuildSource, import it back, and compare against
// the code-defined content. If this passes, a dashboard build from the (identically-shaped) Google Sheet
// parses too — the sheet is just a different transport for the same cells.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Metaplay.Core;
using Metaplay.Core.Config;
using NUnit.Framework;

namespace Game.Logic.Tests
{
    [TestFixture]
    public class SheetConfigBuildTests
    {
        [Test]
        public async Task ExportedCsvsBuildAndImportThroughTheRealPipeline()
        {
            string dir = Path.Combine(Path.GetTempPath(), "footdraft-sheet-roundtrip-" + Guid.NewGuid().ToString("N"));
            try
            {
                SheetCsvExport.ExportAll(dir);

                DefaultGameConfigBuildParameters buildParams = new DefaultGameConfigBuildParameters
                {
                    DefaultSource = new FileSystemBuildSource(FileSystemBuildSource.Format.Csv),
                };
                IGameConfigSourceFetcherConfig fetcherConfig = GameConfigSourceFetcherConfigCore.Create().WithLocalFileSourcesPath(dir);

                ConfigArchive archive;
                try
                {
                    archive = await StaticFullGameConfigBuilder.BuildArchiveAsync(MetaTime.Now, MetaGuid.None, parent: null, buildParams, fetcherConfig);
                }
                catch (GameConfigBuildFailed failed)
                {
                    Assert.Fail("Config build from exported CSVs failed:\n" +
                        string.Join("\n", failed.BuildReport.BuildMessages.Select(m => m.ToString())));
                    throw;
                }
                FullGameConfig full = FullGameConfig.CreateSoloUnpatched(archive);
                SharedGameConfig shared = (SharedGameConfig)full.SharedConfig;

                // Legends: the entire squads corpus survives the round trip.
                Assert.That(shared.Legends.Count, Is.EqualTo(LegendContent.Legends.Count), "every exported legend parses back");
                LegendPlayer codeLegend = LegendContent.Legends[0];
                LegendPlayer sheetLegend = shared.Legends[codeLegend.Id];
                Assert.That(sheetLegend.Name, Is.EqualTo(codeLegend.Name));
                Assert.That(sheetLegend.Position, Is.EqualTo(codeLegend.Position));
                Assert.That(sheetLegend.Ovr, Is.EqualTo(codeLegend.Ovr));
                Assert.That(sheetLegend.Club, Is.EqualTo(codeLegend.Club));
                Assert.That(sheetLegend.Season, Is.EqualTo(codeLegend.Season));

                // Formations: slot lists intact (11 slots, GK first).
                Assert.That(shared.Formations.Count, Is.EqualTo(FormationContent.Formations.Length));
                FormationInfo f433 = shared.Formations[FormationId.FromString("4-3-3")];
                Assert.That(f433.Slots.Count, Is.EqualTo(11));
                Assert.That(f433.Slots[0], Is.EqualTo(Position.GK));
                Assert.That(f433.Slots.Count(s => s == Position.DEF), Is.EqualTo(4));

                // LeagueDefinitions: scalars + the nested matchday-events list parse.
                LeagueDefinition def = shared.LeagueDefinitions[LeagueDefinitionId.FromString(LeagueDefinitionContent.DefaultId)];
                Assert.That(def.LeagueSize, Is.EqualTo(LeagueDefinitionContent.Default.LeagueSize));
                Assert.That(def.DailySimHourUtc, Is.EqualTo(LeagueDefinitionContent.Default.DailySimHourUtc));
                Assert.That(def.TransferBudget, Is.EqualTo(LeagueDefinitionContent.Default.TransferBudget));
                Assert.That(def.MatchdayEvents.Count, Is.EqualTo(LeagueDefinitionContent.Default.MatchdayEvents.Count), "matchday events survive the indexed-column encoding");
                Assert.That(def.EventForMatchday(19), Is.Not.Null);
                Assert.That(def.EventForMatchday(19).GoalBonus, Is.EqualTo(1));

                // Quests.
                Assert.That(shared.Quests.Count, Is.EqualTo(QuestContent.Quests.Length));
                QuestInfo quest = shared.Quests[QuestId.FromString("daily_win_1")];
                Assert.That(quest.Metric, Is.EqualTo(QuestMetric.MatchesWon));
                Assert.That(quest.RewardCoins, Is.EqualTo(150));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
            }
        }
    }
}
