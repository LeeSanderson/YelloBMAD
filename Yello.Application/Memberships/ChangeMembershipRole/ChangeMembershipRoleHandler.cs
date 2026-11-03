using Yello.Domain.Memberships;

namespace Yello.Application.Memberships.ChangeMembershipRole;

/// <summary>
/// Changes a Membership's Role in the active Space, within the acting Account's authority. FR-13, AR-12/AD-5, AR-16/AD-9;
/// story 4.4, AC4, AC5, AC6; Lee's Q1 (a) and Q2 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space, checked
/// <c>ChangeMembershipRole</c> (Owner Allowed, Admin Qualified; a Member or Viewer is its <c>403</c>), replayed an
/// <c>Idempotency-Key</c> it had already seen, and set the <see cref="ActingAuthority"/> from the Role it read.
/// </para>
/// <para>
/// <b>The Qualified cell's target rule, without reading a Role</b> (<c>deferred-work.md:329</c>): this slice hands the
/// authority's Role sets to the store, which composes them into one guarded statement under the Space's Memberships lock.
/// An outcome beyond the authority comes back typed; the endpoint hands it to the pipeline, which records and answers the
/// <c>403</c> (AD-20). Owner is in no set, so no change here can touch the Owner's row or write <c>Owner</c>.
/// </para>
/// </remarks>
/// <param name="store">Changes the Role.</param>
/// <param name="authority">The acting authority the pipeline set.</param>
public sealed class ChangeMembershipRoleHandler(IMembershipStore store, ActingAuthority authority)
{
    /// <summary>
    /// Changes the Role.
    /// </summary>
    /// <param name="actorAccountId">The acting Account, from the pipeline's context.</param>
    /// <param name="membershipId">The target, from the route.</param>
    /// <param name="proposed">The Role asked for - never Owner (the endpoint refused it).</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened.</returns>
    public Task<RoleChangeResult> HandleAsync(Guid actorAccountId, Guid membershipId, Role proposed, CancellationToken cancellationToken) =>
        store.ChangeRoleAsync(actorAccountId, membershipId, proposed, authority.RoleChanges, cancellationToken);
}
