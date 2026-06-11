// FOOTDRAFT — thin CLI over Game.Logic.SheetCsvExport: dumps the code-defined game config to per-tab CSVs
// for import into the "Footdraft Game Config" Google Sheet (File → Import the .xlsx built by make_workbook.py).

using System;
using System.Collections.Generic;
using Game.Logic;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: dotnet run --project tools/sheet-export -- <output-dir>");
            return 1;
        }

        foreach (KeyValuePair<string, int> tab in SheetCsvExport.ExportAll(args[0]))
            Console.WriteLine($"{tab.Key}: {tab.Value} rows");
        Console.WriteLine($"Wrote CSVs to {args[0]}");
        return 0;
    }
}
