using Xunit;
using Yello.Application.Invitations;
using Yello.Application.Invitations.ListInvitations;
using Yello.Application.Invitations.RevokeInvitation;
using Yello.Domain.Memberships;

namespace Yello.Tests.Slices.Invitations.RevokeInvitation;

/// <summary>
/// Story 4.2 Task 3 - the list and revoke slices pass the store's answer through unchanged: the list returns the store's
/// rows in the store's order, and the revoke names exactly the Invitation it was given and returns each of the three
/// outcomes as it came. Neither takes a context, so neither can read a Role, a Space or the caller (AR-3; FR-12's "any
/// Owner or Admin"). AC1, AC2, AC4.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>ActiveSpaceEndpointTests.ListInvitations</c>, <c>.RevokeInvitation</c> and
/// <c>.RevokeInvitationRace</c> hold the predicate, the guarded update, the rows and the race.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-12")]
public sealed class ListAndRevokeInvitationHandlerTests
{
    /// <summary>
    /// The outcomes a revoke can come to.
    /// </summary>
    public static TheoryData<InvitationRevocation> Revocations => new()
    {
        InvitationRevocation.Revoked,
        InvitationRevocation.NotPending,
        InvitationRevocation.NotFound,
    };

    /// <summary>
    /// AC1, UX-DR14 - the list returns the store's actionable pending Invitations as the store ordered them, newest first.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "UX-DR14")]
    public async Task The_list_returns_the_stores_pending_Invitations_in_its_order()
    {
        PendingInvitation[] rows =
        [
            new(Guid.NewGuid(), "newer@example.test", Role.Viewer, "Ada", DateTimeOffset.UnixEpoch.AddDays(2)),
            new(Guid.NewGuid(), "older@example.test", Role.Admin, "Grace", DateTimeOffset.UnixEpoch.AddDays(1)),
        ];
        var store = new ScriptedStore(rows, InvitationRevocation.Revoked);

        var listed = await new ListInvitationsHandler(store).HandleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(rows, listed);
        Assert.Empty(store.Revoked);
    }

    /// <summary>
    /// AC2, AC4 - the revoke names exactly the Invitation it was given, once, and answers the store's outcome unchanged.
    /// </summary>
    /// <param name="outcome">What the store answers.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(Revocations))]
    public async Task The_revoke_names_the_given_Invitation_once_and_answers_the_stores_outcome(InvitationRevocation outcome)
    {
        var invitationId = Guid.NewGuid();
        var store = new ScriptedStore([], outcome);

        var revocation = await new RevokeInvitationHandler(store).HandleAsync(invitationId, TestContext.Current.CancellationToken);

        Assert.Equal(outcome, revocation);
        Assert.Equal([invitationId], store.Revoked);
    }

    private sealed class ScriptedStore(IReadOnlyList<PendingInvitation> pending, InvitationRevocation revocation) : IInvitationStore
    {
        public List<Guid> Revoked { get; } = [];

        public Task<InvitationIssue> IssueAsync(Guid spaceId, Guid issuerAccountId, string emailAddress, Role role, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Neither the list nor the revoke issues.");

        public Task<IReadOnlyList<PendingInvitation>> ListPendingAsync(CancellationToken cancellationToken) => Task.FromResult(pending);

        public Task<InvitationRevocation> RevokeAsync(Guid invitationId, CancellationToken cancellationToken)
        {
            Revoked.Add(invitationId);

            return Task.FromResult(revocation);
        }

        public Task<InvitationOffer?> ReadOfferAsync(InvitationContext context, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Neither the list nor the revoke reads an offer.");

        public Task<InvitationAcceptanceOutcome> AcceptAsync(InvitationContext context, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Neither the list nor the revoke accepts.");
    }
}
