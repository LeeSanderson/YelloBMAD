using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts.Invitations;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.1 over the wire: issuing an Invitation through <c>POST /api/v1/spaces/{spaceId}/invitations</c>, a Space-scoped
/// write the pipeline authorises by <c>Capability.IssueAndRevokeInvitations</c>. AC1-AC5, AC8 (the falsifiable half),
/// AC12, AC13; FR-10, AR-28/AD-23, AR-31/AD-27, NFR-6, I-17.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.1 ATDD run (2026-11-01).</b> Every case is skipped with the AC it
/// waits for. The route, the problem types and the fixed values are <see cref="InvitationWire"/>'s until
/// <c>Yello.Contracts</c> holds them; emails are read from the Host's drop folder (<see cref="MailDrop"/>, assumed Q13
/// (a) - the recording sender a Host started as a process allows).
/// </para>
/// <para>
/// <b>Seeding.</b> Accounts are registered through the product. Spaces are seeded by <see cref="SpaceSeeding"/> (the
/// <c>Space</c> and one <c>Membership</c> under that Space's key: <c>FK_Membership_Space_SpaceId</c>,
/// <c>FK_Membership_Account_AccountId</c>, <c>AK_Membership_SpaceId_AccountId</c> and the <c>BLOCK AFTER INSERT</c>
/// predicate hold). A second member is added by <see cref="WireDatabase.AddMembershipAsync"/> (same constraints;
/// <c>UX_Membership_SpaceId_Owner</c> holds because it never adds an Owner). An expired Invitation is the one row written
/// directly (<see cref="WireDatabase.AddInvitationAsync"/>; R12) - its constraints are listed there.
/// </para>
/// <para>
/// <b>Not here:</b> AC5's duration (the isolation suite's timing row <c>ISO-TIMING-ISSUE-INVITATION-NO-ACCOUNT-ORACLE</c>,
/// which reads the Host's own elapsed time), the outbox and the email (<c>.IssueInvitationOutbox.cs</c>), the lock race
/// (<c>.IssueInvitationRace.cs</c>) and the schema (<c>.IssueInvitationSchema.cs</c>).
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    private const string IssueInvitationCapabilityCode = "issue-and-revoke-invitations";

    private static readonly string[] OfferableRoles = ["admin", "member", "viewer"];

    /// <summary>
    /// AC1, AC3, AC8, AC12, FR-10, Q2 (Lee) - an Owner and an Admin each offer Admin, Member and Viewer: each answer is
    /// <c>204</c> with no body and no <c>Location</c> (Q5 (a)), and each row carries exactly the resolved Space, the
    /// offered Role, <c>Pending</c>, the address verbatim and normalised, the issuer's Account and display name, a
    /// 32-byte token hash, and an <c>ExpiresAt</c> seven days after the database clock at insert.
    /// </summary>
    /// <param name="issuer">The issuer's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(Role.Owner)]
    [InlineData(Role.Admin)]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-9")]
    public async Task An_Owner_or_an_Admin_issues_an_Invitation_carrying_one_Space_and_one_Role_fixed_at_issue(Role issuer)
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var displayName = DisplayName();
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;
        var invited = OfferableRoles.ToDictionary(role => role, role => " " + InvitationWire.FreshAddress(role).ToUpperInvariant() + " ", StringComparer.Ordinal);
        var answers = new List<WireResponse>();
        using var mail = new MailDrop();

        var before = await wire.DatabaseNowAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, displayName);
            accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Inviting " + Suffix(), accountId, issuer);

            await session.FetchAntiforgeryTokenAsync();

            foreach (var (role, invitee) in invited)
            {
                answers.Add(await IssueAsync(session, spaceId, invitee, role));
            }
        });

        var after = await wire.DatabaseNowAsync();

        Assert.All(answers, answer =>
        {
            Assert.True(answer.StatusCode == HttpStatusCode.NoContent, $"{answer.StatusCode} {answer.Body}");
            Assert.Equal(string.Empty, answer.Body);
            Assert.Null(answer.Header("Location"));
        });

        var rows = await wire.InvitationsInAsync(spaceId);

        Assert.Equal(3, rows.Count);

        foreach (var (role, invitee) in invited)
        {
            var row = Assert.Single(rows, row => row.EmailAddress == invitee.Trim() || row.EmailAddress == invitee);

            Assert.Equal(spaceId, row.SpaceId);
            Assert.Equal(char.ToUpperInvariant(role[0]) + role[1..], row.Role);
            Assert.Equal("Pending", row.State);
            Assert.Equal(EmailAddressNormalisation.Normalise(invitee), row.NormalizedEmailAddress);
            Assert.Equal(accountId, row.IssuedByAccountId);
            Assert.Equal(displayName, row.IssuerName);
            Assert.Equal(32, row.TokenHash.Length);
            Assert.InRange(row.ExpiresAt, before.AddDays(InvitationWire.ExpiryDays), after.AddDays(InvitationWire.ExpiryDays));
        }

        Assert.Equal(3, rows.Select(row => Convert.ToHexString(row.TokenHash)).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// AC1, FR-10, AR-11 - a Member or a Viewer is refused <c>403 insufficient-role</c> naming
    /// <c>issue-and-revoke-invitations</c>; the pipeline records one <c>InsufficientRole</c> refusal; no Invitation and no
    /// outbox row is written. Anchor: the same caller reads the Space first (<c>200</c>).
    /// </summary>
    /// <param name="role">The caller's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(Role.Member)]
    [InlineData(Role.Viewer)]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "AR-11")]
    public async Task A_Member_or_a_Viewer_is_refused_403_and_nothing_is_written(Role role)
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("refused");
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;
        WireResponse? read = null;
        WireResponse? refused = null;
        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Refusing " + Suffix(), accountId, role);

            read = await session.GetAsync(SpaceRoutes.ActiveOf(spaceId));

            await session.FetchAntiforgeryTokenAsync();
            refused = await IssueAsync(session, spaceId, invitee, "member");
        });

        Assert.Equal(HttpStatusCode.OK, read?.StatusCode);
        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"{refused?.StatusCode} {refused?.Body}");

        var problem = JsonSerializer.Deserialize<ProblemResponse>(refused!.Body, JsonOptions);

        Assert.Equal(ProblemTypes.InsufficientRole, problem?.Type);
        Assert.Equal(IssueInvitationCapabilityCode, problem?.Capability);
        Assert.DoesNotContain(invitee, refused.Body, StringComparison.OrdinalIgnoreCase);

        var recorded = Assert.Single(await RefusalsOfAsync(database, accountId));

        Assert.Equal((spaceId, "InsufficientRole", "IssueAndRevokeInvitations"), (recorded.TargetSpaceId, recorded.Kind, recorded.Capability));
        Assert.Empty(await wire.InvitationsInAsync(spaceId));
        Assert.DoesNotContain(await wire.OutboxRowsAsync(), row => string.Equals(row.Recipient, invitee, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(await mail.ReadAllAsync(), email => string.Equals(email.To, invitee, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AC1, AD-3, AR-10 - a non-member's issue is the boundary <c>404 not-available</c>, byte for byte the answer for a
    /// Space that does not exist, with one <c>CrossSpace</c> refusal; nothing is written in the foreign Space and no
    /// outbox row exists for the address.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AR-10")]
    public async Task A_non_members_issue_is_the_boundary_404_and_nothing_is_written()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("foreign");
        var foreign = Guid.NewGuid();
        var foreignName = "Foreign " + Suffix();
        var accountId = Guid.Empty;
        WireResponse? refused = null;
        WireResponse? absent = null;
        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
            await SeedForeignSpaceAsync(database, foreign, foreignName);

            await session.FetchAntiforgeryTokenAsync();
            refused = await IssueAsync(session, foreign, invitee, "member");
            absent = await IssueAsync(session, Guid.NewGuid(), invitee, "member");
        });

        Assert.Equal(HttpStatusCode.NotFound, refused?.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(refused!));
        Assert.Equal(absent!.Body, refused!.Body);
        Assert.Equal(absent.HeaderNames, refused.HeaderNames);
        Assert.DoesNotContain(foreignName, refused.Body, StringComparison.Ordinal);

        Assert.Contains(await RefusalsOfAsync(database, accountId), row => row.TargetSpaceId == foreign && row.Kind == "CrossSpace");
        Assert.Empty(await wire.InvitationsInAsync(foreign));
        Assert.DoesNotContain(await wire.OutboxRowsAsync(), row => string.Equals(row.Recipient, invitee, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(await mail.ReadAllAsync(), email => string.Equals(email.To, invitee, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AC2, FR-10, FR-42 - <c>owner</c> is refused <c>400 role-not-offerable</c> whatever the client offered, and so (assumed)
    /// are an unknown, an empty and a missing Role; nothing is written. Anchor: the same caller's <c>member</c> issue is
    /// <c>204</c>, so the refusal is the Role's.
    /// </summary>
    /// <param name="role">The submitted Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("owner")]
    [InlineData("superuser")]
    [InlineData("")]
    [InlineData(null)]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "FR-42")]
    public async Task Owner_is_never_offerable_and_an_unknown_or_missing_Role_is_refused_400(string? role)
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var (refusedInvitee, acceptedInvitee) = (InvitationWire.FreshAddress("owner"), InvitationWire.FreshAddress("anchor"));
        var spaceId = Guid.NewGuid();
        WireResponse? refused = null;
        WireResponse? accepted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Offering " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            refused = await IssueAsync(session, spaceId, refusedInvitee, role);
            accepted = await IssueAsync(session, spaceId, acceptedInvitee, "member");
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.BadRequest, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(ProblemTypes.RoleNotOfferable, ProblemType(refused!));
        Assert.Equal(HttpStatusCode.NoContent, accepted?.StatusCode);

        var rows = await wire.InvitationsInAsync(spaceId);

        Assert.Equal([acceptedInvitee], rows.Select(row => row.EmailAddress));
        Assert.DoesNotContain(rows, row => row.Role == "Owner");
    }

    /// <summary>
    /// AC3, FR-10 - any plausible address is invited: no domain restriction, no allowlist, no Account needed. The edges
    /// are registration's rule's: a sub-domain and a tag, non-ASCII on both sides of the <c>@</c>, and exactly 254
    /// characters.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-10")]
    public async Task Any_plausible_address_is_invited_with_no_domain_restriction_and_no_Account_needed()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var longest = new string('l', InvitationWire.EmailAddressMaxLength - "@example.test".Length - suffix.Length) + suffix + "@example.test";
        string[] invitees =
        [
            $"first.last+tag-{suffix}@mail.sub.example.co.uk",
            $"ünïcödé-{suffix}@exämple.test",
            $"{suffix}@localhost-free-domain.invalid",
            longest,
        ];
        var statuses = new List<HttpStatusCode>();

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Anyone " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();

            foreach (var invitee in invitees)
            {
                statuses.Add((await IssueAsync(session, spaceId, invitee, "viewer")).StatusCode);
            }

            // Sent before this Host stops: an outbox row left behind is dispatched by the next case's Host on the same
            // database, into that case's drop.
            _ = await mail.WaitForAsync(invitees.Length);
        });

        Assert.Equal(InvitationWire.EmailAddressMaxLength, longest.Length);
        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.NoContent, status));
        Assert.Equal(invitees.Order(StringComparer.Ordinal), (await wire.InvitationsInAsync(spaceId)).Select(row => row.EmailAddress).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC3 - an implausible address is refused <c>400</c> with a stable type (assumed <c>invalid-email-address</c>), the
    /// body does not echo it, and nothing is written. The cases are registration's rule's edges: no <c>@</c>, two, one at
    /// either end, whitespace, a control and a Format character, 255 characters, blank and missing. Anchor: a plausible
    /// address in the same Space is <c>204</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-10")]
    public async Task An_implausible_address_is_refused_400_and_nothing_is_written()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();
        var anchor = InvitationWire.FreshAddress("plausible");
        string?[] implausible =
        [
            null,
            string.Empty,
            "   ",
            "no-at-sign.example.test",
            "@example.test",
            "person@",
            "two@@example.test",
            "a b@example.test",
            "tab\t@example.test",
            "zero​width@example.test",
            new string('x', InvitationWire.EmailAddressMaxLength + 1 - "@example.test".Length) + "@example.test",
        ];
        var refusals = new List<WireResponse>();
        WireResponse? accepted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Plausible " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();

            foreach (var invitee in implausible)
            {
                refusals.Add(await IssueAsync(session, spaceId, invitee, "member"));
            }

            accepted = await IssueAsync(session, spaceId, anchor, "member");
        });

        Assert.All(refusals, refusal =>
        {
            Assert.True(refusal.StatusCode == HttpStatusCode.BadRequest, $"{refusal.StatusCode} {refusal.Body}");
            Assert.Equal(ProblemTypes.InvalidEmailAddress, ProblemType(refusal));
            Assert.DoesNotContain("example.test", refusal.Body, StringComparison.Ordinal);
        });

        Assert.Equal(HttpStatusCode.NoContent, accepted?.StatusCode);
        Assert.Equal([anchor], (await wire.InvitationsInAsync(spaceId)).Select(row => row.EmailAddress));
    }

    /// <summary>
    /// AC4, FR-10 - an address that already holds a Membership in this Space is refused <c>409 already-a-member</c>,
    /// whatever its case and surrounding whitespace (one normalisation, one collation), and so is the issuer's own; nothing
    /// is written. Anchor: a fresh address in the same Space is <c>204</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-10")]
    public async Task An_address_already_a_Member_of_this_Space_is_refused_409_already_a_member()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var anchor = InvitationWire.FreshAddress("anchor");
        var spaceId = Guid.NewGuid();
        WireResponse? member = null;
        WireResponse? self = null;
        WireResponse? accepted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Members " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            _ = await SignedInAsync(client, memberAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, memberAddress), "viewer");

            await session.FetchAntiforgeryTokenAsync();
            member = await IssueAsync(session, spaceId, "  " + memberAddress.ToUpperInvariant() + " ", "member");
            self = await IssueAsync(session, spaceId, ownerAddress, "admin");
            accepted = await IssueAsync(session, spaceId, anchor, "member");
        });

        foreach (var refused in new[] { member, self })
        {
            Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"{refused?.StatusCode} {refused?.Body}");
            Assert.Equal(ProblemTypes.AlreadyAMember, ProblemType(refused!));
        }

        Assert.Equal(HttpStatusCode.NoContent, accepted?.StatusCode);
        Assert.Equal([anchor], (await wire.InvitationsInAsync(spaceId)).Select(row => row.EmailAddress));
    }

    /// <summary>
    /// AC4, AC5 - a Membership in ANOTHER Space is not a Membership here: an address registered as an Account and a
    /// member elsewhere is invited like any other (<c>204</c>, one row). The refusal depends on Membership in this Space,
    /// never on whether an Account exists anywhere.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "AD-23")]
    public async Task A_member_of_another_Space_is_invited_like_anyone_else()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, elsewhereAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        WireResponse? answer = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, ownerAddress);
            var ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Here " + Suffix(), ownerId, Role.Owner);

            _ = await SignedInAsync(client, elsewhereAddress);

            await session.FetchAntiforgeryTokenAsync();
            answer = await IssueAsync(session, spaceId, elsewhereAddress, "member");
        });

        Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal([elsewhereAddress], (await wire.InvitationsInAsync(spaceId)).Select(row => row.EmailAddress));
    }

    /// <summary>
    /// AC5, I-7, AD-23 (bytes; the duration is the isolation suite's timing row) - issuing to an address with no Account
    /// and to one registered as an Account answers identically: status, body, content type and header names. And the work
    /// does not branch: each gets exactly one email, and the two emails are the same text once the token is set aside.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "AR-28")]
    [Trait("Requirement", "AD-23")]
    public async Task The_answer_and_the_email_are_the_same_whether_or_not_the_address_has_an_Account()
    {
        var database = Available();
        var (ownerAddress, registered) = (RandomAddress(), RandomAddress());
        var nobody = InvitationWire.FreshAddress("nobody");
        var spaceId = Guid.NewGuid();
        WireResponse? toNobody = null;
        WireResponse? toAccount = null;
        DroppedEmail? first = null;
        DroppedEmail? second = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Oracle " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);

            _ = await SignedInAsync(client, registered);

            await session.FetchAntiforgeryTokenAsync();
            toNobody = await IssueAsync(session, spaceId, nobody, "member");
            toAccount = await IssueAsync(session, spaceId, registered, "member");

            // By recipient, not by count: the drop can also hold mail an earlier case's Host left in the shared outbox.
            first = await mail.WaitForEmailToAsync(nobody);
            second = await mail.WaitForEmailToAsync(registered);
        });

        Assert.Equal(HttpStatusCode.NoContent, toNobody?.StatusCode);
        Assert.Equal(toNobody!.StatusCode, toAccount?.StatusCode);
        Assert.Equal(toNobody.Body, toAccount!.Body);
        Assert.Equal(toNobody.ContentType, toAccount.ContentType);
        Assert.Equal(toNobody.HeaderNames, toAccount.HeaderNames);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Subject, second.Subject);
        Assert.Equal(WithoutToken(first.PlainText), WithoutToken(second.PlainText));
    }

    /// <summary>
    /// AC13, Q12 (a), AD-27 - a live pending Invitation for the same address (compared normalised) is refused
    /// <c>409 invitation-pending</c>; one row and one email exist.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "AD-27")]
    public async Task A_live_pending_Invitation_for_the_same_address_is_refused_409_invitation_pending()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("twice");
        var spaceId = Guid.NewGuid();
        WireResponse? first = null;
        WireResponse? again = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Pending " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            first = await IssueAsync(session, spaceId, invitee, "member");
            again = await IssueAsync(session, spaceId, invitee.ToUpperInvariant(), "viewer");

            _ = await mail.WaitForEmailToAsync(invitee);
        });

        Assert.Equal(HttpStatusCode.NoContent, first?.StatusCode);
        Assert.True(again?.StatusCode == HttpStatusCode.Conflict, $"{again?.StatusCode} {again?.Body}");
        Assert.Equal(ProblemTypes.InvitationPending, ProblemType(again!));
        Assert.Single(await wire.InvitationsInAsync(spaceId));
        Assert.Single(await mail.ReadAllAsync(), email => string.Equals(email.To, invitee, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AC8, AC13, FR-39, AD-27, R12 - the shared predicate is <c>State = Pending AND ExpiresAt &gt; now</c> by the database
    /// clock: an Invitation whose <c>ExpiresAt</c> has passed does not block a reissue (FR-39: "must be reissued"), while
    /// one an hour from expiry still does. The expired row is kept, still stored <c>Pending</c> - never <c>Expired</c>,
    /// never deleted (AD-27). Both seeded rows are the R12 method: <see cref="WireDatabase.AddInvitationAsync"/>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-39")]
    [Trait("Requirement", "AR-31")]
    [Trait("Requirement", "AD-27")]
    public async Task An_expired_pending_Invitation_does_not_block_a_reissue_and_a_live_one_does()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var (lapsed, live) = (InvitationWire.FreshAddress("lapsed"), InvitationWire.FreshAddress("live"));
        var spaceId = Guid.NewGuid();
        WireResponse? reissued = null;
        WireResponse? blocked = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            var ownerId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Expiry " + Suffix(), ownerId, Role.Owner);

            var now = await wire.DatabaseNowAsync();

            await wire.AddInvitationAsync(spaceId, ownerId, "Seeder", lapsed, EmailAddressNormalisation.Normalise(lapsed), "Member", now.AddMinutes(-1));
            await wire.AddInvitationAsync(spaceId, ownerId, "Seeder", live, EmailAddressNormalisation.Normalise(live), "Member", now.AddHours(1));

            await session.FetchAntiforgeryTokenAsync();
            reissued = await IssueAsync(session, spaceId, lapsed, "member");
            blocked = await IssueAsync(session, spaceId, live, "member");
        });

        Assert.True(reissued?.StatusCode == HttpStatusCode.NoContent, $"{reissued?.StatusCode} {reissued?.Body}");
        Assert.Equal(HttpStatusCode.Conflict, blocked?.StatusCode);
        Assert.Equal(ProblemTypes.InvitationPending, ProblemType(blocked!));

        var rows = await wire.InvitationsInAsync(spaceId);

        Assert.Equal(2, rows.Count(row => row.EmailAddress == lapsed));
        Assert.All(rows, row => Assert.Equal("Pending", row.State));
    }

    /// <summary>
    /// AC6, AD-18 - a replay with the same <c>Idempotency-Key</c> answers the original <c>204</c> and writes no second
    /// Invitation, no second outbox row and no second email. Anchor: a later issue to another address IS sent, so the
    /// count of one is not a dispatcher that never ran.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-18")]
    public async Task A_replay_with_the_same_Idempotency_Key_writes_one_Invitation_and_sends_one_email()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var (invitee, later) = (InvitationWire.FreshAddress("replayed"), InvitationWire.FreshAddress("later"));
        var key = Guid.NewGuid().ToString("D");
        var spaceId = Guid.NewGuid();
        WireResponse? original = null;
        WireResponse? replay = null;
        IReadOnlyList<DroppedEmail> emails = [];

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Replay " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            original = await IssueAsync(session, spaceId, invitee, "member", key);
            replay = await IssueAsync(session, spaceId, invitee, "member", key);

            _ = await mail.WaitForEmailToAsync(invitee);
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, later, "member")).StatusCode);

            _ = await mail.WaitForEmailToAsync(later);
            emails = await mail.ReadAllAsync();
        });

        Assert.Equal(HttpStatusCode.NoContent, original?.StatusCode);
        Assert.Equal(original!.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.Single(await wire.InvitationsInAsync(spaceId), row => row.EmailAddress == invitee);
        Assert.Single(emails, email => email.To == invitee);
        Assert.Single(emails, email => email.To == later);
    }

    /// <summary>
    /// AC12, Q1 (Lee: cascade), FR-7 - deleting a Space deletes its Invitations in the one deletion statement. Anchor: the
    /// Invitation exists before the deletion.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "FR-10")]
    public async Task Deleting_a_Space_deletes_its_Invitations()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("cascade");
        var spaceId = Guid.NewGuid();
        var beforeDeletion = 0;
        WireResponse? deleted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Doomed " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);

            beforeDeletion = (await wire.InvitationsInAsync(spaceId)).Count;
            deleted = await DeleteSpaceOverWireAsync(session, spaceId, key: null);
        });

        Assert.Equal(1, beforeDeletion);
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Empty(await wire.InvitationsInAsync(spaceId));
    }

    /// <summary>
    /// AC12, recorded rather than prevented (story 4.1 code review): the outbox carries no Space reference (Lee's Q6), so
    /// an email still unsent when its Space is deleted stays in the outbox and may yet be delivered; its link then answers
    /// 4.3's uniform "no longer valid". The row is seeded as claimed a minute ago, so no sweep sends it during the case.
    /// Anchor: the same deletion removes the Space's Invitation.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "AD-10")]
    public async Task An_unsent_outbox_row_survives_its_Spaces_deletion()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("cascade-mail");
        var spaceId = Guid.NewGuid();
        var outboxId = Guid.Empty;
        WireResponse? deleted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Doomed " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);

            outboxId = await wire.AddOutboxRowAsync(invitee, "Unsent " + Suffix(), "Still waiting", claimedMinutesAgo: 1);
            deleted = await DeleteSpaceOverWireAsync(session, spaceId, key: null);
        });

        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Empty(await wire.InvitationsInAsync(spaceId));
        Assert.Single(await wire.OutboxRowsAsync(), row => row.Id == outboxId);
    }

    /// <summary>
    /// AC5, NFR-6, I-17 - neither the invited address (as submitted or normalised), nor the token, nor the Space's or the
    /// issuer's name reaches the Host's log, across an issue, a refused re-issue and a refused Owner offer. Anchor: the
    /// log is the Host's (its listening line is in it), and the token is read from the email.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-23")]
    public async Task Neither_the_invited_address_nor_the_token_reaches_the_Host_log()
    {
        var database = Available();
        var address = RandomAddress();
        var displayName = DisplayName();
        var invitee = InvitationWire.FreshAddress("logged");
        var spaceName = "Unlogged " + Suffix();
        var spaceId = Guid.NewGuid();
        DroppedEmail? email = null;

        using var mail = new MailDrop();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, displayName);
            await SeedNamedSpaceAsync(database, spaceId, spaceName, await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await IssueAsync(session, spaceId, InvitationWire.FreshAddress("owner"), "owner")).StatusCode);

            email = await mail.WaitForEmailToAsync(invitee);
        });

        Assert.Contains(HostProcess.ListeningMarker, log, StringComparison.Ordinal);

        foreach (var secret in new[] { invitee, EmailAddressNormalisation.Normalise(invitee), TokenOf(email!), spaceName, displayName })
        {
            Assert.DoesNotContain(secret, log, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The issue as the browser sends it: the Session's cookie and anti-forgery header, the body
    /// <c>{emailAddress, role}</c>, and an <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> IssueAsync(WireSession session, Guid spaceId, string? emailAddress, string? role, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Post, InvitationRoutes.InvitationsOf(spaceId), InvitationWire.Body(emailAddress, role), KeyHeaders(key));

    /// <summary>
    /// The raw-SQL helper on this class's own database.
    /// </summary>
    private WireDatabase Wire() => new(fixture.ConnectionString!);

    /// <summary>
    /// The token in an invitation email: the last segment of its one <c>{ClientOrigin}/invitations/{token}</c> link.
    /// </summary>
    private static string TokenOf(DroppedEmail email)
    {
        var link = ClientOrigin + InvitationWire.LinkPath;
        var start = email.PlainText.IndexOf(link, StringComparison.Ordinal);

        Assert.True(start >= 0, $"The email carries no {link} link:\n{email.PlainText}");

        var token = new string([.. email.PlainText[(start + link.Length)..].TakeWhile(character => !char.IsWhiteSpace(character))]);

        Assert.NotEmpty(token);

        return token;
    }

    /// <summary>
    /// The email text with its token replaced by a fixed marker, so two emails compare by everything else.
    /// </summary>
    private static string WithoutToken(string plainText)
    {
        var link = ClientOrigin + InvitationWire.LinkPath;
        var start = plainText.IndexOf(link, StringComparison.Ordinal);

        if (start < 0)
        {
            return plainText;
        }

        var tokenStart = start + link.Length;
        var tokenEnd = tokenStart;

        while (tokenEnd < plainText.Length && !char.IsWhiteSpace(plainText[tokenEnd]))
        {
            tokenEnd++;
        }

        return string.Concat(plainText.AsSpan(0, tokenStart), "{token}", plainText.AsSpan(tokenEnd));
    }
}
