namespace Yello.Application.Memberships.RemoveMembership;

/// <summary>
/// Removes a Membership from the active Space, within the acting Account's authority.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space, checked
/// <c>RemoveMembership</c> (Owner Allowed, Admin Qualified; a Member or Viewer is its <c>403</c>), replayed an
/// <c>Idempotency-Key</c> it had already seen, and set the <see cref="ActingAuthority"/> from the Role it read.
/// </para>
/// <para>
/// <b>The Qualified cell's target rule, without reading a Role</b>: this slice hands the authority's removal sets to the
/// store, which composes them into one guarded <c>DELETE</c> under the Space's Memberships lock. An outcome beyond the
/// authority, or an actor that lost its own Membership while it waited, comes back typed; the endpoint hands it to the
/// pipeline, which records and answers it.
/// </para>
/// </remarks>
/// <param name="store">Removes the Membership.</param>
/// <param name="authority">The acting authority the pipeline set.</param>
public sealed class RemoveMembershipHandler(IMembershipStore store, ActingAuthority authority)
{
    /// <summary>
    /// Removes the Membership.
    /// </summary>
    /// <param name="actorAccountId">The acting Account, from the pipeline's context.</param>
    /// <param name="membershipId">The target, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened.</returns>
    public Task<RemovalOutcome> HandleAsync(Guid actorAccountId, Guid membershipId, CancellationToken cancellationToken) =>
        store.RemoveAsync(actorAccountId, membershipId, authority.Removals, cancellationToken);
}
