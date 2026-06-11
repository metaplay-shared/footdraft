using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace WebClient.Tests;

/// <summary>
/// Marketing/QA screenshot tour — NOT part of the regular suite ([Explicit]). Drives a solo season to the
/// in-season hub and captures phone-format screenshots of every meta surface (menu, draft + elite spin,
/// hub with HUD, transfer market, quests, season pass, shop) into ~/Desktop/footdraft-blog/screens/.
/// Run with: dotnet test WebClient.Tests --no-build --filter "FullyQualifiedName~ScreenshotTour"
/// Requires the server and WASM client running (same as the E2E suite).
/// </summary>
[TestFixture]
[Explicit("screenshot generator, not a regression test")]
public class ScreenshotTourTests : PageTest
{
    private const string BaseUrl = "http://localhost:5290";
    private static readonly string OutDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Desktop", "footdraft-blog", "screens");

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        // Phone-format portrait (iPhone-ish), 2x for crisp marketing shots.
        ViewportSize = new ViewportSize { Width = 430, Height = 932 },
        DeviceScaleFactor = 2,
    };

    private async Task ShotAsync(string name)
    {
        Directory.CreateDirectory(OutDir);
        // Scroll every meta surface back to its top — scroll positions persist across re-renders of the
        // same container (e.g. the draft view's depth carries into the in-season hub).
        await Page.EvaluateAsync("document.querySelectorAll('.t38-bg').forEach(el => el.scrollTo(0, 0))");
        await Page.WaitForTimeoutAsync(700); // let reveal/badge animations settle
        await Page.ScreenshotAsync(new() { Path = Path.Combine(OutDir, name + ".png") });
        TestContext.Out.WriteLine($"shot: {name}");
    }

    [Test]
    public async Task Tour()
    {
        await Page.GotoAsync(BaseUrl, new() { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.GetByTestId("single-player")).ToBeVisibleAsync(new() { Timeout = 60000 });
        await ShotAsync("01-menu");

        // Solo season → straight into the draft.
        await Page.GetByTestId("single-player").ClickAsync();
        await Page.GetByPlaceholder("Your team name").FillAsync("Screenshot FC");
        await Page.GetByTestId("start-solo").ClickAsync();
        await Expect(Page.GetByText("YOUR PICK")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await ShotAsync("02-draft-spin");

        // One spin so the squad-reveal + pick list show.
        await Page.GetByTestId("spin-wheel").ClickAsync();
        await Expect(Page.GetByTestId("draft-candidate").First).ToBeVisibleAsync(new() { Timeout = 25000 });
        await Page.WaitForTimeoutAsync(1600); // staggered reveal completes
        await ShotAsync("03-draft-reveal");

        // Auto-draft out → in-season hub with the HUD, hero card + tiles.
        await Page.GetByTestId("auto-draft-all").ClickAsync();
        await Expect(Page.GetByTestId("meta-transfers")).ToBeVisibleAsync(new() { Timeout = 30000 });
        await Expect(Page.GetByTestId("hud")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await ShotAsync("04-season-hub");

        // Matchday cinematic: play the first matchday and watch the sim roll in.
        await Page.GetByTestId("play-matchday").ClickAsync();
        await Expect(Page.GetByTestId("match-cinematic")).ToBeVisibleAsync(new() { Timeout = 25000 });
        await Page.WaitForTimeoutAsync(4500); // mid-match: clock running, maybe a goal in
        await ShotAsync("04b-match-cinematic");
        await Page.GetByTestId("match-cinematic").ClickAsync(); // tap to skip → full time
        await Expect(Page.GetByTestId("continue-cinematic")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await ShotAsync("04c-full-time");
        await Page.GetByTestId("continue-cinematic").ClickAsync();
        await Expect(Page.GetByTestId("meta-transfers")).ToBeVisibleAsync(new() { Timeout = 10000 });

        // Transfer market: open, sell the first player, show the priced candidates + confirm sheet.
        await Page.GetByTestId("meta-transfers").ClickAsync();
        await Expect(Page.GetByTestId("transfer-panel")).ToBeVisibleAsync(new() { Timeout = 25000 });
        await Page.GetByTestId("transfer-drop").First.ClickAsync();
        await Expect(Page.GetByTestId("transfer-candidate").First).ToBeVisibleAsync(new() { Timeout = 25000 });
        await ShotAsync("05-transfer-market");
        await Page.GetByTestId("transfer-candidate").First.ClickAsync();
        await Expect(Page.GetByTestId("confirm-transfer").Or(Page.GetByTestId("confirm-transfer-shop"))).ToBeVisibleAsync(new() { Timeout = 10000 });
        await ShotAsync("06-confirm-sheet");
        await Page.GetByText("✕ CLOSE").Last.ClickAsync();

        // Quests: daily + season sections.
        await Page.GetByTestId("meta-quests").ClickAsync();
        await Expect(Page.GetByTestId("daily-quest").First).ToBeVisibleAsync(new() { Timeout = 10000 });
        await ShotAsync("07-quests");
        await Page.GetByText("✕ CLOSE").Last.ClickAsync();

        // Season pass.
        await Page.GetByTestId("meta-pass").ClickAsync();
        await Expect(Page.GetByTestId("claim-free")).ToBeVisibleAsync(new() { Timeout = 10000 });
        await ShotAsync("08-season-pass");
        await Page.GetByText("✕ CLOSE").Last.ClickAsync();

        // Shop: coin packs + gem packs.
        await Page.GetByTestId("meta-shop").ClickAsync();
        await Expect(Page.GetByTestId("coin-pack").First).ToBeVisibleAsync(new() { Timeout = 10000 });
        await ShotAsync("09-shop");
        await Page.GetByText("✕ CLOSE").Last.ClickAsync();

        // Inbox: send a LiveOps mail with a reward through the admin API so the shot shows real content.
        var api = await Playwright.APIRequest.NewContextAsync();
        var listResp = await api.GetAsync("http://localhost:5550/api/players?query=&count=200");
        var players = (await listResp.JsonAsync())!.Value;
        string playerId = players.EnumerateArray()
            .Where(p => p.TryGetProperty("id", out _))
            .OrderByDescending(p => p.TryGetProperty("createdAt", out var c) ? (c.GetString() ?? "") : "")
            .Select(p => p.GetProperty("id").GetString()!)
            .First();
        await api.PostAsync($"http://localhost:5550/api/players/{playerId}/sendMail", new()
        {
            DataObject = new Dictionary<string, object>
            {
                ["$type"] = "Metaplay.Core.InGameMail.SimplePlayerMail",
                ["id"] = "03f46b106ebbe40-0-5a50f300f139ee44",
                ["title"] = new { localizations = new Dictionary<string, string> { ["en"] = "Welcome bonus" } },
                ["body"] = new { localizations = new Dictionary<string, string> { ["en"] = "A little something for your first transfer window — spend it well." } },
                ["attachments"] = new object[]
                {
                    new Dictionary<string, object> { ["$type"] = "Game.Logic.RewardCurrency", ["currency"] = "Coins", ["amount"] = 500 },
                },
            },
        });
        await Page.GetByTestId("meta-inbox").ClickAsync();
        await Expect(Page.GetByTestId("inbox-mail").First).ToBeVisibleAsync(new() { Timeout = 30000 });
        await ShotAsync("10-inbox");
    }

    /// <summary>
    /// Multiplayer tour: two browser contexts (= two managers). Captures the create-league form, the lobby with
    /// a shareable code, both sides of the turn-based draft (YOUR PICK vs waiting), the draft board, and the
    /// in-season hub of a league with two humans.
    /// </summary>
    [Test]
    public async Task TourMultiplayer()
    {
        await Page.GotoAsync(BaseUrl, new() { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "START A LEAGUE" })).ToBeVisibleAsync(new() { Timeout = 60000 });

        // Create-league form.
        await Page.GetByRole(AriaRole.Button, new() { Name = "START A LEAGUE" }).ClickAsync();
        await Page.GetByPlaceholder("League name").FillAsync("Sunday League");
        await Page.GetByPlaceholder("Your team name").FillAsync("Tour FC");
        await ShotAsync("20-create-league");
        await Page.GetByRole(AriaRole.Button, new() { Name = "CREATE LEAGUE" }).ClickAsync();

        var codeLoc = Page.GetByTestId("league-code");
        await Expect(codeLoc).ToBeVisibleAsync(new() { Timeout = 25000 });
        string code = (await codeLoc.InnerTextAsync()).Trim();

        // Second manager joins from a separate context (phone-format too).
        var ctxB = await Browser.NewContextAsync(ContextOptions());
        var pageB = await ctxB.NewPageAsync();
        try
        {
            await pageB.GotoAsync(BaseUrl, new() { Timeout = 60000, WaitUntil = WaitUntilState.DOMContentLoaded });
            await Assertions.Expect(pageB.GetByRole(AriaRole.Button, new() { Name = "JOIN WITH A CODE" })).ToBeVisibleAsync(new() { Timeout = 60000 });
            await pageB.GetByRole(AriaRole.Button, new() { Name = "JOIN WITH A CODE" }).ClickAsync();
            await pageB.GetByPlaceholder("CODE").FillAsync(code);
            await pageB.GetByPlaceholder("Your team name").FillAsync("Rivals United");
            await pageB.GetByRole(AriaRole.Button, new() { Name = "JOIN LEAGUE" }).ClickAsync();

            // Lobby with the shareable code + both managers.
            await Expect(Page.GetByText("2 / 20")).ToBeVisibleAsync(new() { Timeout = 25000 });
            await ShotAsync("21-lobby");

            // Draft: A on the clock, B waiting.
            await Page.GetByRole(AriaRole.Button, new() { Name = "START DRAFT" }).ClickAsync();
            await Expect(Page.GetByText("YOUR PICK")).ToBeVisibleAsync(new() { Timeout = 25000 });
            await Page.GetByTestId("spin-wheel").ClickAsync();
            await Expect(Page.GetByTestId("draft-candidate").First).ToBeVisibleAsync(new() { Timeout = 25000 });
            await Page.WaitForTimeoutAsync(1600); // staggered squad reveal completes
            await ShotAsync("22-draft-my-pick");

            await Assertions.Expect(pageB.GetByText("is picking")).ToBeVisibleAsync(new() { Timeout = 25000 });
            await pageB.EvaluateAsync("document.querySelectorAll('.t38-bg').forEach(el => el.scrollTo(0, 0))");
            await pageB.WaitForTimeoutAsync(700);
            await pageB.ScreenshotAsync(new() { Path = Path.Combine(OutDir, "23-draft-waiting.png") });

            // A picks; B's turn arrives (live turn-based flow across two sessions).
            await Page.GetByTestId("draft-candidate").First.ClickAsync();
            await Assertions.Expect(pageB.GetByText("YOUR PICK")).ToBeVisibleAsync(new() { Timeout = 25000 });

            // Draft board mid-draft on A.
            await Expect(Page.GetByText("Draft board")).ToBeVisibleAsync(new() { Timeout = 10000 });
            await ShotAsync("24-draft-board");

            // Auto-draft the rest, capture the in-season hub with two human managers in the table.
            await Page.GetByTestId("auto-draft-all").ClickAsync();
            await Expect(Page.GetByTestId("meta-transfers")).ToBeVisibleAsync(new() { Timeout = 30000 });
            await ShotAsync("25-season-hub-multiplayer");
        }
        finally
        {
            await ctxB.CloseAsync();
        }
    }
}
