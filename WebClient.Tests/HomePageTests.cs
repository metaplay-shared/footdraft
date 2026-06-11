using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace WebClient.Tests;

/// <summary>
/// E2E for the 38-0-20 menu: the app loads, connects, and shows the minimal two-button menu
/// (Start a League / Join with a Code). The full league flow is covered by <see cref="LeagueFlowTests"/>.
/// Requires the server (metaplay dev server) and the Blazor WASM client (on <see cref="BaseUrl"/>) running.
/// (The inherited 1v1 match + solo spin-draft were removed from the menu in the league-only redesign; those
/// components remain in the codebase but are no longer reachable, so they're not E2E-tested here.)
/// </summary>
[TestFixture]
public class HomePageTests : PageTest
{
    private const string BaseUrl = "http://localhost:5290";

    [Test]
    public async Task Menu_LoadsAndConnects()
    {
        await Page.GotoAsync(BaseUrl, new() { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "START A LEAGUE" }))
            .ToBeVisibleAsync(new() { Timeout = 60000 });
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "JOIN WITH A CODE" })).ToBeVisibleAsync();
    }
}
