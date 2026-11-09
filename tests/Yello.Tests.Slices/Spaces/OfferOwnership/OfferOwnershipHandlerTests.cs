using Xunit;
using Yello.Application.Memberships;
using Yello.Application.Outbox;
using Yello.Application.Spaces;
using Yello.Application.Spaces.OfferOwnership;
using Yello.Application.Spaces.RevokeOwnershipOffer;
using Yello.Domain.Memberships;

namespace Yello.Tests.Slices.Spaces.OfferOwnership;

/// <summary>
/// The offer slice enqueues exactly one email per offer made - to the recipient, naming the Space and the offerer, carrying
/// the expiry, linking to the client with no id - and nothing on any refusal; the revoke enqueues nothing because it holds
/// no outbox.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>ActiveSpaceEndpointTests.OfferOwnership*</c> hold the statement, the lock, the outbox
/// row, the send record and the drop.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-43")]
[Trait("Requirement", "FR-8")]
public sealed class OfferOwnershipHandlerTests
{
    private static readonly Uri Origin = new("https://client.yello.test/somewhere?x=1");

    private static readonly Guid Recipient = Guid.NewGuid();

    private static readonly DateTimeOffset Expiry = new(2026, 11, 15, 9, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// A made offer enqueues one <c>ownership-offered</c> email to the recipient's address, naming the offerer and the Space,
    /// carrying the expiry in UTC and linking to the client's origin with nothing after it; the answer carries the offer and the
    /// recipient's name.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Assumption", "PRD-12-3")]
    public async Task A_made_offer_enqueues_one_email_to_the_recipient()
    {
        var offerId = Guid.NewGuid();
        var (handler, outbox) = Handler(new OwnershipOfferMade(OwnershipOfferOutcome.Offered, offerId), offerId);

        var result = await handler.HandleAsync(Context(), Recipient, TestContext.Current.CancellationToken);

        Assert.Equal(OwnershipOfferOutcome.Offered, result.Outcome);
        Assert.Equal((offerId, Recipient, "Nadia Ansari", Expiry), (result.Offer!.Id, result.Offer.RecipientMembershipId, result.Offer.RecipientName, result.Offer.ExpiresAt));

        var email = Assert.Single(outbox.Enqueued);

        Assert.Equal(OutboxKinds.OwnershipOffered, email.Kind);
        Assert.Equal("nadia@example.test", email.Recipient);
        Assert.Equal("Sam Lee offered you ownership of Harbour on Yello", email.Subject);
        Assert.Contains("Sam Lee has offered you ownership of Harbour on Yello.", email.Body, StringComparison.Ordinal);
        Assert.Contains("The offer is open until 2026-11-15 09:30 UTC.", email.Body, StringComparison.Ordinal);
        Assert.Contains("https://client.yello.test/\n", email.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("somewhere", email.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(offerId.ToString("D"), email.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("!", email.Subject + email.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing is enqueued when the store refuses the offer (another is pending, the recipient is not one, the caller's
    /// Membership is gone, ownership moved while it waited) or when the recipient is not a Membership of this Space at all. Anchor: a made offer enqueues.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task No_email_for_any_refused_offer()
    {
        var token = TestContext.Current.CancellationToken;

        foreach (var outcome in new[] { OwnershipOfferOutcome.AlreadyPending, OwnershipOfferOutcome.InvalidRecipient, OwnershipOfferOutcome.LostMembership, OwnershipOfferOutcome.NoLongerOwner })
        {
            var (refused, outbox) = Handler(new OwnershipOfferMade(outcome, null), null);

            Assert.Equal(outcome, (await refused.HandleAsync(Context(), Recipient, token)).Outcome);
            Assert.Empty(outbox.Enqueued);
        }

        var (unknown, unknownOutbox) = Handler(new OwnershipOfferMade(OwnershipOfferOutcome.Offered, Guid.NewGuid()), null, noticeKnown: false);

        Assert.Equal(OwnershipOfferOutcome.InvalidRecipient, (await unknown.HandleAsync(Context(), Recipient, token)).Outcome);
        Assert.Empty(unknownOutbox.Enqueued);

        var offerId = Guid.NewGuid();
        var (anchor, anchorOutbox) = Handler(new OwnershipOfferMade(OwnershipOfferOutcome.Offered, offerId), offerId);

        _ = await anchor.HandleAsync(Context(), Recipient, token);

        Assert.Single(anchorOutbox.Enqueued);
    }

    /// <summary>
    /// The revoke slice is the store's answer and nothing more: its only dependency is the offer store.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_revoke_answers_what_the_store_decided()
    {
        var offerId = Guid.NewGuid();
        var store = new FixedOffers(new OwnershipOfferMade(OwnershipOfferOutcome.Offered, offerId), null);

        Assert.Equal(OwnershipOfferRevocation.Revoked, await new RevokeOwnershipOfferHandler(store).HandleAsync(Guid.NewGuid(), offerId, TestContext.Current.CancellationToken));
        Assert.Equal(offerId, store.Revoked);
    }

    private static ActiveSpaceContext Context() => new(Guid.NewGuid(), Guid.NewGuid(), Role.Owner);

    private static (OfferOwnershipHandler Handler, RecordingOutbox Outbox) Handler(OwnershipOfferMade made, Guid? pendingId, bool noticeKnown = true)
    {
        var outbox = new RecordingOutbox();
        var pending = pendingId is { } id ? new PendingOwnershipOffer(id, Recipient, Expiry, Expiry.AddDays(-7)) : null;
        var notice = noticeKnown ? new OwnershipOfferNotice("nadia@example.test", "Nadia Ansari", "Sam Lee") : null;

        return (new OfferOwnershipHandler(new FixedOffers(made, pending), new FixedNotice(notice), new FixedName("Harbour"), new OwnershipOfferLinks(Origin), outbox), outbox);
    }

    private sealed class RecordingOutbox : IOutbox
    {
        public List<OutboxEmail> Enqueued { get; } = [];

        public Task EnqueueAsync(OutboxEmail email, CancellationToken cancellationToken)
        {
            Enqueued.Add(email);

            return Task.CompletedTask;
        }
    }

    private sealed class FixedOffers(OwnershipOfferMade made, PendingOwnershipOffer? pending) : IOwnershipOfferStore
    {
        public Guid? Revoked { get; private set; }

        public Task<OwnershipOfferMade> OfferAsync(Guid actorAccountId, Guid recipientMembershipId, string offererName, CancellationToken cancellationToken) =>
            Task.FromResult(made);

        public Task<PendingOwnershipOffer?> ReadPendingAsync(CancellationToken cancellationToken) => Task.FromResult(pending);

        public Task<OwnershipOfferRevocation> RevokeAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken)
        {
            Revoked = offerId;

            return Task.FromResult(OwnershipOfferRevocation.Revoked);
        }

        public Task<OwnershipOfferForRecipient?> ReadMineAsync(Guid actorAccountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("An offer reads no recipient's offer.");

        public Task<OwnershipOfferAcceptance> AcceptAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("An offer accepts nothing.");

        public Task<OwnershipOfferDecline> DeclineAsync(Guid actorAccountId, Guid offerId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("An offer declines nothing.");
    }

    private sealed class FixedName(string? name) : IActiveSpaceNameReader
    {
        public Task<string?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(name);
    }

    private sealed class FixedNotice(OwnershipOfferNotice? notice) : IMembershipStore
    {
        public Task<OwnershipOfferNotice?> OwnershipOfferNoticeAsync(Guid actorAccountId, Guid recipientMembershipId, CancellationToken cancellationToken) =>
            Task.FromResult(notice);

        public Task<string?> DisplayNameOfAsync(Guid membershipId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The offer reads the recipient's name with its notice.");

        public Task<AssignmentNotice?> AssignmentNoticeAsync(Guid actorAccountId, Guid membershipId, Guid taskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("An offer assigns no Task.");

        public Task<IReadOnlyList<AssigneeCandidate>> ListAssigneesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("An offer lists no candidates.");

        public Task<(IReadOnlyList<ListedMembership> Rows, int Total)> ListAsync(Guid callerAccountId, MembershipCursor? after, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException("An offer lists no Memberships.");

        public Task<RoleChangeResult> ChangeRoleAsync(Guid actorAccountId, Guid membershipId, Role proposed, RoleChangeAuthority authority, CancellationToken cancellationToken) =>
            throw new NotSupportedException("An offer changes no Role.");

        public Task<ListedMembership?> ReadAsync(Guid callerAccountId, Guid membershipId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("An offer reads no Membership row.");

        public Task<RemovalOutcome> RemoveAsync(Guid actorAccountId, Guid membershipId, RemovalAuthority authority, CancellationToken cancellationToken) =>
            throw new NotSupportedException("An offer removes nothing.");

        public Task<RemovalOutcome> LeaveAsync(Guid actorAccountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("An offer leaves nothing.");
    }
}
