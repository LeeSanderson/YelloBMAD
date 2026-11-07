using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Memberships;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.4 over the wire: the Membership list, <c>GET /api/v1/spaces/{spaceId}/memberships</c> with an optional
/// <c>after</c> cursor (Q9 (a)), under <c>Capability.ChangeMembershipRole</c> (Lee's Q4 (a): Owner Allowed, Admin
/// Qualified, Member and Viewer refused by the pipeline), answered <c>MembershipPageResponse(Total, Memberships,
/// NextAfter)</c> - 50 rows a page, keyset on <c>(CreatedAt, Id)</c>, the true total (Q8 (a)). AC1, AC2, AC7; FR-13,
/// UX-DR14, UX-DR34, NFR-8, AD-23, I-17.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.4 ATDD run (2026-11-02).</b> Every case is skipped with the AC it waits
/// for. The route is <see cref="MembershipWire"/>'s ATDD stand-in until Task 3 declares <c>MembershipRoutes</c>.
/// </para>
/// <para>
/// <b>The 4.2/4.3 trap.</b> No path under <c>/memberships</c> is mapped today, so every one answers the boundary
/// <c>404 not-available</c>. A refusal or an absence asserted here is therefore anchored on a <c>200</c> on the same route in
/// the same case first; a scaffold activated before the route exists fails on that anchor, never passes vacuously.
/// </para>
/// <para>
/// <b>Seeding.</b> Bulk Spaces (60 and 100 Memberships) through <see cref="SeedSpaceHoldingMembershipsAsync"/> (4.3's shape:
/// <c>FK_Membership_Space_SpaceId</c>, <c>FK_Membership_Account_AccountId</c>, <c>AK_Membership_SpaceId_AccountId</c>, one
/// Owner under <c>UX_Membership_SpaceId_Owner</c>, unique random addresses, the BLOCK AFTER INSERT predicate satisfied by the
/// unit's Space key), and the Admin who lists beside them through <see cref="WireDatabase.AddMembershipAsync"/> (a 100-row
/// acceptance would bury the point; recorded). The Member and Viewer whose refusal is the point join through the product:
/// an Invitation at that Role, accepted (4.3), by <see cref="JoinedThroughTheProductAsync"/>.
/// </para>
/// <para>
/// <b>Not here:</b> the cross-Space rows on both surfaces (<c>IsolationCases.Memberships.cs</c>), the Role change
/// (<c>.ChangeMembershipRole.cs</c>), and the contract's exact property list as a reflection gate
/// (<c>RefusalBoundaryGateTests.Memberships.cs</c>).
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC1, Q8 (a), NFR-8 - the Owner and an Admin each walk every page of a 60-Membership Space: every page answers
    /// <c>200</c> with <c>Total = 60</c>; the first holds exactly 50 rows and a cursor, the last none; the pages' union is
    /// exactly the 60 stored Memberships, none repeated or skipped; each row's Role is the stored Role's stable code; and
    /// exactly one row - the caller's own - is <c>isYou</c>. Plant 16: the list unpaged (60 rows on one page).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "UX-DR14")]
    [Trait("Requirement", "NFR-8")]
    public async Task The_Owner_and_an_Admin_each_list_every_Membership_across_pages_with_the_true_total()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var (ownerId, adminId) = (Guid.Empty, Guid.Empty);
        IReadOnlyList<MembershipRow> stored = [];
        var walked = new Dictionary<string, IReadOnlyList<WireResponse>>(StringComparer.Ordinal);

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedSpaceHoldingMembershipsAsync(database, spaceId, "Listed " + Suffix(), ownerId, 59);

            var admin = await SignedInAsync(client, adminAddress);
            adminId = await AccountIdOfAsync(database, adminAddress);
            await wire.AddMembershipAsync(spaceId, adminId, "admin");

            stored = await wire.MembershipRowsInAsync(spaceId);
            walked["Owner"] = await EveryListPageAsync(owner, spaceId);
            walked["Admin"] = await EveryListPageAsync(admin, spaceId);
        });

        Assert.Equal(60, stored.Count);

        foreach (var (caller, pages) in walked)
        {
            var callerId = caller == "Owner" ? ownerId : adminId;

            Assert.All(pages, page => Assert.True(page.StatusCode == HttpStatusCode.OK, $"{caller}'s list: {Described(page)}"));

            var parsed = pages.Select(page => MembershipWire.ParsePage(page.Body)).ToList();
            var rows = parsed.SelectMany(page => page.Memberships).ToList();

            Assert.True(parsed.Count == 2, $"{caller}: expected two pages of 50 and 10; got {parsed.Count}: {string.Join(" | ", pages.Select(Described))}");
            Assert.Equal(MembershipWire.PageSize, parsed[0].Memberships.Count);
            Assert.NotNull(parsed[0].NextAfter);
            Assert.Null(parsed[^1].NextAfter);
            Assert.All(parsed, page => Assert.Equal(60, page.Total));
            Assert.Equal(rows.Count, rows.Select(row => row.Id).Distinct().Count());
            Assert.Equal(stored.Select(row => row.Id).Order(), rows.Select(row => row.Id).Order());
            Assert.All(rows, row => Assert.Equal(stored.Single(candidate => candidate.Id == row.Id).Role, StoredRole(row.Role)));
            Assert.Equal(stored.Single(row => row.AccountId == callerId).Id, Assert.Single(rows, row => row.IsYou).Id);
        }

        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// AC1, NFR-8 ("it pages rather than growing unbounded at the 100-Membership bound"), Q8 (a) - at exactly 100
    /// Memberships the first page holds 50 rows and a cursor, the second 50 and none, both say <c>Total = 100</c>, and the two
    /// pages share no row. Plant 16: one page of 100.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "UX-DR14")]
    public async Task At_exactly_100_Memberships_the_first_page_holds_50_and_a_cursor_and_the_second_50_and_none()
    {
        var database = Available();
        var ownerAddress = RandomAddress();
        var spaceId = Guid.NewGuid();
        IReadOnlyList<WireResponse> pages = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedSpaceHoldingMembershipsAsync(database, spaceId, "Full list " + Suffix(), await AccountIdOfAsync(database, ownerAddress), InvitationWire.MembershipsPerSpace);

            pages = await EveryListPageAsync(owner, spaceId);
        });

        Assert.All(pages, page => Assert.True(page.StatusCode == HttpStatusCode.OK, Described(page)));

        var parsed = pages.Select(page => MembershipWire.ParsePage(page.Body)).ToList();

        Assert.True(parsed.Count == 2, $"Expected two pages; got {parsed.Count}: {string.Join(" | ", pages.Select(Described))}");
        Assert.Equal([MembershipWire.PageSize, MembershipWire.PageSize], parsed.Select(page => page.Memberships.Count));
        Assert.NotNull(parsed[0].NextAfter);
        Assert.Null(parsed[1].NextAfter);
        Assert.All(parsed, page => Assert.Equal(InvitationWire.MembershipsPerSpace, page.Total));
        Assert.Empty(parsed[0].Memberships.Select(row => row.Id).Intersect(parsed[1].Memberships.Select(row => row.Id)));
    }

    /// <summary>
    /// AC1, Lee's Q4 (a), AD-20 [SECURITY] - the list is the Owner's and the Admins' only: a Member and a Viewer, each joined
    /// through the product, are refused by the pipeline with <c>403 insufficient-role</c> naming
    /// <c>change-membership-role</c>, each recorded once as <c>InsufficientRole</c> against this Space; a non-member gets the
    /// boundary <c>404</c>, byte-identical to a Space that does not exist, recorded <c>CrossSpace</c>. No refusal body carries
    /// an <c>@</c>. Anchor: the Owner's list of the same Space answers <c>200</c> first, so no refusal here is the unmapped
    /// route's <c>404</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "AD-23")]
    public async Task A_Member_and_a_Viewer_are_refused_403_recorded_and_a_non_member_meets_the_boundary_404()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress, viewerAddress, strangerAddress) = (RandomAddress(), RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Refused list " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            var member = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");
            var viewer = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), viewerAddress, DisplayName(), "viewer");
            var stranger = await SignedInAsync(client, strangerAddress);

            answers["anchor"] = await ListMembershipsAsync(owner, spaceId);
            answers["member"] = await ListMembershipsAsync(member, spaceId);
            answers["viewer"] = await ListMembershipsAsync(viewer, spaceId);
            answers["stranger"] = await ListMembershipsAsync(stranger, spaceId);
            answers["absent"] = await ListMembershipsAsync(stranger, Guid.NewGuid());
        });

        Assert.True(answers["anchor"].StatusCode == HttpStatusCode.OK, $"The Owner's anchor: {Described(answers["anchor"])}");

        foreach (var (lesser, address) in new[] { ("member", memberAddress), ("viewer", viewerAddress) })
        {
            var refused = answers[lesser];

            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"The {lesser}'s list: {Described(refused)}; the Owner's anchor was {Described(answers["anchor"])}");
            Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(refused));
            Assert.Equal(MembershipWire.CapabilityCode, CapabilityOf(refused));
            Assert.DoesNotContain("@", refused.Body, StringComparison.Ordinal);

            var refusals = await RefusalsOfAsync(database, await AccountIdOfAsync(database, address));

            Assert.Equal(
                [(nameof(Capability.ChangeMembershipRole), "InsufficientRole")],
                refusals.Where(row => row.TargetSpaceId == spaceId).Select(row => (row.Capability, row.Kind)));
        }

        Assert.True(answers["stranger"].StatusCode == HttpStatusCode.NotFound, $"The stranger's list: {Described(answers["stranger"])}");
        Assert.Equal(answers["absent"].Body, answers["stranger"].Body);
        Assert.Equal(answers["absent"].HeaderNames, answers["stranger"].HeaderNames);
        Assert.Contains(await RefusalsOfAsync(database, await AccountIdOfAsync(database, strangerAddress)), row => row.TargetSpaceId == spaceId && row.Kind == "CrossSpace");
    }

    /// <summary>
    /// AC1, AC2, AC7, Lee's Q4 (a) and Q5 (a), AD-23 - the page is exactly <c>total</c>, <c>memberships</c> and
    /// <c>nextAfter</c>; each row exactly <c>id</c>, <c>displayName</c>, <c>emailAddress</c>, <c>role</c> and <c>isYou</c>.
    /// The address is the Account's <b>as typed at registration</b> (mixed case), never its normalised form; neither Account's
    /// id appears anywhere in the body; and the display name is read live: renamed in the database, the next list shows the
    /// new name (Q5 (a): a current Membership's name is shared by definition). The Owner's own row is <c>isYou</c>, the
    /// Member's is not. Plant 11's runtime twin: an extra member on the row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR14")]
    [Trait("Requirement", "UX-DR34")]
    [Trait("Requirement", "AD-23")]
    public async Task Each_row_is_exactly_its_id_live_display_name_typed_address_Role_code_and_IsYou()
    {
        var database = Available();
        var wire = Wire();
        var ownerAddress = RandomAddress();
        var memberAddress = $"Mixed.Case-{Guid.NewGuid():N}@Example.Test";
        var (ownerName, memberName, renamed) = (DisplayName(), DisplayName(), DisplayName());
        var spaceId = Guid.NewGuid();
        var (ownerId, memberId) = (Guid.Empty, Guid.Empty);
        WireResponse? first = null;
        WireResponse? afterRename = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await RegisteredAndSignedInAsync(client, ownerAddress, ownerName);
            ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Shaped " + Suffix(), ownerId, Role.Owner);

            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, memberName, "member");
            memberId = await AccountIdOfAsync(database, memberAddress);

            first = await ListMembershipsAsync(owner, spaceId);

            await wire.SetDisplayNameAsync(memberId, renamed);

            afterRename = await ListMembershipsAsync(owner, spaceId);
        });

        Assert.True(first?.StatusCode == HttpStatusCode.OK, Described(first));

        var (pageMembers, rowMembers) = MembershipWire.MemberNames(first.Body);

        Assert.Equal(MembershipWire.PageMembers, pageMembers);
        Assert.Equal(2, rowMembers.Count);
        Assert.All(rowMembers, names => Assert.Equal(MembershipWire.RowMembers, names));

        var rows = MembershipWire.ParsePage(first.Body).Memberships;
        var ownRow = Assert.Single(rows, row => row.IsYou);
        var memberRow = Assert.Single(rows, row => !row.IsYou);

        Assert.Equal(("owner", ownerName, ownerAddress), (ownRow.Role, ownRow.DisplayName, ownRow.EmailAddress));
        Assert.Equal(("member", memberName), (memberRow.Role, memberRow.DisplayName));
        Assert.Equal(memberAddress, memberRow.EmailAddress, StringComparer.Ordinal);
        Assert.DoesNotContain(EmailAddressNormalisation.Normalise(memberAddress), first.Body, StringComparison.Ordinal);

        foreach (var accountId in new[] { ownerId, memberId })
        {
            Assert.DoesNotContain(accountId.ToString("D"), first.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(accountId.ToString("N"), first.Body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(afterRename?.StatusCode == HttpStatusCode.OK, Described(afterRename));
        Assert.Equal(renamed, Assert.Single(MembershipWire.ParsePage(afterRename.Body).Memberships, row => !row.IsYou).DisplayName);
    }

    /// <summary>
    /// AC2, NFR-6, AD-23, I-17 - neither a Member's address (as typed or normalised) nor either display name reaches the
    /// Host's log across a list and a committed Role change; the new log messages take ids only. Anchor: the log is the
    /// Host's (its listening line is in it), and both requests were served.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-23")]
    public async Task Neither_an_address_nor_a_display_name_reaches_the_Host_log_on_list_or_change()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), $"Logged.Not-{Guid.NewGuid():N}@Example.Test");
        var (ownerName, memberName) = (DisplayName(), DisplayName());
        var spaceId = Guid.NewGuid();
        var answers = new List<WireResponse>();

        using var mail = new MailDrop();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await RegisteredAndSignedInAsync(client, ownerAddress, ownerName);
            await SeedNamedSpaceAsync(database, spaceId, "Unlogged members " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, memberName, "member");

            var target = await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress));

            answers.Add(await ListMembershipsAsync(owner, spaceId));
            answers.Add(await ChangeRoleOverWireAsync(owner, spaceId, target.Id, "viewer"));
        });

        Assert.Contains(HostProcess.ListeningMarker, log, StringComparison.Ordinal);
        Assert.All(answers, answer => Assert.True(answer.StatusCode == HttpStatusCode.OK, Described(answer)));

        foreach (var secret in new[] { memberAddress, EmailAddressNormalisation.Normalise(memberAddress), ownerAddress, memberName, ownerName })
        {
            // Counted, never printed: a failure message that quoted the secret would itself be the leak.
            Assert.False(log.Contains(secret, StringComparison.OrdinalIgnoreCase), "The Host log holds an address or a display name (AD-23, I-17).");
        }
    }

    /// <summary>
    /// The list as the browser sends it: the Session's cookie, a <c>GET</c>, the cursor when one is given.
    /// </summary>
    private static Task<WireResponse> ListMembershipsAsync(WireSession session, Guid spaceId, string? after = null) =>
        session.SendWithHeadersAsync(HttpMethod.Get, MembershipRoutes.MembershipsOf(spaceId, after), content: null, NoKey, withAntiforgeryToken: false);

    /// <summary>
    /// The Role change as the browser sends it: the Session's cookie and anti-forgery header, <c>{ role }</c>, and an
    /// <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> ChangeRoleOverWireAsync(WireSession session, Guid spaceId, Guid membershipId, string? role, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Patch, MembershipRoutes.MembershipOf(spaceId, membershipId), MembershipWire.ChangeBody(role), KeyHeaders(key));

    /// <summary>
    /// Every page of the list, following <c>nextAfter</c> until it is absent; stops at the first answer that is not a
    /// <c>200</c> (returned as the last element, for the assertion to state) or after ten pages.
    /// </summary>
    private static async Task<IReadOnlyList<WireResponse>> EveryListPageAsync(WireSession session, Guid spaceId)
    {
        var pages = new List<WireResponse>();
        string? after = null;
        var more = true;

        while (more)
        {
            var page = await ListMembershipsAsync(session, spaceId, after);

            pages.Add(page);
            after = page.StatusCode == HttpStatusCode.OK ? MembershipWire.ParsePage(page.Body).NextAfter : null;
            more = after is not null && pages.Count < 10;
        }

        return pages;
    }

    /// <summary>
    /// A fresh Account registered with <paramref name="displayName"/> at <paramref name="address"/> and signed in, which
    /// joins the Space <paramref name="into"/> names at <paramref name="role"/> through the product: its issuer (an Owner
    /// or Admin there) issues an Invitation through 4.1's route, the token is read from the email, and the new Account
    /// accepts it through 4.3's route. Its anti-forgery token is fetched after sign-in.
    /// </summary>
    /// <returns>The joined Account's Session.</returns>
    private static async Task<WireSession> JoinedThroughTheProductAsync(
        HttpClient client,
        WireDatabase wire,
        MailDrop mail,
        (WireSession Issuer, Guid SpaceId) into,
        string address,
        string displayName,
        string role)
    {
        var (issuer, spaceId) = into;
        var joiner = await RegisteredAndSignedInAsync(client, address, displayName);

        await issuer.FetchAntiforgeryTokenAsync();

        var issued = await IssueAsync(issuer, spaceId, address, role);

        Assert.True(issued.StatusCode == HttpStatusCode.NoContent, $"Issuing through the product: {Described(issued)}");

        var token = InvitationWire.TokenIn(await mail.WaitForEmailToAsync(address));

        await joiner.FetchAntiforgeryTokenAsync();

        var accepted = await AcceptAsync(joiner, token);

        Assert.True(accepted.StatusCode == HttpStatusCode.OK, $"Accepting through the product: {Described(accepted)}");
        Assert.Single(await wire.InvitationsInAsync(spaceId), row => row.EmailAddress == address && row.State == "Accepted");

        return joiner;
    }

    /// <summary>
    /// The S-2 invariant over the case's Spaces (<see cref="WireDatabase.SpacesNotHoldingExactlyOneOwnerAsync"/>): every
    /// one still holds exactly one Owner Membership.
    /// </summary>
    private static async Task AssertOneOwnerEachAsync(WireDatabase wire, params Guid[] spaceIds)
    {
        var broken = await wire.SpacesNotHoldingExactlyOneOwnerAsync(spaceIds);

        Assert.True(broken.Count == 0, "S-2 (AR-12, AD-5): a Space does not hold exactly one Owner - " + string.Join("; ", broken));
    }

    /// <summary>
    /// The <c>capability</c> a <c>403</c> body names, or <c>null</c>.
    /// </summary>
    private static string? CapabilityOf(WireResponse response)
    {
        using var document = JsonDocument.Parse(response.Body);

        return document.RootElement.TryGetProperty("capability", out var capability) ? capability.GetString() : null;
    }

    /// <summary>
    /// The stored form of a Role's stable code: its name (<c>MembershipConfiguration</c>'s <c>HasConversion&lt;string&gt;</c>).
    /// </summary>
    private static string StoredRole(string code) => char.ToUpperInvariant(code[0]) + code[1..];
}
