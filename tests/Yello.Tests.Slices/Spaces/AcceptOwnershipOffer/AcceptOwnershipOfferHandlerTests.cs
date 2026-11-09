using Xunit;
using Yello.Application.Spaces;
using Yello.Application.Spaces.AcceptOwnershipOffer;
using Yello.Application.Spaces.DeclineOwnershipOffer;
using Yello.Application.Spaces.ReadMyOwnershipOffer;

namespace Yello.Tests.Slices.Spaces.AcceptOwnershipOffer;

/// <summary>
/// The recipient's three slices - the read of the offer naming it, the acceptance and the decline - hand the caller's Account
/// and the route's offer to the store and answer exactly what it decided: identity is the store's, never the slice's, and
/// none of them takes anything else.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>ActiveSpaceEndpointTests.AcceptOwnershipOffer*</c>, <c>.DeclineOwnershipOffer</c> and
/// <c>.ReadMyOwnershipOffer</c> hold the guarded statements, the lock, the swap and the publishes.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-42")]
[Trait("Requirement", "AD-26")]
public sealed class AcceptOwnershipOfferHandlerTests
{
    /// <summary>
    /// Every acceptance outcome the store returns is the slice's answer, and the store is asked for the caller's Account and
    /// the route's offer.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_acceptance_answers_what_the_store_decided_for_the_caller_and_the_offer()
    {
        var (actor, offerId) = (Guid.NewGuid(), Guid.NewGuid());

        foreach (var outcome in Enum.GetValues<OwnershipOfferAcceptance>())
        {
            var store = new RecordingOffers { Acceptance = outcome };

            Assert.Equal(outcome, await new AcceptOwnershipOfferHandler(store).HandleAsync(actor, offerId, TestContext.Current.CancellationToken));
            Assert.Equal((actor, offerId), store.Asked);
        }
    }

    /// <summary>
    /// Every decline outcome the store returns is the slice's answer, for the caller's Account and the route's offer.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_decline_answers_what_the_store_decided_for_the_caller_and_the_offer()
    {
        var (actor, offerId) = (Guid.NewGuid(), Guid.NewGuid());

        foreach (var outcome in Enum.GetValues<OwnershipOfferDecline>())
        {
            var store = new RecordingOffers { Decline = outcome };

            Assert.Equal(outcome, await new DeclineOwnershipOfferHandler(store).HandleAsync(actor, offerId, TestContext.Current.CancellationToken));
            Assert.Equal((actor, offerId), store.Asked);
        }
    }

    /// <summary>
    /// The read answers the store's offer for the caller's Account, or none.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_recipients_read_answers_the_store_s_offer_for_the_caller_or_none()
    {
        var actor = Guid.NewGuid();
        var offer = new OwnershipOfferForRecipient(Guid.NewGuid(), "Sam Lee", DateTimeOffset.UnixEpoch.AddDays(7), DateTimeOffset.UnixEpoch);
        var named = new RecordingOffers { Mine = offer };
        var none = new RecordingOffers();

        Assert.Equal(offer, await new ReadMyOwnershipOfferHandler(named).HandleAsync(actor, TestContext.Current.CancellationToken));
        Assert.Equal((actor, Guid.Empty), named.Asked);
        Assert.Null(await new ReadMyOwnershipOfferHandler(none).HandleAsync(actor, TestContext.Current.CancellationToken));
    }

    private sealed class RecordingOffers : IOwnershipOfferStore
    {
        public OwnershipOfferAcceptance Acceptance { get; init; }

        public OwnershipOfferDecline Decline { get; init; }

        public OwnershipOfferForRecipient? Mine { get; init; }

        public (Guid Actor, Guid Offer) Asked { get; private set; }

        public Task<OwnershipOfferForRecipient?> ReadMineAsync(Guid actorAccountId, CancellationToken cancellationToken)
        {
            Asked = (actorAccountId, Guid.Empty);

            return Task.FromResult(Mine);
        }

        public Task<OwnershipOfferAcceptance> AcceptAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken)
        {
            Asked = (actorAccountId, offerId);

            return Task.FromResult(Acceptance);
        }

        public Task<OwnershipOfferDecline> DeclineAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken)
        {
            Asked = (actorAccountId, offerId);

            return Task.FromResult(Decline);
        }

        public Task<OwnershipOfferMade> OfferAsync(Guid actorAccountId, Guid recipientMembershipId, string offererName, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The recipient makes no offer.");

        public Task<PendingOwnershipOffer?> ReadPendingAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("The recipient does not read the Owner's view.");

        public Task<OwnershipOfferRevocation> RevokeAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The recipient revokes nothing.");
    }
}
