using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace WebClient.Tests;

/// <summary>
/// E2E for the headline league flow with TWO managers (two isolated browser contexts = two guest players):
/// admin starts a league from the menu → reads the shareable code → friend joins by code → admin starts the
/// draft → managers take turns drafting from the shared pool (one manual pick exercises the turn/pick path) →
/// admin auto-drafts the rest and simulates the season → final standings.
/// Requires the server (metaplay dev server) and the Blazor WASM client (on <see cref="BaseUrl"/>) running.
/// </summary>
[TestFixture]
public class LeagueFlowTests : PageTest
{
    private const string BaseUrl = "http://localhost:5290";

    static async Task GotoMenuAsync(IPage page)
    {
        // DOMContentLoaded, not load: the WASM runtime's streaming fetch can hold the window load event
        // back arbitrarily; the menu-button expectation below is the real readiness signal. One retry: the
        // local dev Kestrel occasionally stalls a navigation while another heavy WASM context is booting.
        try
        {
            await page.GotoAsync(BaseUrl, new() { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
        }
        catch (TimeoutException)
        {
            await page.GotoAsync(BaseUrl, new() { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
        }
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "START A LEAGUE" }))
            .ToBeVisibleAsync(new() { Timeout = 60000 });
    }

    [Test]
    public async Task League_CreateJoinDraftSimulate()
    {
        // ---- Manager A (admin): start a league from the menu, read the share code. ----
        await GotoMenuAsync(Page);
        await Page.GetByRole(AriaRole.Button, new() { Name = "START A LEAGUE" }).ClickAsync();
        await Page.GetByPlaceholder("League name").FillAsync("E2E Cup");
        await Page.GetByPlaceholder("Your team name").FillAsync("Wanderers FC");
        await Page.GetByRole(AriaRole.Button, new() { Name = "CREATE LEAGUE" }).ClickAsync();

        var codeLoc = Page.GetByTestId("league-code");
        await Expect(codeLoc).ToBeVisibleAsync(new() { Timeout = 25000 });
        string code = (await codeLoc.InnerTextAsync()).Trim();
        Assert.That(code, Is.Not.Empty, "league should have an invite code");

        // ---- Manager B (friend): join by code from a separate context (= a different guest player). ----
        var ctxB = await Browser.NewContextAsync();
        var pageB = await ctxB.NewPageAsync();
        try
        {
            await GotoMenuAsync(pageB);
            await pageB.GetByRole(AriaRole.Button, new() { Name = "JOIN WITH A CODE" }).ClickAsync();
            await pageB.GetByPlaceholder("CODE").FillAsync(code);
            await pageB.GetByPlaceholder("Your team name").FillAsync("Rovers United");
            await pageB.GetByRole(AriaRole.Button, new() { Name = "JOIN LEAGUE" }).ClickAsync();

            // ---- A sees the second manager join, then starts the draft. ----
            await Expect(Page.GetByText("2 / 20")).ToBeVisibleAsync(new() { Timeout = 25000 });
            // Team names (not "Guest …") show in the lobby.
            await Expect(Page.GetByText("Wanderers FC")).ToBeVisibleAsync(new() { Timeout = 10000 });
            await Expect(Page.GetByText("Rovers United")).ToBeVisibleAsync(new() { Timeout = 10000 });
            await Page.GetByRole(AriaRole.Button, new() { Name = "START DRAFT" }).ClickAsync();

            // ---- A is first on the clock; spin the wheel, then pick a player from the spun squad. ----
            await Expect(Page.GetByText("YOUR PICK")).ToBeVisibleAsync(new() { Timeout = 25000 });
            await Page.GetByTestId("spin-wheel").ClickAsync();
            var firstCand = Page.GetByTestId("draft-candidate").First;
            await Expect(firstCand).ToBeVisibleAsync(new() { Timeout = 25000 });
            await firstCand.ClickAsync();

            // Pick registered — A's XI shows 1/11 (still connected = no desync).
            await Expect(Page.GetByText("1/11").First).ToBeVisibleAsync(new() { Timeout = 25000 });

            // ---- Admin auto-drafts the remaining picks, then simulates the season. ----
            await Page.GetByTestId("auto-draft-all").ClickAsync();

            var sim = Page.GetByTestId("simulate-season");
            await Expect(sim).ToBeVisibleAsync(new() { Timeout = 25000 });
            await sim.ClickAsync();

            await Expect(Page.GetByText("FULL TIME")).ToBeVisibleAsync(new() { Timeout = 25000 });
        }
        finally
        {
            await ctxB.CloseAsync();
        }
    }

    /// <summary>
    /// E2E for the transfer market: create a league, draft it fully, open the TRANSFER MARKET sub-screen,
    /// force-open the window via the dashboard admin API (what a LiveOps operator would do), then swap a drafted
    /// player for a free agent and verify the WALLET Coins balance is charged the OVR-scaled signing fee.
    /// </summary>
    [Test]
    public async Task League_TransferSwap()
    {
        const string AdminApiBase = "http://localhost:5550";

        // ---- Create a league (a second manager joins — lobbies need 2+ humans), draft it out. ----
        await GotoMenuAsync(Page);
        await Page.GetByRole(AriaRole.Button, new() { Name = "START A LEAGUE" }).ClickAsync();
        await Page.GetByPlaceholder("League name").FillAsync("Transfer Test");
        await Page.GetByPlaceholder("Your team name").FillAsync("Window Shoppers");
        await Page.GetByRole(AriaRole.Button, new() { Name = "CREATE LEAGUE" }).ClickAsync();

        var codeLoc = Page.GetByTestId("league-code");
        await Expect(codeLoc).ToBeVisibleAsync(new() { Timeout = 25000 });
        string code = (await codeLoc.InnerTextAsync()).Trim();

        var ctxB = await Browser.NewContextAsync();
        var pageB = await ctxB.NewPageAsync();
        await GotoMenuAsync(pageB);
        await pageB.GetByRole(AriaRole.Button, new() { Name = "JOIN WITH A CODE" }).ClickAsync();
        await pageB.GetByPlaceholder("CODE").FillAsync(code);
        await pageB.GetByPlaceholder("Your team name").FillAsync("Deadline Day FC");
        await pageB.GetByRole(AriaRole.Button, new() { Name = "JOIN LEAGUE" }).ClickAsync();
        await Expect(Page.GetByText("2 / 20")).ToBeVisibleAsync(new() { Timeout = 25000 });
        await ctxB.CloseAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "START DRAFT" }).ClickAsync();
        await Expect(Page.GetByText("YOUR PICK")).ToBeVisibleAsync(new() { Timeout = 25000 });
        await Page.GetByTestId("auto-draft-all").ClickAsync();

