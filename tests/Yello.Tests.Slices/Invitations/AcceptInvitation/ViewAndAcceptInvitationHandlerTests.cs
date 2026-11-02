using Xunit;
using Yello.Application.Invitations;
using Yello.Application.Invitations.AcceptInvitation;
using Yello.Application.Invitations.ViewInvitation;
using Yello.Domain.Memberships;

namespace Yello.Tests.Slices.Invitations.AcceptInvitation;

/// <summary>
/// Story 4.3 Task 5 - the view and accept slices pass the pipeline's context to the store unchanged and answer what the
/// store came to: they read no Role, count nothing and choose no Space (AR-3), and the accept refuses to run without the
/// Session's Account rather than accepting as nobody (AD-28). AC1, AC4.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>ActiveSpaceEndpointTests.ViewInvitation</c>, <c>.AcceptInvitation</c>,
/// <c>.AcceptInvitationRace</c> and <c>.AcceptInvitationBound</c> hold the predicate, the guarded update, the lock and the
/// bound.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-11")]
public sealed class ViewAndAcceptInvitationHandlerTests
{
    /// <summary>
    /// The outcomes an accept can come to.
    /// </summary>
    public static TheoryData<InvitationAcceptanceOutcome> Outcomes => new()
    {
        InvitationAcceptanceOutcome.Accepted,
        InvitationAcceptanceOutcome.NotValid,
        InvitationAcceptanceOutcome.ForAnotherAccount,
        InvitationAcceptanceOutcome.AlreadyAMember,
    };

    /// <summary>
    /// AC1, AC2 - the view hands the store exactly the context it was given and answers the store's offer, or its
    /// absence, unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR18")]
    public async Task The_view_reads_the_offer_for_the_given_context_and_answers_it_unchanged()
    {
        var context = new InvitationContext(Guid.NewGuid(), Guid.NewGuid(), AccountId: null);
        var offer = new InvitationOffer("Northwind", Role.Viewer, "Ada", AddressedToYou: null);
        var offering = new ScriptedStore(offer, InvitationAcceptanceOutcome.NotValid);
        var empty = new ScriptedStore(offer: null, InvitationAcceptanceOutcome.NotValid);

        Assert.Same(offer, await new ViewInvitationHandler(offering).HandleAsync(context, TestContext.Current.CancellationToken));
        Assert.Null(await new ViewInvitationHandler(empty).HandleAsync(context, TestContext.Current.CancellationToken));
        Assert.Equal([context], offering.Read);
        Assert.Empty(offering.Accepted);
    }

    /// <summary>
    /// AC4 - the accept hands the store exactly the context it was given, once, and answers each outcome as it came.
    /// </summary>
    /// <param name="outcome">What the store answers.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(Outcomes))]
    [Trait("Requirement", "AD-28")]
    public async Task The_accept_names_the_given_context_once_and_answers_the_stores_outcome(InvitationAcceptanceOutcome outcome)
    {
        var context = new InvitationContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var store = new ScriptedStore(offer: null, outcome);

        var accepted = await new AcceptInvitationHandler(store).HandleAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(outcome, accepted);
        Assert.Equal([context], store.Accepted);
        Assert.Empty(store.Read);
    }

    /// <summary>
    /// AC4, AD-28, Lee's Q7 (a) - with no Session's Account the accept never reaches the store: the token is never the
    /// authority, so there is nobody to accept as. The pipeline answers such a request <c>401</c> before this runs.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-28")]
    public async Task An_accept_without_an_Account_never_reaches_the_store()
    {
        var store = new ScriptedStore(offer: null, InvitationAcceptanceOutcome.Accepted);
        var handler = new AcceptInvitationHandler(store);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new InvitationContext(Guid.NewGuid(), Guid.NewGuid(), AccountId: null), TestContext.Current.CancellationToken));
        Assert.Empty(store.Accepted);
    }

    private sealed class ScriptedStore(InvitationOffer? offer, InvitationAcceptanceOutcome outcome) : IInvitationStore
    {
        public List<InvitationContext> Read { get; } = [];

        public List<InvitationContext> Accepted { get; } = [];

        public Task<InvitationOffer?> ReadOfferAsync(InvitationContext context, CancellationToken cancellationToken)
        {
            Read.Add(context);

            return Task.FromResult(offer);
        }

        public Task<InvitationAcceptanceOutcome> AcceptAsync(InvitationContext context, CancellationToken cancellationToken)
        {
            Accepted.Add(context);

            return Task.FromResult(outcome);
        }

        public Task<InvitationIssue> IssueAsync(Guid spaceId, Guid issuerAccountId, string emailAddress, Role role, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Neither the view nor the accept issues.");

        public Task<IReadOnlyList<PendingInvitation>> ListPendingAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("Neither the view nor the accept lists.");

        public Task<InvitationRevocation> RevokeAsync(Guid invitationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Neither the view nor the accept revokes.");
    }
}
