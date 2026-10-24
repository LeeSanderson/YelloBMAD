using Microsoft.Playwright;
using Xunit;

namespace Yello.Tests.Client;

/// <summary>
/// Story 1.8 in a real browser: Account settings reached from the context bar, issuing an API Token,
/// the plaintext shown once with dismissal a confirmed act, the Token list without a Role, and the
/// plaintext purged with the Session. AC4, AC7, AC11; UX-DR12, UX-DR37.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds, written by the story 1.8 ATDD run on 2026-10-24.</b> Every case is skipped
/// with the AC it waits for. The API is <see cref="FakeYelloApi"/> at the network edge - no Host, no
/// database, no container - so this class adds no engine.
/// </para>
/// <para>
/// <b>The selectors and copy are the story's</b> (Task 7 and its suggested copy): an <c>Account</c>
/// link in <c>nav.context-bar</c> leading to <c>/account</c>; a <c>Name</c> field and a <c>Space</c>
/// picker, labelled; a <c>Generate</c> button; the plaintext in a read-only field; <c>Copy</c> as the
/// <c>button-primary</c>; <c>Done</c> asking "You will not be able to see this Token again." with
/// <c>Dismiss</c> / <c>Keep showing</c>; <c>Revoke</c> asking "Scripts using this Token will stop
/// working." No copy is specified anywhere in the planning set, so these are proposals: a different
/// choice in dev-story is corrected here and recorded in the Debug Log.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-36")]
[Trait("Requirement", "NFR-6")]
[Trait("Requirement", "UX-DR12")]
[Trait("Requirement", "UX-DR37")]
[Collection(nameof(ClientSurfaceCollection))]
public sealed class AccountSettingsTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;

    private const string ContextBar = "nav.context-bar";
    private const string Main = "main";
    private const string AccountSettingsPath = "/account";
    private const string IssueRouteSuffix = "/api-tokens";
    private const string EnglishLocale = "en-GB";

    private const string ShownOnce = "shown once";
    private const string DismissWarning = "You will not be able to see this Token again.";
    private const string RevokeWarning = "Scripts using this Token will stop working.";

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// AC4 / AC11 / <c>EXPERIENCE.md:274</c> - Account settings is reached from the context bar, and
    /// before any Token is generated the page says it will be shown once. Nothing has been issued when
    /// the sentence is read.
    /// </summary>
    [Fact(Skip = "red: AC4/AC11 - the Account link, the /account page and its copy do not exist (Task 7)")]
    public async Task Account_settings_says_a_Token_is_shown_once_before_any_is_generated()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);

            Assert.Contains(ShownOnce, await page.Locator(Main).InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(api.Requests, request => request.Method == "POST" && request.Path.EndsWith(IssueRouteSuffix, StringComparison.Ordinal));
            Assert.False(await HoldsAsync(page, api.IssuedSecret));
        }
    }

    /// <summary>
    /// AC4 / AC11 - generating shows the plaintext exactly once, in a read-only field, with Copy as the
    /// primary action and focus on the plaintext; leaving the page and coming back does not show it
    /// again.
    /// </summary>
    [Fact(Skip = "red: AC4/AC11 - the issue form and the display-once field do not exist (Task 7, Q10a)")]
    public async Task Generating_shows_the_plaintext_once_with_Copy_as_the_primary_action()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);
            await GenerateAsync(page, "deploy script", "Harbour Ops");

            var plaintext = page.Locator("input[readonly]");

            Assert.Equal(api.IssuedSecret, await plaintext.InputValueAsync());
            Assert.Equal(1, await plaintext.CountAsync());
            Assert.True(await plaintext.EvaluateAsync<bool>("e => e === document.activeElement"), "Focus did not move to the plaintext.");

            var copy = page.Locator(Main).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Copy" });

            Assert.Contains("button-primary", await copy.GetAttributeAsync("class") ?? string.Empty, StringComparison.Ordinal);

            // The issuance went to the chosen Space, and only once.
            Assert.Single(api.Requests, request => request.Method == "POST" && request.Path == $"/api/v1/spaces/{Harbour:D}{IssueRouteSuffix}");

            // Away and back inside the running client (history, not a reload, which would clear memory
            // on its own and prove nothing): the only copy was the one shown.
            await page.GoBackAsync();
            await WaitForPathAsync(page, $"/spaces/{Northwind:D}");
            await page.Locator($"{ContextBar} .role-chip").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Account" }).ClickAsync();
            await WaitForPathAsync(page, AccountSettingsPath);
            await page.GetByText("deploy script").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.False(await HoldsAsync(page, api.IssuedSecret), "The plaintext was shown a second time.");
        }
    }

    /// <summary>
    /// AC11 / Q10 (a) - <c>Esc</c> never dismisses the plaintext: pressed on the field and on the page,
    /// the Token is still shown.
    /// </summary>
    [Fact(Skip = "red: AC11 - the display-once field does not exist (Task 7, Q10a)")]
    [Trait("Requirement", "UX-DR40")]
    public async Task Esc_does_not_dismiss_the_Token()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);
            await GenerateAsync(page, "deploy script", "Northwind Redesign");

            await page.Locator("input[readonly]").PressAsync("Escape");
            await page.Keyboard.PressAsync("Escape");

            Assert.Equal(api.IssuedSecret, await page.Locator("input[readonly]").InputValueAsync());
        }
    }

    /// <summary>
    /// AC4 / AC11 - dismissing is a confirmed act: Done asks first, Keep showing keeps it, and only
    /// Dismiss removes the plaintext from the page - from the DOM, not merely from view.
    /// </summary>
    [Fact(Skip = "red: AC4/AC11 - the confirmed dismissal does not exist (Task 7, Q10a)")]
    public async Task Dismissing_the_Token_asks_for_confirmation_and_only_then_removes_it()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);
            await GenerateAsync(page, "deploy script", "Northwind Redesign");

            await Button(page, "Done").ClickAsync();

            await page.GetByText(DismissWarning).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            Assert.True(await HoldsAsync(page, api.IssuedSecret), "Done removed the plaintext without confirmation.");

            await Button(page, "Keep showing").ClickAsync();
            Assert.Equal(api.IssuedSecret, await page.Locator("input[readonly]").InputValueAsync());

            await Button(page, "Done").ClickAsync();
            await Button(page, "Dismiss").ClickAsync();

            await page.WaitForFunctionAsync(
                "s => !document.documentElement.outerHTML.includes(s) && ![...document.querySelectorAll('input,textarea')].some(e => e.value.includes(s))",
                api.IssuedSecret,
                new PageWaitForFunctionOptions { Timeout = Timeout });

            // The list shows the new Token by name: the dismissal kept the record, only the secret went.
            Assert.Contains("deploy script", await page.Locator(Main).InnerTextAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AC11 / AD-7 - the plaintext lives only in component memory: while it is shown, nothing holding
    /// it is in <c>localStorage</c>, <c>sessionStorage</c> or IndexedDB; and signing out purges it, so
    /// the page after sign-out holds no trace of it.
    /// </summary>
    [Fact(Skip = "red: AC11 - the display-once field and its purge registration do not exist (Task 7)")]
    [Trait("Requirement", "AD-7")]
    [Trait("Requirement", "FR-2")]
    public async Task The_plaintext_reaches_no_web_storage_and_is_purged_by_sign_out()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);
            await GenerateAsync(page, "deploy script", "Northwind Redesign");

            Assert.True(await HoldsAsync(page, api.IssuedSecret), "The anchor failed: the plaintext is not shown.");
            Assert.Equal(0, await page.EvaluateAsync<int>("() => localStorage.length + sessionStorage.length"));
            Assert.Equal(0, await page.EvaluateAsync<int>("async () => (await indexedDB.databases()).length"));

            await page.Locator(ContextBar).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Sign out" }).ClickAsync();
            await WaitForPathAsync(page, "/sign-in");

            Assert.False(await HoldsAsync(page, api.IssuedSecret), "The plaintext survived sign-out.");
        }
    }

    /// <summary>
    /// AC7 / AR-29 - the Token list shows each Token's name and its Space's name, and no Role: no chip
    /// and no Role word inside the page body (the context bar's own chip is outside it).
    /// </summary>
    [Fact(Skip = "red: AC7 - the Token list does not exist (Task 7)")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task The_Token_list_shows_names_and_Spaces_and_no_Role()
    {
        var api = TwoSpaces();
        api.ApiTokens.Add(new FakeApiToken(Guid.NewGuid(), "nightly export", Northwind, "Northwind Redesign", DateTimeOffset.UtcNow.AddDays(-2)));
        api.ApiTokens.Add(new FakeApiToken(Guid.NewGuid(), "board sync", Harbour, "Harbour Ops", DateTimeOffset.UtcNow.AddDays(-1)));

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);
            await page.GetByText("board sync").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var body = await page.Locator(Main).InnerTextAsync();

            Assert.Contains("nightly export", body, StringComparison.Ordinal);
            Assert.Contains("Harbour Ops", body, StringComparison.Ordinal);
            Assert.Equal(0, await page.Locator($"{Main} .role-chip").CountAsync());

            foreach (var role in new[] { "Owner", "Admin", "Member", "Viewer" })
            {
                Assert.DoesNotContain(role, body, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// AC11 / UJ-7 / AD-24 - the Space a Token is bound to is chosen from exactly the Spaces the
    /// switcher lists, by name.
    /// </summary>
    [Fact(Skip = "red: AC11 - the Space picker does not exist (Task 7)")]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AD-24")]
    public async Task The_Space_picker_offers_exactly_the_switcher_Spaces_by_name()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);

            var options = (await page.GetByLabel("Space").Locator("option").AllInnerTextsAsync())
                .Select(text => text.Trim())
                .Where(text => text.Length > 0)
                .ToList();

            Assert.Equal(["Northwind Redesign", "Harbour Ops"], options);
        }
    }

    /// <summary>
    /// Q3 (a) - revoking asks for confirmation in place, Cancel sends nothing, and Revoke sends the
    /// <c>DELETE</c> with the anti-forgery token and removes the row.
    /// </summary>
    [Fact(Skip = "red: Q3 - revocation in Account settings does not exist (Task 7)")]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AR-14")]
    public async Task Revoking_asks_for_confirmation_and_then_sends_the_DELETE()
    {
        var api = TwoSpaces();
        var tokenId = Guid.NewGuid();
        api.ApiTokens.Add(new FakeApiToken(tokenId, "nightly export", Northwind, "Northwind Redesign", DateTimeOffset.UtcNow));

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);
            await page.GetByText("nightly export").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            await Button(page, "Revoke").ClickAsync();
            await page.GetByText(RevokeWarning).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await Button(page, "Cancel").ClickAsync();

            Assert.DoesNotContain(api.Requests, request => request.Method == "DELETE" && request.Path.Contains(tokenId.ToString("D"), StringComparison.OrdinalIgnoreCase));

            await Button(page, "Revoke").ClickAsync();
            await page.GetByText(RevokeWarning).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.Locator(Main).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Revoke" }).Last.ClickAsync();

            await page.GetByText("nightly export").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });

            var revoke = Assert.Single(api.Requests, request => request.Method == "DELETE" && request.Path.Contains(tokenId.ToString("D"), StringComparison.OrdinalIgnoreCase));
            Assert.Equal(api.IssuedToken, revoke.Header(Contracts.Accounts.Antiforgery.HeaderName));
        }
    }

    /// <summary>
    /// A signed-in Account who is Owner of Northwind Redesign and Viewer of Harbour Ops.
    /// </summary>
    private static FakeYelloApi TwoSpaces()
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, "Northwind Redesign"));
        api.Switcher.Add((Harbour, "Harbour Ops"));
        api.Roles[Northwind] = "owner";
        api.Roles[Harbour] = "viewer";

        return api;
    }

    /// <summary>
    /// Opens a Space, then follows the context bar's Account link - the entry <c>EXPERIENCE.md:55</c>
    /// names - rather than typing the address, so the link is part of what is asserted.
    /// </summary>
    private async Task OpenAccountSettingsAsync(FakeYelloApi api, IPage page)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{Northwind:D}"));
        await page.Locator($"{ContextBar} .role-chip").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

        await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Account" }).ClickAsync();

        await WaitForPathAsync(page, AccountSettingsPath);
        await page.Locator($"{Main} h1").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static async Task GenerateAsync(IPage page, string name, string spaceName)
    {
        await page.GetByLabel("Name").FillAsync(name);
        await page.GetByLabel("Space").SelectOptionAsync(new SelectOptionValue { Label = spaceName });
        await Button(page, "Generate").ClickAsync();

        await page.Locator("input[readonly]").WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }

    private static ILocator Button(IPage page, string name) =>
        page.Locator(Main).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = name, Exact = true }).First;

    /// <summary>
    /// Whether the page holds <paramref name="secret"/> anywhere - in its markup or in any field's
    /// value, which <c>outerHTML</c> does not include.
    /// </summary>
    private static Task<bool> HoldsAsync(IPage page, string secret) =>
        page.EvaluateAsync<bool>(
            "s => document.documentElement.outerHTML.includes(s) || [...document.querySelectorAll('input,textarea')].some(e => e.value.includes(s))",
            secret);

    /// <summary>
    /// Waits on the page's own <c>location.pathname</c>, never Playwright's URL wait
    /// (<c>TESTING-CONVENTIONS.md</c>).
    /// </summary>
    private static Task WaitForPathAsync(IPage page, string path) =>
        page.WaitForFunctionAsync(
            "path => location.pathname === path",
            path,
            new PageWaitForFunctionOptions { Timeout = Timeout });

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();
}
