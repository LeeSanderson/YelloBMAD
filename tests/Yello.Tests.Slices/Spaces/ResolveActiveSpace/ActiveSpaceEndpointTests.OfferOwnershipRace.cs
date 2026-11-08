using System.Net;
using Xunit;
using Yello.Tests.Shared;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Offers under contention. The offer and the revoke take the Space's Memberships application lock
/// (<c>Yello/Memberships/{spaceId:D}</c>, Exclusive) before their guarded statement, as removal and leaving already do, so
/// every case holds that lock on a connection under the Space's key, fires the requests, waits on <c>LCK_M_%</c>, then
/// commits. Never a delay. The outcome is always one of the serial orders, and never a <c>Pending</c> offer naming a
/// Membership that is gone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seeding.</b> Participants join through the product (the recipient's Membership is the product's), then every racing
/// Account is made present in the Space and holds its anti-forgery token before the lock is taken, so nothing but the
/// application lock orders the pair.
/// </para>
/// <para>
/// <b>The deciding assertion is that both waited.</b> The filtered unique index and the removal's own row locks would order
/// some pairs without the application lock; the waits are what show the lock is taken.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The Owner's offer to M and an Admin's removal of M, held on the Memberships lock until both are blocked, then released.
    /// Offer first: the removal lapses it (<c>201</c>, <c>204</c>, the offer <c>Lapsed</c>). Removal first: the offer finds no
    /// recipient (<c>204</c>, <c>400 invalid-offer-recipient</c>, no offer). Either way M is gone and no pending offer names it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - the offer route, its Memberships lock (Lee's Q5 (a)) and the lapse do not exist yet")]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-5")]
    public async Task An_offer_and_the_removal_of_its_recipient_held_on_the_Memberships_lock_end_in_a_serial_outcome()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var recipientMembership = Guid.Empty;
        WireResponse[] raced = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("admin", DisplayName(), "admin"));
            var admin = space.Members["admin"].Session;

            recipientMembership = space.Members["recipient"].MembershipId;

            await MadePresentAsync(space.Owner, spaceId);
            await MadePresentAsync(admin, spaceId);
            await space.Owner.FetchAntiforgeryTokenAsync();
            await admin.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var offer = OfferOverWireAsync(space.Owner, spaceId, recipientMembership, Guid.NewGuid().ToString("D"));
            var removal = RemoveOverWireAsync(admin, spaceId, recipientMembership, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(offer, removal), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(offer, removal);
        });

        var (offered, removed) = (raced[0], raced[1]);
        var compared = $"offer: {Described(offered)}; removal: {Described(removed)}";
        var offers = await wire.OwnershipOffersInAsync(spaceId);

        Assert.True(removed.StatusCode == HttpStatusCode.NoContent, "The removal succeeds in either order. " + compared);
        Assert.True(offered.StatusCode is HttpStatusCode.Created or HttpStatusCode.BadRequest, "The offer answers 201 (it ran first) or 400 (it ran second). " + compared);

        if (offered.StatusCode == HttpStatusCode.Created)
        {
            Assert.Equal("Lapsed", Assert.Single(offers).State);
        }
        else
        {
            Assert.Equal(OwnershipOfferWire.InvalidOfferRecipientProblemType, ProblemType(offered));
            Assert.Empty(offers);
        }

        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceId), row => row.Id == recipientMembership);
        await AssertNoPendingOfferNamesAMissingMembershipAsync(wire, spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// Two offers in one Space (two recipients, two keys), held on the Memberships lock until both are blocked, then
    /// released: exactly one <c>201</c> and one <c>409 ownership-offer-pending</c>, never a <c>500</c>, and exactly one
    /// <c>Pending</c> row, naming the winner's recipient.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AC13 - the offer route, its lock and UX_OwnershipOffer_SpaceId_Pending do not exist yet")]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-12")]
    public async Task Two_offers_held_on_the_Memberships_lock_commit_one_and_refuse_the_other_409()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        WireResponse[] raced = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("first", DisplayName(), "member"), ("second", DisplayName(), "viewer"));

            await MadePresentAsync(space.Owner, spaceId);
            await space.Owner.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var first = OfferOverWireAsync(space.Owner, spaceId, space.Members["first"].MembershipId, Guid.NewGuid().ToString("D"));
            var second = OfferOverWireAsync(space.Owner, spaceId, space.Members["second"].MembershipId, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(first, second), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(first, second);
        });

        var compared = $"first: {Described(raced[0])}; second: {Described(raced[1])}";
        var winner = Assert.Single(raced, answer => answer.StatusCode == HttpStatusCode.Created);
        var loser = Assert.Single(raced, answer => answer.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(OwnershipOfferWire.OwnershipOfferPendingProblemType, ProblemType(loser));

        var pending = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

        Assert.True(pending.State == "Pending", compared);
        Assert.Equal(OwnershipOfferWire.Parse(winner.Body).RecipientMembershipId, pending.RecipientMembershipId);
    }

    /// <summary>
    /// The Owner's revoke of the offer and the recipient's leave, held on the Memberships lock until both are blocked, then
    /// released. Revoke first: <c>204</c>, then the leave <c>204</c>, the offer <c>Revoked</c>. Leave first: <c>204</c>, the
    /// offer <c>Lapsed</c>, then the revoke <c>409 ownership-offer-not-pending</c>. Never both written: the offer holds exactly
    /// one terminal state, and the recipient is gone.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4, AC5, AC13 - the revoke route, its lock and the lapse in LeaveAsync do not exist yet")]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-26")]
    public async Task A_revoke_and_the_recipients_leave_held_on_the_Memberships_lock_end_in_a_serial_outcome()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var recipientAccount = Guid.Empty;
        WireResponse[] raced = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            recipientAccount = recipient.AccountId;

            await MadePresentAsync(space.Owner, spaceId);
            await MadePresentAsync(recipient.Session, spaceId);
            await space.Owner.FetchAntiforgeryTokenAsync();
            await recipient.Session.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var revoke = RevokeOfferOverWireAsync(space.Owner, spaceId, offer.Id, Guid.NewGuid().ToString("D"));
            var leave = LeaveOverWireAsync(recipient.Session, spaceId, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(revoke, leave), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(revoke, leave);
        });

        var (revoked, left) = (raced[0], raced[1]);
        var compared = $"revoke: {Described(revoked)}; leave: {Described(left)}";
        var state = Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State;

        Assert.True(left.StatusCode == HttpStatusCode.NoContent, "The leave succeeds in either order. " + compared);

        if (revoked.StatusCode == HttpStatusCode.NoContent)
        {
            Assert.True(state == "Revoked", "The revoke ran first, so the offer is Revoked. " + compared);
        }
        else
        {
            Assert.True(revoked.StatusCode == HttpStatusCode.Conflict, "The revoke answers 204 (first) or 409 (second). " + compared);
            Assert.Equal(OwnershipOfferWire.OwnershipOfferNotPendingProblemType, ProblemType(revoked));
            Assert.True(state == "Lapsed", "The leave ran first, so the offer is Lapsed. " + compared);
        }

        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceId), row => row.AccountId == recipientAccount);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }
}
