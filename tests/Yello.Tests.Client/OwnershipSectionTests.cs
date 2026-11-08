using Microsoft.Playwright;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Space settings' Ownership section in a real browser: Owner-only (absent, not disabled, for anyone else); a recipient
/// picker over the Space's other Memberships by label with nothing selected; an in-place confirm that pre-focuses neither
/// choice; one send with one key; the pending offer with its recipient and its expiry counted from the server's clock; and
/// Revoke, which returns to the picker and says so.
/// </summary>
/// <remarks>
/// <para>
/// The API is <see cref="FakeYelloApi"/> at the network edge, taught the offer routes in
/// <c>FakeYelloApi.OwnershipOffers.cs</c>; no Host, no database, no engine. Same assembly fixture as
/// <see cref="MembershipSectionTests"/>, so no new fixture. The picker's labels are seeded both as the Members list and as the
/// assignment candidates, so either read the section is built on finds them.
/// </para>
/// <para>
/// <b>Locators are assumptions</b> written from the story's copy and the Role picker's shape: the section is
/// <c>main .ownership-section</c> under a heading "Ownership"; the recipients are radios named by each Membership's label
/// inside it; the action is a button "Offer ownership"; the confirm's buttons are "Offer" and "Cancel"; the pending state
/// shows "Ownership offered to {label}. Waiting for their answer.", "Expires in {n} days" with a <c>time</c> element, and a
/// button "Revoke". Text is read with <c>textContent</c>. A markup built differently is corrected here first and recorded in
/// the Debug Log.
/// </para>
/// </remarks>
[Trait("Suite", "Client")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-8")]
[Trait("Requirement", "UX-DR14")]
public sealed class OwnershipSectionTests(ClientSurfaceFixture surface)
{
    private const float Timeout = 30_000;
    private const string EnglishLocale = "en-GB";
    private const string ContextBar = "nav.context-bar";
    private const string RoleChip = "nav.context-bar .role-chip";
    private const string Section = "main .ownership-section";
    private const string MembersSection = "main .membership-section";

    private const string SettingsLink = "Space settings";
    private const string SectionHeading = "Ownership";
    private const string OfferOwnership = "Offer ownership";
    private const string OfferAction = "Offer";
    private const string CancelAction = "Cancel";
    private const string RevokeAction = "Revoke";
    private const string SpaceName = "Northwind Redesign";
    private const string InvalidRecipientCopy = "Ownership can be offered only to another Membership of this Space.";

    /// <summary>
    /// Records every non-empty text a polite or status region changes to: <c>window.__said</c>.
    /// </summary>
    private const string RecordEverythingSaid =
        """
        () => {
            window.__said = [];
            const last = new WeakMap();
            const regions = () => document.querySelectorAll('[aria-live="polite"], [role="status"]');
            for (const region of regions()) last.set(region, region.textContent.trim());
            new MutationObserver(() => {
                for (const region of regions()) {
                    const text = region.textContent.trim();
                    if (text && last.get(region) !== text) window.__said.push(text);
                    last.set(region, text);
                }
            }).observe(document.body, { childList: true, subtree: true, characterData: true });
        }
        """;

    /// <summary>
    /// A 1.4.10 reflow probe over the page and the section.
    /// </summary>
    private const string Reflow =
        """
        () => {
            const problems = [];
            const page = document.scrollingElement;
            if (page.scrollWidth > page.clientWidth + 1) problems.push('the page scrolls sideways by ' + (page.scrollWidth - page.clientWidth) + 'px');
            for (const element of document.querySelectorAll('main .ownership-section, main .ownership-section *')) {
                const style = getComputedStyle(element);
                if (style.display === 'none') continue;
                if (['auto', 'scroll'].includes(style.overflowX) && element.scrollWidth > element.clientWidth + 1) {
                    problems.push('scrolls sideways: ' + element.tagName + '.' + element.className);
                }
            }
            return problems;
        }
        """;

    private const string TextSpacingOverride =
        "* { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; } "
        + "p { margin-block-end: 2em !important; }";

    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// The Owner sees the Ownership section: one radio per other Membership of any Role, named by its label, the Owner's own
    /// row not among them, <b>no radio checked</b> (the choice is consequential, so there is no default), and no address
    /// anywhere in the section.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR11")]
    [Trait("Requirement", "AD-23")]
    public async Task The_Owner_sees_the_Ownership_section_with_a_recipient_picker_that_selects_no_one()
    {
        var api = SpaceWithMembers("owner", out var others);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenOwnershipAsync(api, page);

            var radios = page.Locator(Section).GetByRole(AriaRole.Radio);

            await radios.First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(others.Select(row => row.DisplayName).Order(StringComparer.Ordinal), (await RadioNamesAsync(page)).Order(StringComparer.Ordinal));
            Assert.Equal(0, await page.Locator($"{Section} input[type=radio]:checked").CountAsync());
            Assert.DoesNotContain("@", await page.Locator(Section).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// An Admin sees no Ownership section at all: Space settings opens (its Members section is there), and there is no
    /// Ownership heading, no section and no disabled control standing in for one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public async Task An_Admin_has_no_Ownership_section_at_all()
    {
        var api = SpaceWithMembers("admin", out _);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenSettingsAsync(api, page);
            await page.Locator(MembersSection).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Equal(0, await page.Locator(Section).CountAsync());
            Assert.Equal(0, await page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = SectionHeading, Exact = true }).CountAsync());
            Assert.Equal(0, await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = OfferOwnership, Exact = true }).CountAsync());
            Assert.Empty(api.OwnershipOffersSent(Northwind));
        }
    }

    /// <summary>
    /// "Offer ownership" replaces the form in place with a confirm naming the Space and the recipient, saying the Owner stays
    /// the Owner until they accept and that they will be emailed; neither "Offer" nor "Cancel" is focused. Cancel restores the
    /// picker and sends nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR11")]
    public async Task The_offer_asks_in_place_pre_focuses_neither_choice_and_Cancel_sends_nothing()
    {
        var api = SpaceWithMembers("owner", out var others);
        var recipient = others[0];
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenOwnershipAsync(api, page);
            await ChooseAndAskAsync(page, recipient.DisplayName);

            var section = page.Locator(Section);
            var offer = section.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = OfferAction, Exact = true });

            await offer.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var text = await section.TextContentAsync() ?? string.Empty;

            Assert.Contains($"Offer ownership of {SpaceName} to {recipient.DisplayName}?", text, StringComparison.Ordinal);
            Assert.Contains("You stay the Owner until they accept.", text, StringComparison.Ordinal);
            Assert.Contains("They will be emailed.", text, StringComparison.Ordinal);
            Assert.False(await offer.EvaluateAsync<bool>("e => e === document.activeElement"), "The confirm pre-focuses Offer.");
            Assert.False(
                await section.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CancelAction, Exact = true }).EvaluateAsync<bool>("e => e === document.activeElement"),
                "The confirm pre-focuses Cancel.");

            await section.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = CancelAction, Exact = true }).ClickAsync();
            await section.GetByRole(AriaRole.Radio).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.Empty(api.OwnershipOffersSent(Northwind));
        }
    }

    /// <summary>
    /// The confirmed offer is sent once, with one <c>Idempotency-Key</c> and the chosen Membership's id, and the section then
    /// shows the pending offer by the recipient's label with a Revoke control.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    public async Task The_confirmed_offer_is_sent_once_with_one_key_and_shows_the_pending_offer()
    {
        var api = SpaceWithMembers("owner", out var others);
        var recipient = others[1];
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenOwnershipAsync(api, page);
            await ChooseAndAskAsync(page, recipient.DisplayName);
            await page.Locator(Section).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = OfferAction, Exact = true }).ClickAsync();
            await PendingTextAsync(page, recipient.DisplayName);

            var sent = Assert.Single(api.OwnershipOffersSent(Northwind));

            Assert.False(string.IsNullOrEmpty(sent.Header(Idempotency.HeaderName)), "The offer carries no Idempotency-Key.");
            Assert.Contains(recipient.Id.ToString("D"), sent.Body ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, await page.Locator(Section).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RevokeAction, Exact = true }).CountAsync());
        }
    }

    /// <summary>
    /// The expiry is counted from the server's clock: the pending offer's <c>ExpiresAt</c> is six days and a few hours after
    /// its <c>ReadAt</c>, while the browser's clock is set more than a year away. The section says "Expires in 6 days" and
    /// carries a <c>time</c> element with a machine-readable <c>datetime</c>. A section counting from the browser's clock
    /// says something else.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task The_expiry_is_counted_from_the_server_clock_and_not_the_browsers()
    {
        var api = SpaceWithMembers("owner", out var others);
        var recipient = others[0];

        api.PendingOffers[Northwind] = (Guid.NewGuid(), recipient.Id, recipient.DisplayName, api.ServerNow.AddDays(6).AddHours(5), api.ServerNow);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.Clock.SetFixedTimeAsync(api.ServerNow.AddDays(400).UtcDateTime);
            await OpenOwnershipAsync(api, page);
            await PendingTextAsync(page, recipient.DisplayName);

            Assert.Contains("Expires in 6 days", await page.Locator(Section).TextContentAsync() ?? string.Empty, StringComparison.Ordinal);
            Assert.False(
                string.IsNullOrEmpty(await page.Locator($"{Section} time[datetime]").First.GetAttributeAsync("datetime")),
                "The expiry carries no machine-readable time.");
        }
    }

    /// <summary>
    /// Revoke (no confirm) sends one <c>DELETE</c> for the pending offer, returns the section to the picker with nothing
    /// selected, says "Ownership offer revoked." with no name in it, and leaves focus on an element rather than the body.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "UX-DR36")]
    public async Task Revoke_returns_to_the_picker_says_so_and_keeps_focus_off_the_body()
    {
        var api = SpaceWithMembers("owner", out var others);
        var recipient = others[0];
        var offerId = Guid.NewGuid();

        api.PendingOffers[Northwind] = (offerId, recipient.Id, recipient.DisplayName, api.ServerNow.AddDays(OwnershipOfferWire.ExpiryDays), api.ServerNow);

        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenOwnershipAsync(api, page);
            await PendingTextAsync(page, recipient.DisplayName);
            await page.EvaluateAsync(RecordEverythingSaid);
            await page.Locator(Section).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = RevokeAction, Exact = true }).ClickAsync();
            await page.Locator(Section).GetByRole(AriaRole.Radio).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            var revoke = Assert.Single(api.OfferRevokes(Northwind));
            var said = await page.EvaluateAsync<string[]>("() => window.__said || []");

            Assert.EndsWith(offerId.ToString("D"), revoke.Path, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await page.Locator($"{Section} input[type=radio]:checked").CountAsync());
            Assert.Contains("Ownership offer revoked.", said);
            Assert.DoesNotContain(said, text => text.Contains(recipient.DisplayName, StringComparison.Ordinal));
            Assert.False(await page.EvaluateAsync<bool>("() => document.activeElement === document.body"), "Focus fell to the body after Revoke.");
        }
    }

    /// <summary>
    /// An offer refused <c>409 ownership-offer-pending</c> (another tab offered first) re-reads and shows the pending offer
    /// the Space now holds, naming its recipient.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    public async Task An_offer_refused_because_one_is_pending_shows_the_pending_offer()
    {
        var api = SpaceWithMembers("owner", out var others);
        var (chosen, elsewhere) = (others[0], others[1]);
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenOwnershipAsync(api, page);
            await ChooseAndAskAsync(page, chosen.DisplayName);

            api.PendingOffers[Northwind] = (Guid.NewGuid(), elsewhere.Id, elsewhere.DisplayName, api.ServerNow.AddDays(OwnershipOfferWire.ExpiryDays), api.ServerNow);

            await page.Locator(Section).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = OfferAction, Exact = true }).ClickAsync();
            await PendingTextAsync(page, elsewhere.DisplayName);

            Assert.Single(api.OwnershipOffersSent(Northwind));
        }
    }

    /// <summary>
    /// An offer refused <c>400 invalid-offer-recipient</c> (the chosen Membership left meanwhile) says so in the section's own
    /// words and offers the picker again.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    public async Task An_offer_refused_for_its_recipient_says_why_and_offers_the_picker_again()
    {
        var api = SpaceWithMembers("owner", out var others);
        var gone = others[0];
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await OpenOwnershipAsync(api, page);
            await ChooseAndAskAsync(page, gone.DisplayName);

            api.SpaceMemberships[Northwind].RemoveAll(row => row.Id == gone.Id);
            api.SpaceAssignees[Northwind].RemoveAll(row => row.MembershipId == gone.Id);

            await page.Locator(Section).GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = OfferAction, Exact = true }).ClickAsync();
            await page.Locator(Section).GetByText(InvalidRecipientCopy, new LocatorGetByTextOptions { Exact = false }).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

            Assert.True(await page.Locator(Section).GetByRole(AriaRole.Radio).First.IsVisibleAsync(), "The picker is not offered again.");
        }
    }

    /// <summary>
    /// The section reflows at 320 by 256 CSS pixels with no sideways scroll, in both states, and still does with the text
    /// spacing overrides applied.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR41")]
    [Trait("Requirement", "NFR-9")]
    public async Task The_Ownership_section_reflows_at_320_by_256_with_text_spacing()
    {
        var api = SpaceWithMembers("owner", out var others);
        var recipient = others[2];
        var (context, page) = await surface.OpenAsync(EnglishLocale);

        await using (context)
        {
            await page.SetViewportSizeAsync(320, 256);
            await OpenOwnershipAsync(api, page);
            await page.Locator(Section).GetByRole(AriaRole.Radio).First.WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = TextSpacingOverride });

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));

            api.PendingOffers[Northwind] = (Guid.NewGuid(), recipient.Id, recipient.DisplayName, api.ServerNow.AddDays(OwnershipOfferWire.ExpiryDays), api.ServerNow);

            await page.ReloadAsync();
            await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
            await PendingTextAsync(page, recipient.DisplayName);
            await page.AddStyleTagAsync(new PageAddStyleTagOptions { Content = TextSpacingOverride });

            Assert.Empty(await page.EvaluateAsync<string[]>(Reflow));
        }
    }

    /// <summary>
    /// A signed-in Account holding <paramref name="northwindRole"/> in Northwind Redesign (with an Owner, an Admin, a Member
    /// and a Viewer) and Member of Harbour Ops. <paramref name="others"/> are Northwind's Memberships other than the Owner's.
    /// </summary>
    private static FakeYelloApi SpaceWithMembers(string northwindRole, out List<ListedMembership> others)
    {
        var api = new FakeYelloApi { SignedIn = true };

        api.Switcher.Add((Northwind, SpaceName));
        api.Switcher.Add((Harbour, "Harbour Ops"));
        api.Roles[Northwind] = northwindRole;
        api.Roles[Harbour] = "member";

        var owner = Row("Olu Owner", "owner", isYou: northwindRole == "owner");

        others = [Row("Ada Admin", "admin", isYou: northwindRole == "admin"), Row("Mo Member", "member"), Row("Vi Viewer", "viewer")];
        api.SpaceMemberships[Northwind] = [owner, .. others];
        api.SpaceAssignees[Northwind] = [.. api.SpaceMemberships[Northwind].Select(row => (row.Id, row.DisplayName))];

        return api;
    }

    /// <summary>
    /// A Membership as the list sends it, with a fresh address (never a shared one).
    /// </summary>
    private static ListedMembership Row(string displayName, string role, bool isYou = false) =>
        new(Guid.NewGuid(), displayName, $"member-{Guid.NewGuid():N}@example.test", role, isYou);

    /// <summary>
    /// Checks the radio named <paramref name="label"/> and presses "Offer ownership".
    /// </summary>
    private static async Task ChooseAndAskAsync(IPage page, string label)
    {
        var section = page.Locator(Section);

        await section.GetByRole(AriaRole.Radio, new LocatorGetByRoleOptions { Name = label, Exact = true }).CheckAsync();
        await section.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = OfferOwnership, Exact = true }).ClickAsync();
    }

    /// <summary>
    /// Waits until the section shows the pending offer to <paramref name="label"/>.
    /// </summary>
    private static Task PendingTextAsync(IPage page, string label) =>
        page.Locator(Section).GetByText($"Ownership offered to {label}. Waiting for their answer.", new LocatorGetByTextOptions { Exact = false })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });

    private static async Task<string[]> RadioNamesAsync(IPage page)
    {
        var names = new List<string>();

        foreach (var radio in await page.Locator(Section).GetByRole(AriaRole.Radio).AllAsync())
        {
            names.Add((await radio.EvaluateAsync<string>("e => (e.labels && e.labels[0] ? e.labels[0].textContent : e.getAttribute('aria-label') || '').trim()")).Trim());
        }

        return [.. names];
    }

    private string Address(string path) => new Uri(surface.BaseAddress, path).ToString();

    /// <summary>
    /// Opens Northwind and follows the context bar's "Space settings" link.
    /// </summary>
    private async Task OpenSettingsAsync(FakeYelloApi api, IPage page)
    {
        await api.InstallAsync(page);
        await page.GotoAsync(Address($"spaces/{Northwind:D}"));
        await page.Locator(RoleChip).WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
        await page.Locator(ContextBar).GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = SettingsLink, Exact = true }).ClickAsync();
    }

    /// <summary>
    /// Opens Space settings and waits for the Ownership heading.
    /// </summary>
    private async Task OpenOwnershipAsync(FakeYelloApi api, IPage page)
    {
        await OpenSettingsAsync(api, page);
        await page.Locator(Section).GetByRole(AriaRole.Heading, new LocatorGetByRoleOptions { Name = SectionHeading, Exact = true })
            .WaitForAsync(new LocatorWaitForOptions { Timeout = Timeout });
    }
}