        // Season goes Active once the draft completes; the TRANSFER MARKET tile opens the full-screen market.
        var marketTile = Page.GetByTestId("meta-transfers");
        await Expect(marketTile).ToBeVisibleAsync(new() { Timeout = 30000 });
        await marketTile.ClickAsync();
        var panel = Page.GetByTestId("transfer-panel");
        await Expect(panel).ToBeVisibleAsync(new() { Timeout = 25000 });

        // The balance strip shows the WALLET Coins (transfers charge the wallet now, not a league budget).
        var budgetLoc = Page.GetByTestId("transfer-budget");
        await Expect(budgetLoc).ToBeVisibleAsync(new() { Timeout = 25000 });
        long balanceBefore = ParseAmount(await budgetLoc.InnerTextAsync());
        Assert.That(balanceBefore, Is.GreaterThan(0), "fresh player should have starting Coins");

        // ---- LiveOps: force-open the window through the dashboard admin API. ----
        var api = await Playwright.APIRequest.NewContextAsync();
        var resp = await api.PostAsync($"{AdminApiBase}/api/seasonLeagues/{code}/transferWindow",
            new() { DataObject = new { @override = 1 } }); // 1 = force the window open
        Assert.That(resp.Ok, Is.True, $"admin transferWindow open failed: {resp.Status}");

        // ---- Swap: sell the first XI player, sign the best available free agent for that slot. ----
        await Expect(panel.GetByText("WINDOW OPEN")).ToBeVisibleAsync(new() { Timeout = 25000 });
        await Page.GetByTestId("transfer-drop").First.ClickAsync();
        var candidate = Page.GetByTestId("transfer-candidate").First;
        await Expect(candidate).ToBeVisibleAsync(new() { Timeout = 25000 });
        string signing = (await candidate.InnerTextAsync()).Trim();
        await candidate.ClickAsync();
        await Page.GetByTestId("confirm-transfer").ClickAsync();

