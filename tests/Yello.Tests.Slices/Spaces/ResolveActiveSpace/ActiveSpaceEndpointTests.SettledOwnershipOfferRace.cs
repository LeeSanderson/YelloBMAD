using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// A caller whose Membership ends while its request waits on the Space's Memberships lock. The pipeline judged the request
/// before the lock, so it reaches the store; there the caller's own Membership is read <b>before</b> the offer, and a caller
/// with none gets the recorded boundary <c>404</c> - never a <c>409</c>, which would tell an Account with no Membership that
/// the Space and the offer exist.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order is controlled, never hoped for.</b> The case holds <c>Yello/Memberships/{spaceId:D}</c> on a connection under
/// the Space's key, sends each request and waits until it is blocked on <c>LCK_M_%</c> before sending the next, so
/// <c>sp_getapplock</c>'s exclusive waiters are granted in the order sent. Then it commits. Never a delay. Every racing
/// Account is made present and holds its anti-forgery token before the lock is taken.
/// </para>
/// <para>
/// <b>Seeding</b> is as in <c>.AcceptOwnershipOfferRace.cs</c>: participants join through the product and the offer is made
/// through the offer route.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The recipient's decline queued behind an Admin's removal of the recipient: the removal runs first, <c>204</c>, and
    /// writes the offer <c>Lapsed</c>; the decline then finds no Membership of the caller and is the boundary <c>404
    /// not-available</c>, recorded <c>CrossSpace</c> - not <c>409</c>. No <c>Pending</c> offer names the missing Membership,
    /// and the Space holds one Owner. A store miss that decided "names the caller" before resolving the caller's own
    /// Membership answers <c>409</c> here.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "NFR-1")]
    [Trait("Requirement", "AR-13")]
    public async Task A_decline_queued_behind_the_removal_of_its_recipient_is_the_recorded_boundary_404()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (recipientAccount, recipientMembership) = (Guid.Empty, Guid.Empty);
        WireResponse? removed = null;
        WireResponse? declined = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("admin", DisplayName(), "admin"));
            var recipient = space.Members["recipient"];
            var admin = space.Members["admin"].Session;
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (recipientAccount, recipientMembership) = (recipient.AccountId, recipient.MembershipId);

            await MadePresentAsync(recipient.Session, spaceId);
            await MadePresentAsync(admin, spaceId);
            await recipient.Session.FetchAntiforgeryTokenAsync();
            await admin.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var removal = RemoveOverWireAsync(admin, spaceId, recipientMembership, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, removal, cancellationToken);

            var decline = DeclineOverWireAsync(recipient.Session, spaceId, offer.Id, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(removal, decline), cancellationToken);
            await held.CommitAsync(cancellationToken);

            (removed, declined) = (await removal, await decline);
        });

        var compared = $"removal: {Described(removed)}; decline: {Described(declined)}";

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, "The removal, queued first, commits. " + compared);
        Assert.True(declined?.StatusCode == HttpStatusCode.NotFound, "The decline after the removal is the boundary 404, never 409. " + compared);
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(declined));
        Assert.Contains(await RefusalsOfAsync(database, recipientAccount), row => row.TargetSpaceId == spaceId && row.Kind == "CrossSpace");
        Assert.Equal("Lapsed", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceId), row => row.Id == recipientMembership);
        await AssertNoPendingOfferNamesAMissingMembershipAsync(wire, spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The Owner's revoke queued behind the recipient's acceptance and the Owner's own leave. All three passed the pipeline
    /// under their pre-swap Roles. The acceptance runs first, <c>204</c>: the recipient is the Owner and the offerer an
    /// Admin. The leave runs next, as an Admin, <c>204</c>. The revoke then finds no Membership of its caller and is the
    /// boundary <c>404 not-available</c>, recorded <c>CrossSpace</c> - it must not be <c>409</c>, which would tell an Account
    /// with no Membership that the Space and the offer exist. The offer stays <c>Accepted</c> and the Space holds one Owner.
    /// </summary>
    /// <remarks>
    /// Should a run show the waiters granted out of the order sent, the develop phase records the observed order and makes the
    /// assertion order-tolerant: the revoke answers <c>404</c> whenever the leave committed before it, and is never a
    /// <c>409</c> to a non-member.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - the revoke has no LostMembership outcome yet, so a revoker whose Membership ended while it waited answers 409 for an offer that exists")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "NFR-1")]
    [Trait("Requirement", "AR-13")]
    public async Task A_revoke_queued_behind_the_acceptance_and_its_offerers_leave_is_the_recorded_boundary_404()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerAccount, recipientAccount) = (Guid.Empty, Guid.Empty);
        WireResponse[] raced = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerAccount, recipientAccount) = (space.OwnerAccountId, recipient.AccountId);

            await MadePresentAsync(space.Owner, spaceId);
            await MadePresentAsync(recipient.Session, spaceId);
            await space.Owner.FetchAntiforgeryTokenAsync();
            await recipient.Session.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var accept = AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, accept, cancellationToken);

            var leave = LeaveOverWireAsync(space.Owner, spaceId, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(accept, leave), cancellationToken);

            var revoke = RevokeOfferOverWireAsync(space.Owner, spaceId, offer.Id, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 3, Task.WhenAll(accept, leave, revoke), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(accept, leave, revoke);
        });

        var (accepted, left, revoked) = (raced[0], raced[1], raced[2]);
        var compared = $"accept: {Described(accepted)}; leave: {Described(left)}; revoke: {Described(revoked)}";
        var rows = await wire.MembershipRowsInAsync(spaceId);

        Assert.True(accepted.StatusCode == HttpStatusCode.NoContent, "The acceptance, queued first, commits. " + compared);
        Assert.True(left.StatusCode == HttpStatusCode.NoContent, "The leave runs after the swap, as an Admin. " + compared);
        Assert.True(revoked.StatusCode == HttpStatusCode.NotFound, "The revoke by an Account with no Membership is the boundary 404, never 409. " + compared);
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(revoked));
        Assert.Contains(await RefusalsOfAsync(database, ownerAccount), row => row.TargetSpaceId == spaceId && row.Kind == "CrossSpace");
        Assert.Equal("Accepted", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
        Assert.Equal("Owner", Assert.Single(rows, row => row.AccountId == recipientAccount).Role);
        Assert.DoesNotContain(rows, row => row.AccountId == ownerAccount);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }
}
