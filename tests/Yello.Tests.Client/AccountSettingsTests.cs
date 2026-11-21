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
/// <b>Written by the story 1.8 ATDD run on 2026-10-24 and active since its dev-story run.</b> The API
/// is <see cref="FakeYelloApi"/> at the network edge - no Host, no database, no container - so this
/// class adds no engine.
/// </para>
/// <para>
/// <b>The selectors and copy are the story's</b> (Task 7 and the copy in <c>ClientCopy.resx</c>): an
/// <c>Account</c> link in <c>nav.context-bar</c> leading to <c>/account</c>; a <c>Name</c> field and a
/// <c>Space</c> picker, labelled; a <c>Generate</c> button; the plaintext in a read-only field;
/// <c>Copy</c> as the <c>button-primary</c>; <c>Done</c> asking "You will not be able to see this
/// Token again." with <c>Dismiss</c> / <c>Keep showing</c>; <c>Revoke</c> asking "Scripts using this
/// Token will stop working."
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-36")]
[Trait("Requirement", "NFR-6")]
[Trait("Requirement", "UX-DR12")]
[Trait("Requirement", "UX-DR37")]
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
    private const string NameInvalid = "Enter a name of 1 to 100 characters.";

    private const string DeletionSection = "main .account-delete-section";
    private const string OpenDeletion = "Delete your Account";
    private const string DeletionQuestion = "Delete your Account?";
    private const string CannotBeUndone = "This cannot be undone.";
    private const string DeleteAction = "Delete Account";
    private const string CancelAction = "Cancel";
    private const string AccountDeleted = "Account deleted.";
    private const string NotSent = "Not sent.";
    private const string OwnsSpacesStatement = "You own these Spaces, so your Account cannot be deleted yet.";
    private const string TwoExits = "For each one, either offer ownership to someone in it and have them accept, or delete the Space.";
    private const string WhyDeletingIsYours = "Deleting the Space is the only exit you control on your own, because a transfer needs someone else to accept.";

    /// <summary>
    /// A 1.4.10 reflow probe over the page, as <c>InvitationViewTests.Reflow</c>.
    /// </summary>
    private const string TextSpacingOverride =
        "* { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; } "
        + "p { margin-block-end: 2em !important; }";

    private const string Reflow =
        """
        () => {
            const problems = [];
            const page = document.scrollingElement;
            if (page.scrollWidth > page.clientWidth + 1) problems.push('the page scrolls sideways by ' + (page.scrollWidth - page.clientWidth) + 'px');
            for (const element of document.querySelectorAll('main *')) {
                const style = getComputedStyle(element);
                if (style.display === 'none') continue;
                if (['auto', 'scroll'].includes(style.overflowX) && element.scrollWidth > element.clientWidth + 1) {
                    problems.push('scrolls sideways: ' + element.tagName + '.' + element.className);
                }
            }
            return problems;
        }
        """;

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// AC4 / AC11 / <c>EXPERIENCE.md:274</c> - Account settings is reached from the context bar, and
    /// before any Token is generated the page says it will be shown once. Nothing has been issued when
    /// the sentence is read.
    /// </summary>
    [Fact]
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
    [Fact]
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
    [Fact]
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
    [Fact]
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
    [Fact]
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
    [Fact]
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

            // Read off the list's rows, not the page: the Space picker holds the same Space names, so
            // a page-wide search would pass with no Space in the list at all.
            var rows = (await page.Locator($"{Main} .api-token-list__row").AllAsync()).ToList();
            var named = new List<(string Name, string Space)>();

            foreach (var row in rows)
            {
                named.Add((
                    (await row.Locator(".api-token-list__name").InnerTextAsync()).Trim(),
                    (await row.Locator(".api-token-list__space").InnerTextAsync()).Trim()));
            }

            Assert.Equal([("nightly export", "Northwind Redesign"), ("board sync", "Harbour Ops")], named);
            Assert.Equal(0, await page.Locator($"{Main} .role-chip").CountAsync());

            foreach (var role in new[] { "Owner", "Admin", "Member", "Viewer" })
            {
                Assert.DoesNotContain(role, body, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Q8 - a name the Host refuses (<c>400 invalid-api-token</c>) is answered beside the name field,
    /// and no plaintext is shown. Added at code review of story 1.8: the message is keyed on the
    /// problem type, not on the status alone.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    public async Task A_refused_name_is_answered_beside_the_field_and_shows_no_Token()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);

            await page.Locator(Main).GetByLabel("Name").FillAsync("   ");
            await Button(page, "Generate").ClickAsync();

            await page.Locator(Main).GetByText(NameInvalid).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Single(api.Requests, request => request.Method == "POST" && request.Path.EndsWith(IssueRouteSuffix, StringComparison.Ordinal));
            Assert.Equal(0, await page.Locator("input[readonly]").CountAsync());
            Assert.False(await HoldsAsync(page, api.IssuedSecret));
        }
    }

    /// <summary>
    /// AC11 / UJ-7 / AD-24 - the Space a Token is bound to is chosen from exactly the Spaces the
    /// switcher lists, by name.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "AD-24")]
    public async Task The_Space_picker_offers_exactly_the_switcher_Spaces_by_name()
    {
        var api = TwoSpaces();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);

            var options = (await page.Locator(Main).GetByLabel("Space").Locator("option").AllInnerTextsAsync())
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
    [Fact]
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
    /// Deleting the Account asks first, in place, below the API Tokens: "Delete your Account?" as the heading, which takes
    /// focus, "This cannot be undone." after it, the danger "Delete Account" and Cancel. Cancel and Esc each close it and
    /// send nothing.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "UX-DR36")]
    [Trait("Requirement", "UX-DR40")]
    public async Task Deleting_the_Account_asks_in_place_and_Cancel_and_Esc_send_nothing()
    {
        var api = OwnsNothing();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);

            foreach (var close in new Func<Task>[] { () => Button(page, CancelAction).ClickAsync(), () => page.Keyboard.PressAsync("Escape") })
            {
                await OpenDeletionConfirmAsync(page);

                var confirm = page.Locator($"{DeletionSection} .destructive-confirm");
                var question = confirm.GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = DeletionQuestion, Exact = true });

                Assert.True(await question.EvaluateAsync<bool>("e => e === document.activeElement"), "Focus did not move to the question.");
                Assert.Contains(CannotBeUndone, await confirm.TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
                Assert.Contains("button-danger", await confirm.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = DeleteAction, Exact = true }).GetAttributeAsync("class") ?? string.Empty, StringComparison.Ordinal);

                await close();
                await confirm.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached, Timeout = Timeout });
            }

            Assert.Empty(api.AccountDeletions);
        }
    }

    /// <summary>
    /// Confirmed, the deletion is one <c>DELETE</c> carrying the anti-forgery token and no <c>Idempotency-Key</c>;
    /// on <c>204</c> the tab purges and lands on the sign-in page, which says "Account deleted." politely.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "AR-14")]
    [Trait("Requirement", "UX-DR36")]
    public async Task A_confirmed_deletion_sends_one_DELETE_and_lands_on_sign_in_saying_Account_deleted()
    {
        var api = OwnsNothing();
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);
            await OpenDeletionConfirmAsync(page);
            await Button(page, DeleteAction).ClickAsync();

            await WaitForPathAsync(page, "/sign-in");
            await page.Locator("[aria-live='polite'], [role='status']", new PageLocatorOptions { HasText = AccountDeleted })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var deletion = Assert.Single(api.AccountDeletions);

            Assert.Equal(api.IssuedToken, deletion.Header(Contracts.Accounts.Antiforgery.HeaderName));
            Assert.Null(deletion.Header(Contracts.Idempotency.HeaderName));
            Assert.Equal(0, await page.Locator(ContextBar).CountAsync());
        }
    }

    /// <summary>
    /// An Owner's deletion is refused, and the section states it as a designed state, not a toast or an alert: the
    /// statement as a heading with focus moved to it; every owned Space by name, each a link to that Space's settings; the
    /// two exits; and why deleting the Space is the one exit the Account controls. No third exit is offered or implied.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "UX-DR36")]
    public async Task An_Owner_is_shown_the_Spaces_it_owns_and_exactly_two_exits_with_focus_on_the_statement()
    {
        var api = OwnsNothing();

        api.OwnedSpaces.Add((Northwind, "Northwind Redesign"));
        api.OwnedSpaces.Add((Harbour, "Harbour Ops"));
        api.Roles[Northwind] = "owner";
        api.Roles[Harbour] = "owner";

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);
            await OpenDeletionConfirmAsync(page);
            await Button(page, DeleteAction).ClickAsync();

            var section = page.Locator(DeletionSection);
            var statement = section.GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = OwnsSpacesStatement, Exact = true });

            await statement.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await statement.EvaluateAsync<bool>("e => e === document.activeElement"), "Focus did not move to the statement.");

            foreach (var (id, name) in api.OwnedSpaces)
            {
                var link = section.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = name, Exact = true });

                Assert.Equal($"/spaces/{id:D}/settings", new Uri(await link.EvaluateAsync<string>("e => e.href")).AbsolutePath);
            }

            var text = await section.TextContentAsync() ?? string.Empty;

            Assert.Contains(TwoExits, text, StringComparison.Ordinal);
            Assert.Contains(WhyDeletingIsYours, text, StringComparison.Ordinal);

            foreach (var thirdExit in new[] { "leave", "archive", "support", "remove yourself" })
            {
                Assert.DoesNotContain(thirdExit, text, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Equal(0, await page.Locator("[role='alert']:not(:empty)").CountAsync());
            Assert.Equal(AccountSettingsPath, await page.EvaluateAsync<string>("() => location.pathname"));
            Assert.Single(api.AccountDeletions);
        }
    }

    /// <summary>
    /// A deletion that gets a server failure says "Not sent." and keeps the confirm open, on Account settings, still signed in.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "UX-DR36")]
    public async Task A_deletion_that_fails_says_Not_sent_and_keeps_the_confirm()
    {
        var api = OwnsNothing();

        api.NextAccountDeletionFailure = 503;

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenAccountSettingsAsync(api, page);
            await OpenDeletionConfirmAsync(page);
            await Button(page, DeleteAction).ClickAsync();

            await page.Locator(DeletionSection).GetByText(NotSent).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(1, await page.Locator($"{DeletionSection} .destructive-confirm").CountAsync());
            Assert.Equal(AccountSettingsPath, await page.EvaluateAsync<string>("() => location.pathname"));
            Assert.True(api.SignedIn, "The fake ended the Session on a failed deletion.");
        }
    }

    /// <summary>
    /// At 320 by 256 (1280 by 1024 at 400%) the refusal state reflows: no sideways page scroll and nothing in <c>main</c> that
    /// scrolls sideways, with a long Space name in the list - also under the 1.4.12 text-spacing override.
    /// </summary>
    /// <param name="spaced">Whether the 1.4.12 text-spacing override is applied.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "NFR-9")]
    public async Task The_refusal_state_reflows_at_320_by_256(bool spaced)
    {
        var api = OwnsNothing();

        api.OwnedSpaces.Add((Northwind, "Northwind Redesign and the very long name of a Space that wraps"));

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 256);
            await OpenAccountSettingsAsync(api, page);
            await OpenDeletionConfirmAsync(page);
            await Button(page, DeleteAction).ClickAsync();
            await page.Locator(DeletionSection).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = OwnsSpacesStatement, Exact = true })
                .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            if (spaced)
            {
                await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = TextSpacingOverride });
            }

            var problems = await page.EvaluateAsync<string[]>(Reflow);

            Assert.True(problems.Length == 0, string.Join("; ", problems));
        }
    }

    /// <summary>
    /// A signed-in Account who owns nothing: a Member of Northwind Redesign and a Viewer of Harbour Ops. A case that wants
    /// the refusal sets <see cref="FakeYelloApi.OwnedSpaces"/>.
    /// </summary>
    private static FakeYelloApi OwnsNothing()
    {
        var api = TwoSpaces();

        api.Roles[Northwind] = "member";

        return api;
    }

    /// <summary>
    /// Opens the deletion's confirm from its section and waits for the question.
    /// </summary>
    private static async Task OpenDeletionConfirmAsync(IPage page)
    {
        await page.Locator(DeletionSection).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = OpenDeletion, Exact = true }).ClickAsync();
        await page.Locator(DeletionSection).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = DeletionQuestion, Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
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
        // Scoped to <main>: the context bar's own accessible name ("Your Spaces") also matches "Space".
        await page.Locator(Main).GetByLabel("Name").FillAsync(name);
        await page.Locator(Main).GetByLabel("Space").SelectOptionAsync(new SelectOptionValue { Label = spaceName });
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
