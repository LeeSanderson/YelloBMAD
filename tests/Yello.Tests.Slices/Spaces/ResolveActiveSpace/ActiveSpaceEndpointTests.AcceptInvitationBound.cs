using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Invitations;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.3's bound: Memberships per Space is 100 (NFR-8), declared in <c>BoundRegistry</c> with
/// <c>BoundOperation.AcceptInvitation</c>, consulted by the pipeline on the accept - never on issue - inside the creating
/// transaction, after the token resolved to an actionable Invitation addressed to the caller (Q9 (a)). AC9; AR-30/AD-25,
/// UX-DR35.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.3 ATDD run (2026-11-01); activated by its dev phase (2026-11-02).</b>
/// </para>
/// <para>
/// <b>Seeding N Memberships in one Space</b> (<see cref="SeedSpaceHoldingMembershipsAsync"/>): the sibling the story asks
/// for beside <c>SeedMembershipsAsync</c> (one Account across Spaces). One <see cref="SpaceSeeding"/> block under the
/// Space's key: the <c>Space</c> row; the registered Owner's <c>Membership</c>; and for each other Member a fresh
/// <c>Account</c> (random address - <c>NormalizedEmailAddress</c> is unique; no password) with its <c>Member</c> Membership.
/// Constraints checked: <c>FK_Membership_Space_SpaceId</c> and <c>FK_Membership_Account_AccountId</c> (both principals
/// in the same unit of work, which EF orders first), <c>AK_Membership_SpaceId_AccountId</c> (one per Account),
/// <c>UX_Membership_SpaceId_Owner</c> (one Owner), and the BLOCK AFTER INSERT predicate (the unit's <c>SpaceId</c> key names
/// this Space). The B2-block style, <c>TESTING-CONVENTIONS.md:796-803</c>; no 99 HTTP registrations.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC9, AR-30, AD-25, NFR-8, UX-DR35 - in a Space holding 100 Memberships, an Invitation is still issued (<c>204</c>:
    /// issue consults no bound) but its acceptance is refused inside the transaction: <c>409 bound-reached</c> naming
    /// <c>memberships-per-space</c> and <c>100</c>; still 100 Memberships; the Invitation stays <c>Pending</c> (acceptable
    /// once a Membership is removed); nothing recorded as an <c>AccessRefusal</c>. Plant 6: the count skipped (101 rows).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "UX-DR35")]
    public async Task The_101st_Membership_is_refused_at_acceptance_409_and_the_Invitation_stays_pending()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        WireResponse? refused = null;
        var inviteeId = Guid.Empty;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteIntoFullSpaceAsync(client, database, wire, mail, "the-101st", InvitationWire.MembershipsPerSpace);

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            inviteeId = await AccountIdOfAsync(database, invited.Invitee);
            await invitee.FetchAntiforgeryTokenAsync();
            refused = await AcceptAsync(invitee, invited.Token);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"{refused?.StatusCode} {refused?.Body}");

        var problem = JsonSerializer.Deserialize<ProblemResponse>(refused!.Body, JsonOptions);

        Assert.Equal(ProblemTypes.BoundReached, problem?.Type);
        Assert.Equal(InvitationWire.MembershipsPerSpaceBoundCode, problem?.Bound);
        Assert.Equal(InvitationWire.MembershipsPerSpace, problem?.Limit);
        Assert.Equal(InvitationWire.MembershipsPerSpace, (await MembershipsInAsync(database, invited!.SpaceId)).Count);
        Assert.Equal("Pending", Assert.Single(await wire.InvitationsInAsync(invited.SpaceId)).State);
        Assert.Empty(await RefusalsOfAsync(database, inviteeId));
    }

    /// <summary>
    /// AC9's anchor - at 99 Memberships the same accept succeeds and makes 100, so the refusal at 100 is the bound's and not
    /// a route or a seed that never admits anyone.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "NFR-8")]
    public async Task At_99_Memberships_the_same_accept_succeeds_and_makes_100()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        WireResponse? accepted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteIntoFullSpaceAsync(client, database, wire, mail, "the-100th", InvitationWire.MembershipsPerSpace - 1);

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            await invitee.FetchAntiforgeryTokenAsync();
            accepted = await AcceptAsync(invitee, invited.Token);
        });

        Assert.True(accepted?.StatusCode == HttpStatusCode.OK, $"{accepted?.StatusCode} {accepted?.Body}");
        Assert.Equal(InvitationWire.MembershipsPerSpace, (await MembershipsInAsync(database, invited!.SpaceId)).Count);
        Assert.Equal("Accepted", Assert.Single(await wire.InvitationsInAsync(invited.SpaceId)).State);
    }

    /// <summary>
    /// AC9's race, Q8 (a) - two invitees accepting at 99, held on the Space's Invitations lock until both are blocked:
    /// exactly one <c>200</c> and one <c>409 bound-reached</c>; 100 Memberships; one Invitation <c>Accepted</c>, the other
    /// still <c>Pending</c>. Plant 5 or 6 (no lock, or no count): 101.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    [Trait("Requirement", "NFR-8")]
    public async Task Two_accepts_at_99_overlapped_on_the_lock_admit_one_and_refuse_the_other_at_the_bound()
    {
        var database = Available();
        var wire = Wire();
        Invited? first = null;
        WireResponse[] raced = [];

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            first = await InviteIntoFullSpaceAsync(client, database, wire, mail, "race-a", InvitationWire.MembershipsPerSpace - 1);

            var second = await InviteIntoAsync(first.From, wire, mail, InvitationWire.FreshAddress("race-b"), "member");
            var a = await RegisteredAndSignedInAsync(client, first.Invitee, DisplayName());
            var b = await RegisteredAndSignedInAsync(client, second.Invitee, DisplayName());

            await a.FetchAntiforgeryTokenAsync();
            await b.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(first.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, InvitationsLockOf(first.SpaceId), cancellationToken);

            var acceptingA = AcceptAsync(a, first.Token);
            var acceptingB = AcceptAsync(b, second.Token);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(acceptingA, acceptingB), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(acceptingA, acceptingB);
        });

        Assert.Single(raced, answer => answer.StatusCode == HttpStatusCode.OK);
        Assert.Equal(ProblemTypes.BoundReached, ProblemType(Assert.Single(raced, answer => answer.StatusCode == HttpStatusCode.Conflict)));
        Assert.Equal(InvitationWire.MembershipsPerSpace, (await MembershipsInAsync(database, first!.SpaceId)).Count);
        Assert.Equal(["Accepted", "Pending"], (await wire.InvitationsInAsync(first.SpaceId)).Select(row => row.State).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC9, Q9 (a), AD-23 - in a full Space, a holder of the token who is not its addressee is told AC7's <c>403</c>, and a
    /// holder of a revoked token AC6's uniform <c>404</c>; neither learns the Space is full. Anchor: the addressee meets the
    /// <c>409 bound-reached</c>. Plant 6 (the bound checked before the guard): both answer <c>409</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-23")]
    [Trait("Requirement", "AD-28")]
    public async Task In_a_full_Space_a_stranger_or_a_dead_token_never_learns_the_Space_is_full()
    {
        var database = Available();
        var wire = Wire();
        Invited? live = null;
        WireResponse? stranger = null;
        WireResponse? dead = null;
        WireResponse? addressee = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            live = await InviteIntoFullSpaceAsync(client, database, wire, mail, "full-live", InvitationWire.MembershipsPerSpace);

            var revoked = await InviteIntoAsync(live.From, wire, mail, InvitationWire.FreshAddress("full-revoked"), "member");

            await live.Owner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await live.Owner.SendWithHeadersAsync(HttpMethod.Delete, InvitationRoutes.InvitationOf(live.SpaceId, revoked.InvitationId), content: null, NoKey)).StatusCode);

            var other = await SignedInAsync(client, RandomAddress());
            var revokedAddressee = await RegisteredAndSignedInAsync(client, revoked.Invitee, DisplayName());
            var invitee = await RegisteredAndSignedInAsync(client, live.Invitee, DisplayName());

            await other.FetchAntiforgeryTokenAsync();
            await revokedAddressee.FetchAntiforgeryTokenAsync();
            await invitee.FetchAntiforgeryTokenAsync();

            stranger = await AcceptAsync(other, live.Token);
            dead = await AcceptAsync(revokedAddressee, revoked.Token);
            addressee = await AcceptAsync(invitee, live.Token);
        });

        Assert.True(stranger?.StatusCode == HttpStatusCode.Forbidden, $"{stranger?.StatusCode} {stranger?.Body}");
        Assert.Equal(ProblemTypes.InvitationForAnotherAccount, ProblemType(stranger!));
        Assert.True(dead?.StatusCode == HttpStatusCode.NotFound, $"{dead?.StatusCode} {dead?.Body}");
        Assert.Equal(ProblemTypes.InvitationNotValid, ProblemType(dead!));
        Assert.True(addressee?.StatusCode == HttpStatusCode.Conflict, $"The addressee's anchor: {addressee?.StatusCode} {addressee?.Body}");
        Assert.Equal(ProblemTypes.BoundReached, ProblemType(addressee!));
        Assert.Equal(InvitationWire.MembershipsPerSpace, (await MembershipsInAsync(database, live!.SpaceId)).Count);
    }

    /// <summary>
    /// An Owner registered and signed in, owning a seeded Space holding <paramref name="memberships"/> Memberships (itself
    /// and <paramref name="memberships"/> - 1 seeded Members), who issues one Invitation into it through 4.1's route - which
    /// consults no bound, so the issue succeeds even at 100.
    /// </summary>
    private static async Task<Invited> InviteIntoFullSpaceAsync(
        HttpClient client,
        RegistrationDatabase database,
        WireDatabase wire,
        MailDrop mail,
        string label,
        int memberships)
    {
        var ownerAddress = RandomAddress();
        var issuerName = DisplayName();
        var owner = await RegisteredAndSignedInAsync(client, ownerAddress, issuerName);
        var ownerId = await AccountIdOfAsync(database, ownerAddress);
        var spaceId = Guid.NewGuid();
        var spaceName = $"Crowded {label} {Suffix()}";

        await SeedSpaceHoldingMembershipsAsync(database, spaceId, spaceName, ownerId, memberships);

        Assert.Equal(memberships, (await MembershipsInAsync(database, spaceId)).Count);

        return await InviteIntoAsync(new Issuer(owner, ownerId, issuerName, spaceId, spaceName), wire, mail, InvitationWire.FreshAddress(label), "member");
    }

    /// <summary>
    /// A named Space holding exactly <paramref name="total"/> Memberships: the Owner's and <paramref name="total"/> - 1 fresh
    /// Accounts' as Members, in one unit of work under the Space's key. See the file's remarks for the constraints.
    /// </summary>
    private static Task SeedSpaceHoldingMembershipsAsync(RegistrationDatabase database, Guid spaceId, string name, Guid ownerId, int total)
    {
        var others = Enumerable.Range(0, total - 1).Select(_ => SpaceSeeding.NewAccount()).ToList();

        return SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Spaces.Add(NamedSpace(spaceId, name));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, ownerId, Role.Owner));

            foreach (var account in others)
            {
                context.Accounts.Add(account);
                context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, account.Id, Role.Member));
            }
        }, TestContext.Current.CancellationToken);
    }
}
