using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Acceptances under contention. The acceptance takes the Space's Memberships application lock
/// (<c>Yello/Memberships/{spaceId:D}</c>, Exclusive) before its guarded transition, as the offer, the revoke, the Role
/// change, the removal and the leave already do, so every case holds that lock on a connection under the Space's key, fires
/// both requests, waits on <c>LCK_M_%</c>, then commits. Never a delay. The outcome is always one of the serial orders, the
/// Space never holds zero or two Owners, and a removed Membership never holds <c>Owner</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seeding.</b> Participants join through the product and the offer is made through the offer route; then every racing
/// Account is made present in the Space and holds its anti-forgery token before the lock is taken, so nothing but the
/// application lock orders the pair.
/// </para>
/// <para>
/// <b>The deciding assertion is that both waited</b> (<see cref="TaskWire.WaitUntilBlockedOnALockAsync"/> times out
/// otherwise). The guarded statement alone would order some pairs; the waits show the lock is taken. Plant 5 (the acceptance
/// skips the lock) is expected to go red there, or is recorded as masked with its reason.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// Two acceptances of one offer by its recipient, under two keys, held on the Memberships lock until both are blocked:
    /// exactly one <c>204</c> and one <c>409 ownership-offer-not-pending</c>, never a <c>404</c> and never two <c>204</c>s. The
    /// recipient is the one Owner, the former Owner an Admin, the offer <c>Accepted</c>, and the run's log names the former
    /// Owner in exactly one publish.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-12")]
    [Trait("Requirement", "AR-16")]
    public async Task Two_acceptances_of_one_offer_held_on_the_Memberships_lock_commit_one_and_refuse_the_other_409()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerAccount, recipientAccount, bystanderAccount) = (Guid.Empty, Guid.Empty, Guid.Empty);
        WireResponse[] raced = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("bystander", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerAccount, recipientAccount, bystanderAccount) = (space.OwnerAccountId, recipient.AccountId, space.Members["bystander"].AccountId);

            await MadePresentAsync(recipient.Session, spaceId);
            await recipient.Session.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var first = AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, Guid.NewGuid().ToString("D"));
            var second = AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(first, second), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(first, second);
        });

        var compared = $"first: {Described(raced[0])}; second: {Described(raced[1])}";

        Assert.True(raced.Count(answer => answer.StatusCode == HttpStatusCode.NoContent) == 1, "Exactly one acceptance commits. " + compared);

        var loser = Assert.Single(raced, answer => answer.StatusCode != HttpStatusCode.NoContent);

        Assert.True(loser.StatusCode == HttpStatusCode.Conflict, "The other is 409, never 404. " + compared);
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(loser));
        Assert.Equal("Accepted", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
        Assert.Equal("Owner", (await wire.MembershipOfAsync(spaceId, recipientAccount)).Role);
        Assert.Equal("Admin", (await wire.MembershipOfAsync(spaceId, ownerAccount)).Role);
        Assert.Equal(1, PublishesNaming(log, ownerAccount, spaceId));
        Assert.Equal(PublishesNaming(log, bystanderAccount, spaceId) + 1, PublishesNaming(log, recipientAccount, spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The recipient's acceptance and the Owner's revoke of the same offer, held on the Memberships lock until both are
    /// blocked. Serial outcomes only: acceptance first - <c>204</c>, then the revoke's actor-still-Owner clause sees the swap
    /// and it is the recorded <c>403</c> naming <c>offer-ownership-transfer</c>, the offer <c>Accepted</c>, the recipient Owner.
    /// Revoke first - <c>204</c>, then the acceptance <c>409 ownership-offer-not-pending</c>, the offer <c>Revoked</c>, every
    /// Role as it was. Never both written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-1")]
    public async Task An_acceptance_and_the_Owners_revoke_held_on_the_Memberships_lock_end_in_a_serial_outcome()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerAccount, recipientAccount) = (Guid.Empty, Guid.Empty);
        IReadOnlyList<MembershipRow> before = [];
        WireResponse[] raced = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerAccount, recipientAccount) = (space.OwnerAccountId, recipient.AccountId);
            before = await wire.MembershipRowsInAsync(spaceId);

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
            var revoke = RevokeOfferOverWireAsync(space.Owner, spaceId, offer.Id, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(accept, revoke), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(accept, revoke);
        });

        var (accepted, revoked) = (raced[0], raced[1]);
        var compared = $"accept: {Described(accepted)}; revoke: {Described(revoked)}";
        var state = Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State;

        Assert.False(accepted.StatusCode == HttpStatusCode.NoContent && revoked.StatusCode == HttpStatusCode.NoContent, "Both were written. " + compared);

        if (accepted.StatusCode == HttpStatusCode.NoContent)
        {
            Assert.True(revoked.StatusCode == HttpStatusCode.Forbidden, "The revoke after the swap finds its actor no longer the Owner: the recorded 403. " + compared);
            Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(revoked));
            Assert.Equal(OwnershipOfferWire.CapabilityCode, CapabilityOf(revoked));
            Assert.True(state == "Accepted", "The acceptance ran first. " + compared);
            Assert.Equal("Owner", (await wire.MembershipOfAsync(spaceId, recipientAccount)).Role);
            Assert.Equal("Admin", (await wire.MembershipOfAsync(spaceId, ownerAccount)).Role);
        }
        else
        {
            Assert.True(revoked.StatusCode == HttpStatusCode.NoContent, "One of the two commits. " + compared);
            Assert.True(accepted.StatusCode == HttpStatusCode.Conflict, "The acceptance after the revoke is 409. " + compared);
            Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(accepted));
            Assert.True(state == "Revoked", "The revoke ran first. " + compared);
            Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        }

        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The recipient's acceptance and an Admin's removal of the recipient, held on the Memberships lock until both are
    /// blocked. Acceptance first: <c>204</c>, then the removal targets the Owner's row and is the recorded <c>403
    /// insufficient-role</c>; the recipient is the Owner. Removal first: <c>204</c>, the offer <c>Lapsed</c>, then the
    /// acceptance finds no Membership and is the pipeline's <c>404</c>, recorded <c>CrossSpace</c>. Never a removed Membership
    /// holding <c>Owner</c>, never zero Owners, and no <c>Pending</c> offer names a Membership that is gone.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-12")]
    public async Task An_acceptance_and_the_removal_of_its_recipient_held_on_the_Memberships_lock_end_in_a_serial_outcome()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (recipientAccount, recipientMembership) = (Guid.Empty, Guid.Empty);
        WireResponse[] raced = [];

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

            var accept = AcceptOverWireAsync(recipient.Session, spaceId, offer.Id, Guid.NewGuid().ToString("D"));
            var removal = RemoveOverWireAsync(admin, spaceId, recipientMembership, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(accept, removal), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(accept, removal);
        });

        var (accepted, removed) = (raced[0], raced[1]);
        var compared = $"accept: {Described(accepted)}; removal: {Described(removed)}";
        var rows = await wire.MembershipRowsInAsync(spaceId);

        if (accepted.StatusCode == HttpStatusCode.NoContent)
        {
            Assert.True(removed.StatusCode == HttpStatusCode.Forbidden, "The removal after the swap targets the Owner. " + compared);
            Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(removed));
            Assert.Equal("Owner", Assert.Single(rows, row => row.Id == recipientMembership).Role);
            Assert.Equal("Accepted", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
        }
        else
        {
            Assert.True(removed.StatusCode == HttpStatusCode.NoContent, "One of the two commits. " + compared);
            Assert.True(accepted.StatusCode == HttpStatusCode.NotFound, "The acceptance after the removal is the boundary 404. " + compared);
            Assert.Equal(ProblemTypes.NotAvailable, ProblemType(accepted));
            Assert.DoesNotContain(rows, row => row.Id == recipientMembership);
            Assert.Equal("Lapsed", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
            Assert.Contains(await RefusalsOfAsync(database, recipientAccount), row => row.TargetSpaceId == spaceId && row.Kind == "CrossSpace");
        }

        await AssertNoPendingOfferNamesAMissingMembershipAsync(wire, spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The recipient's acceptance and the Owner's leave, held on the Memberships lock until both are blocked. Leave first: the
    /// Owner still holds ownership, <c>409 ownership-held</c>, then the acceptance <c>204</c>. Acceptance first: <c>204</c>,
    /// then the former Owner, now an Admin, leaves <c>204</c>. Either way the recipient is the one Owner.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-26")]
    public async Task An_acceptance_and_the_Owners_leave_held_on_the_Memberships_lock_end_in_a_serial_outcome()
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
            var leave = LeaveOverWireAsync(space.Owner, spaceId, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(accept, leave), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(accept, leave);
        });

        var (accepted, left) = (raced[0], raced[1]);
        var compared = $"accept: {Described(accepted)}; leave: {Described(left)}";
        var rows = await wire.MembershipRowsInAsync(spaceId);

        Assert.True(accepted.StatusCode == HttpStatusCode.NoContent, "The acceptance commits in either order. " + compared);
        Assert.Equal("Owner", Assert.Single(rows, row => row.AccountId == recipientAccount).Role);

        if (left.StatusCode == HttpStatusCode.NoContent)
        {
            Assert.DoesNotContain(rows, row => row.AccountId == ownerAccount);
        }
        else
        {
            Assert.True(left.StatusCode == HttpStatusCode.Conflict, "The leave is 204 (after the swap) or 409 (before it). " + compared);
            Assert.Equal(ProblemTypes.OwnershipHeld, ProblemType(left));
            Assert.Equal("Admin", Assert.Single(rows, row => row.AccountId == ownerAccount).Role);
        }

        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// A former Owner's stale offer is a Role refusal, not a bad request. With an offer pending to M, M's acceptance and the
    /// Owner's offer to a third Member T are held on the Memberships lock until both are blocked; both passed the pipeline
    /// under their pre-swap Roles. Acceptance first: <c>204</c>, then the offer finds its actor no longer the Owner and is the
    /// recorded <c>403 insufficient-role</c> naming <c>offer-ownership-transfer</c> - not <c>400 invalid-offer-recipient</c> -
    /// and nothing is inserted. Offer first: the pending offer's index refuses it, <c>409 ownership-offer-pending</c>, then
    /// the acceptance <c>204</c>. Plant 9 (<c>NoLongerOwner</c> removed) turns the first order's <c>403</c> into <c>400</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "AD-26")]
    public async Task An_acceptance_and_the_Owners_offer_to_another_Member_held_on_the_Memberships_lock_end_in_a_serial_outcome()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerAccount, recipientAccount) = (Guid.Empty, Guid.Empty);
        var refusalsBefore = 0;
        WireResponse[] raced = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("third", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerAccount, recipientAccount) = (space.OwnerAccountId, recipient.AccountId);
            refusalsBefore = (await RefusalsOfAsync(database, ownerAccount)).Count;

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
            var onward = OfferOverWireAsync(space.Owner, spaceId, space.Members["third"].MembershipId, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(accept, onward), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(accept, onward);
        });

        var (accepted, offered) = (raced[0], raced[1]);
        var compared = $"accept: {Described(accepted)}; offer: {Described(offered)}";

        Assert.True(accepted.StatusCode == HttpStatusCode.NoContent, "The acceptance commits in either order. " + compared);
        Assert.Equal("Accepted", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
        Assert.Equal("Owner", (await wire.MembershipOfAsync(spaceId, recipientAccount)).Role);

        if (offered.StatusCode == HttpStatusCode.Forbidden)
        {
            Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(offered));
            Assert.Equal(OwnershipOfferWire.CapabilityCode, CapabilityOf(offered));

            var refusal = Assert.Single((await RefusalsOfAsync(database, ownerAccount)).Skip(refusalsBefore));

            Assert.Equal(nameof(Capability.OfferOwnershipTransfer), refusal.Capability);
        }
        else
        {
            Assert.True(offered.StatusCode == HttpStatusCode.Conflict, "The stale offer is the recorded 403 (after the swap) or 409 (before it), never 400. " + compared);
            Assert.Equal(ProblemTypes.OwnershipOfferPending, ProblemType(offered));
        }

        await AssertOneOwnerEachAsync(wire, spaceId);
    }
}