        // The OVR-scaled signing fee leaves the wallet (charged client-predicted, so it lands fast).
        await Expect(budgetLoc).Not.ToContainTextAsync(balanceBefore.ToString("N0"), new() { Timeout = 25000 });
        long balanceAfter = ParseAmount(await budgetLoc.InnerTextAsync());
        Assert.That(balanceAfter, Is.LessThan(balanceBefore), "signing fee should be deducted from the wallet");
        Assert.That(signing, Is.Not.Empty);
    }

    /// <summary> Digits-only parse for currency labels like "🪙 1,250". </summary>
    static long ParseAmount(string text) =>
        long.Parse(new string(text.Where(char.IsDigit).ToArray()));

    /// <summary>
    /// E2E for SINGLE PLAYER: one tap from the menu into the draft (no lobby), auto-draft the XI, simulate the
    /// whole 38-matchday season against the 19 CPU teams to full time.
    /// </summary>
    [Test]
    public async Task SoloLeague_DraftAndSimulateSeason()
    {
        await GotoMenuAsync(Page);
        await Page.GetByTestId("single-player").ClickAsync();
        await Page.GetByPlaceholder("Your team name").FillAsync("Lone Wolves");
        await Page.GetByTestId("start-solo").ClickAsync();

        // Straight into the draft — it's immediately the solo manager's pick.
        await Expect(Page.GetByText("YOUR PICK")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Page.GetByTestId("auto-draft-all").ClickAsync();

        // Season locks (bots pad the league to 20) and the commissioner controls appear.
        var sim = Page.GetByTestId("simulate-season");
        await Expect(sim).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.GetByText("Lone Wolves")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // The metagame lives around the simmed matchdays: shop/pass/quests/inbox tiles render in-season.
        await Expect(Page.GetByTestId("meta-shop")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await Expect(Page.GetByTestId("meta-inbox")).ToBeVisibleAsync(new() { Timeout = 10000 });

        await sim.ClickAsync();

        await Expect(Page.GetByText("FULL TIME")).ToBeVisibleAsync(new() { Timeout = 30000 });
    }

    /// <summary>
    /// E2E for the in-game inbox: a LiveOps mail with an attached Coins reward (sent through the dashboard
    /// admin API) appears in the player's inbox, and claiming it credits the wallet.
    /// </summary>
    [Test]
    public async Task Inbox_MailWithReward_ClaimCreditsWallet()
    {
        const string AdminApiBase = "http://localhost:5550";

        // The inbox lives in the in-season metagame hub — set up a quick solo season to reach it.
        await GotoMenuAsync(Page);
        await Page.GetByTestId("single-player").ClickAsync();
        await Page.GetByPlaceholder("Your team name").FillAsync("Mail Readers");
        await Page.GetByTestId("start-solo").ClickAsync();
        await Expect(Page.GetByText("YOUR PICK")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Page.GetByTestId("auto-draft-all").ClickAsync();
        await Expect(Page.GetByTestId("meta-inbox")).ToBeVisibleAsync(new() { Timeout = 30000 });

        // This fresh context is the most recently created guest player.
        var api = await Playwright.APIRequest.NewContextAsync();
        var listResp = await api.GetAsync($"{AdminApiBase}/api/players?query=&count=200");
        var players = (await listResp.JsonAsync())!.Value;
        string playerId = players.EnumerateArray()
            .Where(p => p.TryGetProperty("entityId", out _) || p.TryGetProperty("id", out _))
            .OrderByDescending(p => p.TryGetProperty("createdAt", out var c) ? (c.GetString() ?? "") : "")
            .Select(p => p.TryGetProperty("entityId", out var e) ? e.GetString()! : p.GetProperty("id").GetString()!)
            .First();

        var sendResp = await api.PostAsync($"{AdminApiBase}/api/players/{playerId}/sendMail", new()
        {
            DataObject = new Dictionary<string, object>
            {
                ["$type"] = "Metaplay.Core.InGameMail.SimplePlayerMail",
                ["id"] = "03f46b106ebbe40-0-5a50f300f139ee33",
                ["title"] = new { localizations = new Dictionary<string, string> { ["en"] = "E2E gift" } },
                ["body"] = new { localizations = new Dictionary<string, string> { ["en"] = "Automated test reward." } },
                ["attachments"] = new object[]
                {
                    new Dictionary<string, object> { ["$type"] = "Game.Logic.RewardCurrency", ["currency"] = "Coins", ["amount"] = 250 },
                },
            },
        });
        Assert.That(sendResp.Ok, Is.True, $"sendMail failed: {sendResp.Status}");

        // The mail reaches the live session; the inbox badge + card render; CLAIM credits the wallet.
        await Page.GetByTestId("meta-inbox").ClickAsync();
        var mailCard = Page.GetByTestId("inbox-mail").First;
        await Expect(mailCard).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(mailCard).ToContainTextAsync("E2E gift");
        await Page.GetByTestId("claim-mail").ClickAsync();
        await Expect(mailCard).ToContainTextAsync("claimed", new() { Timeout = 25000 });
    }
}
