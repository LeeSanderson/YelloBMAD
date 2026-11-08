using System.Net;
using Xunit;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Where a pending Ownership Offer goes when what it names goes. Removing the recipient's Membership, or the recipient
/// leaving, writes <c>Lapsed</c> on every pending offer naming it inside the removal's own transaction, before the guarded
/// <c>DELETE</c>: the offer is retained, never deleted, and never left <c>Pending</c> naming a Membership that no longer
/// exists. A refused removal rolls the lapse back with everything else. A Space deletion takes its offers with it by the
/// cascade from <c>Space</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written, not joined.</b> A predicate that joined the Membership would leave such rows <c>Pending</c> and holding the
/// Space's one pending place; these cases read the stored state, so only a written lapse passes them.
/// </para>
/// <para>
/// <b>Seeding</b> is as in <c>.OfferOwnership.cs</c>: every Membership joins through the product, and every offer is made
/// through the product's route, so the removal meets the rows the product wrote.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The Owner, or an Admin, removes the recipient (a Member): <c>204</c>, the offer is retained and <c>Lapsed</c>, no
    /// pending offer names a missing Membership, every other Membership keeps its Role, and the Space keeps one Owner. The
    /// pending place is free again: an offer to another Member is then <c>201</c>.
    /// </summary>
    /// <param name="removerRole">Who removes the recipient.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-5")]
    [Trait("Requirement", "AR-31")]
    public async Task Removing_the_recipient_lapses_the_offer_and_changes_no_other_Role(string removerRole)
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var offerId = Guid.Empty;
        IReadOnlyList<MembershipRow> before = [];
        WireResponse? removed = null;
        WireResponse? next = null;
        var recipientMembership = Guid.Empty;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("admin", DisplayName(), "admin"), ("other", DisplayName(), "member"));

            recipientMembership = space.Members["recipient"].MembershipId;
            offerId = (await OfferedAsync(space.Owner, spaceId, recipientMembership)).Id;
            before = await wire.MembershipRowsInAsync(spaceId);

            var remover = removerRole == "owner" ? space.Owner : space.Members["admin"].Session;

            await remover.FetchAntiforgeryTokenAsync();
            removed = await RemoveOverWireAsync(remover, spaceId, recipientMembership);
            next = await OfferOverWireAsync(space.Owner, spaceId, space.Members["other"].MembershipId);
        });

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The {removerRole}'s removal of the recipient: {Described(removed)}");
        Assert.Equal("Lapsed", Assert.Single(await wire.OwnershipOffersInAsync(spaceId), row => row.Id == offerId).State);
        Assert.Equal(before.Where(row => row.Id != recipientMembership), await wire.MembershipRowsInAsync(spaceId));
        Assert.True(next?.StatusCode == HttpStatusCode.Created, $"An offer after the lapse: {Described(next)}");
        await AssertNoPendingOfferNamesAMissingMembershipAsync(wire, spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The recipient leaves: <c>204</c>, the offer is retained and <c>Lapsed</c>, no pending offer names a missing
    /// Membership, and the Space keeps one Owner.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-5")]
    [Trait("Requirement", "AR-31")]
    public async Task The_recipient_leaving_lapses_the_offer()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var offerId = Guid.Empty;
        WireResponse? left = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "viewer"));
            var recipient = space.Members["recipient"];

            offerId = (await OfferedAsync(space.Owner, spaceId, recipient.MembershipId)).Id;

            await recipient.Session.FetchAntiforgeryTokenAsync();
            left = await LeaveOverWireAsync(recipient.Session, spaceId);
        });

        Assert.True(left?.StatusCode == HttpStatusCode.NoContent, $"The recipient's leave: {Described(left)}");
        Assert.Equal("Lapsed", Assert.Single(await wire.OwnershipOffersInAsync(spaceId), row => row.Id == offerId).State);
        await AssertNoPendingOfferNamesAMissingMembershipAsync(wire, spaceId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The lapse is rolled back with a refused removal: an Admin removing the Admin recipient is outside its authority
    /// (<c>403</c>), so the recipient keeps its Membership and its Role and the offer stays <c>Pending</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "FR-14")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task A_refused_removal_of_the_recipient_leaves_the_offer_pending()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var recipientAccount = Guid.Empty;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "admin"), ("actor", DisplayName(), "admin"));
            var recipient = space.Members["recipient"];

            recipientAccount = recipient.AccountId;
            _ = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            await space.Members["actor"].Session.FetchAntiforgeryTokenAsync();
            refused = await RemoveOverWireAsync(space.Members["actor"].Session, spaceId, recipient.MembershipId);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"The Admin removing the Admin recipient: {Described(refused)}");
        Assert.Equal("Admin", (await wire.MembershipOfAsync(spaceId, recipientAccount)).Role);
        Assert.Equal("Pending", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
    }

    /// <summary>
    /// The lapse names only the departing Membership: removing another Member leaves the offer <c>Pending</c> and naming its
    /// recipient.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "FR-14")]
    public async Task Removing_another_Member_leaves_the_offer_pending()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var recipientMembership = Guid.Empty;
        WireResponse? removed = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("bystander", DisplayName(), "member"));

            recipientMembership = space.Members["recipient"].MembershipId;
            _ = await OfferedAsync(space.Owner, spaceId, recipientMembership);
            removed = await RemoveOverWireAsync(space.Owner, spaceId, space.Members["bystander"].MembershipId);
        });

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The bystander's removal: {Described(removed)}");

        var offer = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

        Assert.Equal("Pending", offer.State);
        Assert.Equal(recipientMembership, offer.RecipientMembershipId);
    }

    /// <summary>
    /// A Space deletion takes its offers with it: deleting a Space whose offer is pending answers <c>204</c> in one statement
    /// (the offer's only key is the cascade from <c>Space</c>, so there is no second cascade path) and leaves no offer row
    /// and no Membership of it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-7")]
    [Trait("Requirement", "AD-5")]
    public async Task Deleting_a_Space_with_a_pending_offer_takes_the_offer_with_it()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var offersBefore = 0;
        WireResponse? deleted = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));

            _ = await OfferedAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);
            offersBefore = (await wire.OwnershipOffersInAsync(spaceId)).Count;
            deleted = await DeleteSpaceOverWireAsync(space.Owner, spaceId, key: null);
        });

        Assert.Equal(1, offersBefore);
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"The deletion: {Described(deleted)}");
        Assert.Empty(await wire.OwnershipOffersInAsync(spaceId));
        Assert.Empty(await wire.MembershipRowsInAsync(spaceId));
    }
}
