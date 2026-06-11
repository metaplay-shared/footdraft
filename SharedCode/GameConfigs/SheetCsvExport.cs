// FOOTDRAFT — exports the code-defined game config content to per-tab CSVs in Metaplay sheet format.
//
// Shared by tools/sheet-export (writes the files a designer imports into the "Footdraft Game Config" Google
// Sheet) and by SheetConfigBuildTests (which round-trips the same CSVs through the real config-build pipeline,
// proving the sheet format parses BEFORE anyone imports it into Google Sheets).
//
// Tab names match the sheet-backed [GameConfigEntry] names: Legends, Formations, LeagueDefinitions, Quests.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Game.Logic
{
    public static class SheetCsvExport
    {
        /// <summary> How many MatchdayEvents column-groups the LeagueDefinitions tab carries (spares let designers add events). </summary>
        public const int MaxMatchdayEventColumns = 6;

        /// <summary> Writes Legends/Formations/LeagueDefinitions/Quests CSVs to <paramref name="outDir"/>. Returns per-tab row counts. </summary>
        public static Dictionary<string, int> ExportAll(string outDir)
        {
            Directory.CreateDirectory(outDir);
            Dictionary<string, int> counts = new Dictionary<string, int>();

            counts["Legends"] = WriteCsv(outDir, "Legends",
                new[] { "Id #key", "Name", "Position", "Ovr", "Nation", "Club", "Era", "Season" },
                LegendContent.Legends.Select(p => new string[]
                {
                    p.Id.Value, p.Name, p.Position.ToString(), p.Ovr.ToString(), p.Nation, p.Club, p.Era.ToString(), p.Season,
                }));

            counts["Formations"] = WriteCsv(outDir, "Formations",
                new[] { "Id #key", "DisplayName" }.Concat(Enumerable.Range(0, 11).Select(i => $"Slots[{i}]")).ToArray(),
                FormationContent.Formations.Select(f =>
                    new[] { f.Id.Value, f.DisplayName }.Concat(f.Slots.Select(s => s.ToString())).ToArray()));

            List<string> defHeader = new List<string>
            {
                "Id #key", "DisplayName", "LeagueSize", "DailySimHourUtc", "DefaultFormation",
                "TransferBudget", "TransferSwapCost", "TransferWindowStartHourUtc", "TransferWindowDurationHours",
                // Wallet-coin transfer economy + gem sinks (TransferBudget/TransferSwapCost above are deprecated but
                // kept so the published sheet's columns keep round-tripping).
                "TransferBaseCost", "TransferCostPerOvr", "TransferOvrPivot",
                "MarqueeMinOvr", "MarqueeGemBase", "MarqueeGemPerOvr",
                "EliteSpinGemCost", "EliteSpinMinAvgOvr",
            };
            for (int i = 0; i < MaxMatchdayEventColumns; i++)
                defHeader.AddRange(new[] { $"MatchdayEvents[{i}].Matchday", $"MatchdayEvents[{i}].Name", $"MatchdayEvents[{i}].GoalBonus" });
            counts["LeagueDefinitions"] = WriteCsv(outDir, "LeagueDefinitions", defHeader.ToArray(),
                new[] { LeagueDefinitionContent.Default }.Select(d =>
                {
                    List<string> row = new List<string>
                    {
                        d.Id.Value, d.DisplayName, d.LeagueSize.ToString(), d.DailySimHourUtc.ToString(), d.DefaultFormation,
                        d.TransferBudget.ToString(), d.TransferSwapCost.ToString(), d.TransferWindowStartHourUtc.ToString(), d.TransferWindowDurationHours.ToString(),
                        d.TransferBaseCost.ToString(), d.TransferCostPerOvr.ToString(), d.TransferOvrPivot.ToString(),
                        d.MarqueeMinOvr.ToString(), d.MarqueeGemBase.ToString(), d.MarqueeGemPerOvr.ToString(),
                        d.EliteSpinGemCost.ToString(), d.EliteSpinMinAvgOvr.ToString(),
                    };
                    for (int i = 0; i < MaxMatchdayEventColumns; i++)
                    {
                        MatchdayEvent e = i < d.MatchdayEvents.Count ? d.MatchdayEvents[i] : null;
                        row.AddRange(new[] { e?.Matchday.ToString() ?? "", e?.Name ?? "", e != null ? e.GoalBonus.ToString() : "" });
                    }
                    return row.ToArray();
                }));

            counts["Quests"] = WriteCsv(outDir, "Quests",
                new[] { "Id #key", "Description", "Metric", "Target", "RewardCoins", "Scope", "RewardGems" },
                QuestContent.Quests.Select(q => new[]
                {
                    q.Id.Value, q.Description, q.Metric.ToString(), q.Target.ToString(), q.RewardCoins.ToString(),
                    q.Scope.ToString(), q.RewardGems.ToString(),
                }));

            return counts;
        }

        static int WriteCsv(string outDir, string tabName, string[] header, IEnumerable<string[]> rows)
        {
            string path = Path.Combine(outDir, tabName + ".csv");
            using StreamWriter writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.WriteLine(string.Join(",", header.Select(Escape)));
            int count = 0;
            foreach (string[] row in rows)
            {
                writer.WriteLine(string.Join(",", row.Select(Escape)));
                count++;
            }
            return count;
        }

        static string Escape(string value)
        {
            if (value == null)
                return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }
    }
}
