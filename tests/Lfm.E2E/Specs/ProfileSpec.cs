using FluentAssertions;
using Lfm.E2E.Fixtures;
using Lfm.E2E.Helpers;
using Lfm.E2E.Infrastructure;
using Lfm.E2E.Pages;
using Lfm.E2E.Seeds;
using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace Lfm.E2E.Specs;

[Collection("Profile")]
[Trait("Category", "Functional")]
public class ProfileSpec(ProfileFixture fixture, ITestOutputHelper output)
    : E2ETestBase(output), IAsyncLifetime
{
    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        Context = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl);
        Page = await Context.NewPageAsync();
        AttachDiagnosticListeners();
        await StartTracingAsync();
    }

    public override async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (Context is not null)
            await Context.CloseAsync();
    }

    // -------------------------------------------------------------------------
    // Characters tests (4.2)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CharactersPage_Loads_DisplaysCharacterList()
    {
        var charactersPage = new CharactersPage(Page!);

        // Block the portrait request — it's fire-and-forget and crashes Blazor
        // when the E2E API can't process it (known app issue).
        await Page!.RouteAsync("**/api/battlenet/character-portraits", async route =>
        {
            await route.FulfillAsync(new() { Status = 200, Body = "[]" });
        });

        await charactersPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(charactersPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Wait for at least one character card from seed data (Aelrin + Aelrinalt).
        await Assertions.Expect(charactersPage.CharacterList.First)
            .ToBeVisibleAsync(new() { Timeout = 15000 });

        var count = await charactersPage.GetCharacterCountAsync();
        Log($"Character cards rendered: {count}");
    }

    [Fact]
    public async Task RefreshCharacters_Click_UpdatesFromBattleNet()
    {
        var charactersPage = new CharactersPage(Page!);

        await charactersPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(charactersPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        // Intercept the refresh API call to confirm the button wires up correctly.
        // The request will fail (no real Blizzard access token in E2E sessions),
        // but we verify the call is initiated — the page should not crash.
        var refreshCallInitiated = false;
        Page!.Request += (_, req) =>
        {
            if (req.Url.Contains("/api/battlenet/characters/refresh"))
                refreshCallInitiated = true;
        };

        await Assertions.Expect(charactersPage.RefreshButton).ToBeVisibleAsync(new() { Timeout = 10000 });
        await charactersPage.ClickRefreshAsync();

        // Wait briefly for the request to be dispatched.
        await Page.WaitForTimeoutAsync(2000);

        refreshCallInitiated.Should().BeTrue(
            "clicking the refresh button should POST to /api/battlenet/characters/refresh");

        Log("Refresh API call confirmed dispatched");
    }

    [Fact]
    public async Task SelectCharacter_ViaApi_UpdatesSelection()
    {
        // The characters page does not currently expose a selection UI;
        // this test verifies the underlying API contract that character selection works.
        // PUT /api/raider/characters/{id} with a valid owned character ID returns 200.
        var altCharId = "eu-test-realm-aelrin-alt";

        // Page.APIRequest doesn't carry browser cookies cross-origin, so forward
        // the auth cookie manually from the browser context.
        var cookies = await Context!.CookiesAsync();
        var authCookie = cookies.FirstOrDefault(c => c.Name == "battlenet_token");
        authCookie.Should().NotBeNull("auth cookie should exist in browser context");

        var headers = new Dictionary<string, string>
        {
            ["Accept"] = "application/json",
            ["Cookie"] = $"{authCookie!.Name}={authCookie.Value}",
        };

        var response = await Page!.APIRequest.PutAsync(
            $"{fixture.Stack.ApiBaseUrl}/api/raider/characters/{altCharId}",
            new() { Headers = headers });

        var responseBody = await response.TextAsync();
        Log($"SelectCharacter response: {response.Status} — {responseBody}");

        response.Status.Should().Be(200,
            $"selecting an owned character should succeed — response: {responseBody}");

        var body = await response.JsonAsync();
        body.Value.GetProperty("selectedCharacterId").GetString()
            .Should().Be(altCharId, "the selected character id should be updated");

        Log($"Character selection confirmed: {altCharId}");
    }

    // -------------------------------------------------------------------------
    // Guild tests (4.3)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GuildPage_Loads_DisplaysGuildInfo()
    {
        var guildPage = new GuildPage(Page!);

        await guildPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(guildPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        // The seeded guild document has blizzardProfileRaw with name "Test Guild".
        await Assertions.Expect(guildPage.GuildNameHeading).ToBeVisibleAsync(new() { Timeout = 10000 });

        Log("Guild page rendered with guild info visible");
    }

    [Fact]
    public async Task GuildAdmin_Loads_DisplaysSettings()
    {
        var guildAdminPage = new GuildAdminPage(Page!);

        await guildAdminPage.GotoAsync(fixture.Stack.AppBaseUrl);

        await Assertions.Expect(guildAdminPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

        // The settings form is rendered when guild data loads successfully.
        await Assertions.Expect(guildAdminPage.OverrideSettingsHeading)
            .ToBeVisibleAsync(new() { Timeout = 10000 });

        await Assertions.Expect(guildAdminPage.SaveButton).ToBeVisibleAsync(new() { Timeout = 10000 });

        Log("Guild admin settings form is visible");
    }

    [Fact]
    public async Task GuildAdmin_UpdateSettings_ChangesReflected()
    {
        var guildAdminPage = new GuildAdminPage(Page!);

        await guildAdminPage.GotoAsync(fixture.Stack.AppBaseUrl);
        await Assertions.Expect(guildAdminPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });
        await Assertions.Expect(guildAdminPage.SaveButton).ToBeVisibleAsync(new() { Timeout = 10000 });

        // FluentTextArea is a web component — set value on the outer element and
        // dispatch 'change' to trigger Blazor's @bind-Value.
        var newSlogan = $"E2E updated slogan {Guid.NewGuid():N}";
        var outerSlogan = Page!.Locator("#guild-slogan").First;
        await outerSlogan.EvaluateAsync($"el => {{ el.value = '{newSlogan}'; el.dispatchEvent(new Event('change', {{ bubbles: true }})); }}");

        await guildAdminPage.SaveButton.ClickAsync();

        // Success message should appear confirming the save.
        await Assertions.Expect(guildAdminPage.SuccessMessage).ToBeVisibleAsync(new() { Timeout = 15000 });

        Log($"Guild settings saved successfully with slogan: {newSlogan}");
    }

    // -------------------------------------------------------------------------
    // Delete account test (4.4)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAccount_Confirm_RedirectsToGoodbye()
    {
        // Use a dedicated context with the secondary test user to avoid invalidating
        // the shared primary user session used by other tests.
        var deleteContext = await AuthHelper.AuthenticatedContextAsync(
            fixture.Stack.Browser,
            fixture.Stack.ApiBaseUrl,
            fixture.Stack.AppBaseUrl,
            battleNetId: DefaultSeed.SecondaryBattleNetId,
            redirect: "/characters");
        var deletePage = await deleteContext.NewPageAsync();

        try
        {
            var charactersPage = new CharactersPage(deletePage);
            await charactersPage.GotoAsync(fixture.Stack.AppBaseUrl);
            await Assertions.Expect(charactersPage.Heading).ToBeVisibleAsync(new() { Timeout = 15000 });

            // Type the confirmation phrase and click delete.
            // FluentTextField is a web component — set value on the outer element and
            // dispatch 'change' to trigger Blazor's @bind-Value.
            var outerField = deletePage.Locator("fluent-text-field[placeholder='FORGET ME']").First;
            await Assertions.Expect(outerField).ToBeVisibleAsync(new() { Timeout = 10000 });
            await outerField.EvaluateAsync("el => { el.value = 'FORGET ME'; el.dispatchEvent(new Event('change', { bubbles: true })); }");

            await Assertions.Expect(charactersPage.DeleteAccountButton)
                .ToBeEnabledAsync(new() { Timeout = 5000 });
            await charactersPage.DeleteAccountButton.ClickAsync();

            // Should redirect to /goodbye after successful deletion.
            await Assertions.Expect(deletePage).ToHaveURLAsync(
                new System.Text.RegularExpressions.Regex(@"/goodbye"),
                new() { Timeout = 15000 });

            await Assertions.Expect(deletePage.GetByText("Goodbye")).ToBeVisibleAsync(
                new() { Timeout = 10000 });

            Log("Delete account flow completed — redirected to /goodbye");
        }
        finally
        {
            await deleteContext.CloseAsync();
        }
    }
}
