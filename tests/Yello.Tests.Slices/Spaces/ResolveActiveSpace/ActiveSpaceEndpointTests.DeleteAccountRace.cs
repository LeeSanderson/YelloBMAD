using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Account deletion under contention. The deletion leaves each Space through that Space's own leave, which takes the
/// Space's Memberships application lock (<c>Yello/Memberships/{spaceId:D}</c>) first, so each case holds that lock on a
/// connection of its own, lets the requests queue on it (<c>LCK_M_%</c>), and then commits. Never a delay.
/// </summary>
/// <remarks>
/// The owned-Space check runs before any Space's lock is taken, so a request held on the lock has already passed it: what
/// the case controls is what commits between that check and the leave.
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The deleting Account accepts an offer of ownership in another tab, and the acceptance is queued on the Space's
    /// Memberships lock ahead of the deletion's leave of that Space. <c>sp_getapplock</c> grants the queued waiters in
    /// order, so the acceptance commits first: the leave then meets an Owner's row and the deletion is refused
    /// <c>409 account-owns-spaces</c> naming that Space. The Account still exists and owns it; the former Owner is an Admin,
    /// as every accepted offer leaves it; one Owner.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-5")]
    [Trait("Requirement", "AD-26")]
    public async Task An_acceptance_queued_ahead_of_the_deletions_leave_makes_the_deletion_refuse_naming_that_Space()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, address, name) = (RandomAddress(), RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();
        var (ownerId, accountId) = (Guid.Empty, Guid.Empty);
        WireResponse[] raced = [];

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            ownerId = await AccountIdOfAsync(database, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Accepted mid-deletion " + Suffix(), ownerId, Role.Owner);

            var deleting = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), address, name, "member");
            accountId = await AccountIdOfAsync(database, address);

            await owner.FetchAntiforgeryTokenAsync();

            var offerId = (await OfferedAsync(owner, spaceId, (await wire.MembershipOfAsync(spaceId, accountId)).Id)).Id;

            _ = await DeletePersonalSpaceAsync(deleting, database, name);
            await MadePresentAsync(deleting, spaceId);
            await deleting.FetchAntiforgeryTokenAsync();

            // A second tab of the same Account: one session's cookie jar is not shared by two requests in flight.
            var otherTab = await SignedInWithoutRegisteringAsync(client, address);

            await otherTab.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var acceptance = AcceptOverWireAsync(deleting, spaceId, offerId, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, acceptance, cancellationToken);

            var deletion = AccountDeletionWire.DeleteAsync(otherTab);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(acceptance, deletion), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(acceptance, deletion);
        });

        var (accepted, refused) = (raced[0], raced[1]);

        Assert.True(accepted.StatusCode == HttpStatusCode.NoContent, $"The acceptance queued first: {Described(accepted)}");
        Assert.True(refused.StatusCode == HttpStatusCode.Conflict, $"The deletion queued second: {Described(refused)}; the acceptance was {Described(accepted)}");
        Assert.Equal(ProblemTypes.AccountOwnsSpaces, ProblemType(refused));
        Assert.Equal([spaceId], AccountDeletionWire.OwnedSpacesIn(refused.Body).Select(space => space.Id));
        Assert.Equal(1, await AccountRowsAsync(database, accountId));
        Assert.Equal("Owner", (await wire.MembershipOfAsync(spaceId, accountId)).Role);
        Assert.Equal("Admin", (await wire.MembershipOfAsync(spaceId, ownerId)).Role);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// A Membership that arrives after the deletion has listed the Account's Spaces is still left: the deletion is held on
    /// the first Space's Memberships lock, and meanwhile the Account accepts an Invitation into a second Space, which does
    /// not wait. After the release the deletion answers <c>204</c>, holding no Membership in either Space, because the
    /// erase re-reads the Account's Memberships and leaves the new one in one extra pass.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "AD-5")]
    [Trait("Requirement", "AD-24")]
    public async Task A_Membership_accepted_while_the_deletion_waits_is_left_in_one_extra_pass()
    {
        var database = Available();
        var wire = Wire();
        var (firstOwnerAddress, secondOwnerAddress, address, name) = (RandomAddress(), RandomAddress(), RandomAddress(), DisplayName());
        var (firstSpace, secondSpace) = (Guid.NewGuid(), Guid.NewGuid());
        var secondName = "Joined meanwhile " + Suffix();
        var accountId = Guid.Empty;
        WireResponse? joinedMeanwhile = null;
        WireResponse? deleted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var firstOwner = await SignedInAsync(client, firstOwnerAddress);
            await SeedNamedSpaceAsync(database, firstSpace, "Left first " + Suffix(), await AccountIdOfAsync(database, firstOwnerAddress), Role.Owner);

            var deleting = await JoinedThroughTheProductAsync(client, wire, mail, (firstOwner, firstSpace), address, name, "member");
            accountId = await AccountIdOfAsync(database, address);

            var secondOwner = await SignedInAsync(client, secondOwnerAddress);
            await SeedNamedSpaceAsync(database, secondSpace, secondName, await AccountIdOfAsync(database, secondOwnerAddress), Role.Owner);
            await secondOwner.FetchAntiforgeryTokenAsync();

            var issued = await IssueAsync(secondOwner, secondSpace, address, "member");

            Assert.True(issued.StatusCode == HttpStatusCode.NoContent, $"Issuing the second Invitation: {Described(issued)}");

            // The drop answers the first email to an address by default, which is the first Space's Invitation; this one is
            // told apart by the Space it names.
            var secondToken = InvitationWire.TokenIn(
                (await mail.WaitForEmailsToAsync(address, email => email.Everything.Contains(secondName, StringComparison.Ordinal)))[0]);

            _ = await DeletePersonalSpaceAsync(deleting, database, name);
            await deleting.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(firstSpace, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(firstSpace), cancellationToken);

            var deletion = AccountDeletionWire.DeleteAsync(deleting);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, deletion, cancellationToken);

            joinedMeanwhile = await AcceptAsync(deleting, secondToken);

            await held.CommitAsync(cancellationToken);

            deleted = await deletion;
        });

        Assert.True(joinedMeanwhile?.StatusCode == HttpStatusCode.OK, $"The acceptance while the deletion waits: {Described(joinedMeanwhile)}");
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"The deletion after the release: {Described(deleted)}");
        Assert.Equal(0, await AccountRowsAsync(database, accountId));

        foreach (var space in new[] { firstSpace, secondSpace })
        {
            Assert.DoesNotContain(await wire.MembershipRowsInAsync(space), row => row.AccountId == accountId);
        }

        await AssertOneOwnerEachAsync(wire, firstSpace, secondSpace);
    }
}
