// FOOTDRAFT — wires the "Footdraft Game Config" Google Sheet as a game-config build source.
//
// With this, the LiveOps Dashboard "Game Configs" page can build config from the sheet and publish it — so a
// designer edits the sheet, hits Build → Publish, and the change goes live with no redeploy.
//
// Two fetch paths, picked automatically:
//  - Real Google credentials configured (`GoogleSheets:CredentialsJson` looks like a JSON key) → the SDK's
//    Google Sheets API fetcher (private sheets, tab discovery, metadata).
//  - No (or non-JSON) credentials → CREDENTIAL-LESS public-CSV fetch: the sheet must be link-shared
//    ("Anyone with the link: Viewer") and each [GameConfigEntry] tab is fetched as CSV via the public
//    gviz endpoint. Game config is shipped to clients anyway, so a link-readable sheet leaks nothing new.
//
// A config library is pulled from the sheet when its [GameConfigEntry] drops `isCodeOnly` (so it becomes an
// archive entry); the sheet must then have a tab whose name matches the entry, with an `Id #key` column.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Metaplay.Core.Config;

namespace Game.Logic
{
    public class MyGameConfigBuildIntegration : GameConfigBuildIntegration
    {
        /// <summary> The "Footdraft Game Config" spreadsheet (Drive id). Tabs map to [GameConfigEntry] names. </summary>
        public const string SpreadsheetId = "1r0fpYtDIbFii_9R6ggWFBbiEH-HoDhZK35WQEByto2E";

        public override IEnumerable<GameConfigBuildSource> GetAvailableGameConfigBuildSources(string sourcePropertyInBuildParams)
        {
            return new GameConfigBuildSource[]
            {
                new GoogleSheetBuildSource("Footdraft Game Config", SpreadsheetId),
            };
        }

        public override IGameConfigSourceFetcherProvider MakeSourceFetcherProvider(IGameConfigSourceFetcherConfig config)
        {
            return new PublicCsvFallbackFetcherProvider((GameConfigSourceFetcherConfigCore)config);
        }
    }

    /// <summary>
    /// The default fetcher provider, except a <see cref="GoogleSheetBuildSource"/> with no real Google
    /// credentials configured falls back to the credential-less public-CSV fetcher.
    /// </summary>
    public class PublicCsvFallbackFetcherProvider : DefaultGameConfigSourceFetcherProvider
    {
        public PublicCsvFallbackFetcherProvider(GameConfigSourceFetcherConfigCore config) : base(config) { }

        protected override Task<IGameConfigSourceFetcher> GetGoogleSheetFetcherAsync(GoogleSheetBuildSource source, CancellationToken ct)
        {
            if (HasRealGoogleCredentials(Config))
                return base.GetGoogleSheetFetcherAsync(source, ct);
            return Task.FromResult<IGameConfigSourceFetcher>(new PublicGoogleSheetCsvFetcher(source));
        }

        static bool HasRealGoogleCredentials(GameConfigSourceFetcherConfigCore config)
        {
            if (config == null)
                return false;
            if (!string.IsNullOrEmpty(config.GoogleCredentialsFilePath) || config.GoogleCredentialsClientSecret.HasValue)
                return true;
            // CredentialsJson must look like an actual JSON key (a sentinel like "public-csv" selects this fallback).
            return !string.IsNullOrEmpty(config.GoogleCredentialsJson) && config.GoogleCredentialsJson.TrimStart().StartsWith("{", StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Fetches tabs of a LINK-SHARED Google Sheet as CSV over the public gviz endpoint — no Google API
    /// credentials needed. Each requested item name must be a tab name in the sheet.
    /// </summary>
    public class PublicGoogleSheetCsvFetcher : IGameConfigSourceFetcher
    {
        static readonly HttpClient HttpClient = new HttpClient();

        readonly GoogleSheetBuildSource _source;

        public PublicGoogleSheetCsvFetcher(GoogleSheetBuildSource source)
        {
            _source = source;
        }

        class CsvTab : IGameConfigSourceData
        {
            readonly GoogleSheetBuildSource _source;
            readonly string                 _tabName;

            public CsvTab(GoogleSheetBuildSource source, string tabName)
            {
                _source  = source;
                _tabName = tabName;
            }

            public async Task<object> Get(CancellationToken ct)
            {
                string url = $"https://docs.google.com/spreadsheets/d/{_source.SpreadsheetId}/gviz/tq?tqx=out:csv&sheet={Uri.EscapeDataString(_tabName)}";
                using HttpResponseMessage response = await HttpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"Public CSV fetch of sheet '{_source.SpreadsheetId}' tab '{_tabName}' failed with HTTP {(int)response.StatusCode}. " +
                        "Make sure the spreadsheet is link-shared (Share → Anyone with the link: Viewer), " +
                        $"and that a tab named '{_tabName}' exists. (Alternatively configure GoogleSheets:CredentialsJson with a real service-account key.)");
                }

                byte[] bytes = await response.Content.ReadAsByteArrayAsync(ct);

                // A login/error page instead of CSV means the sheet isn't actually link-readable (Google
                // serves HTML with a 200 in some of these flows).
                if (bytes.Length > 0 && (bytes[0] == (byte)'<' || (response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) ?? false)))
                {
                    throw new InvalidOperationException(
                        $"Public CSV fetch of sheet '{_source.SpreadsheetId}' tab '{_tabName}' returned an HTML page instead of CSV. " +
                        "The spreadsheet is not link-shared: open it in Google Sheets → Share → General access → Anyone with the link: Viewer.");
                }

                return GameConfigHelper.ParseCsvToSpreadsheet($"{_tabName}.csv", bytes);
            }
        }

        public IGameConfigSourceData Fetch(string itemName) => new CsvTab(_source, itemName);

        public Task<GameConfigBuildSourceMetadata> GetMetadataAsync(CancellationToken ct) =>
            Task.FromResult<GameConfigBuildSourceMetadata>(null);
    }
}
