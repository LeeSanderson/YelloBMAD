namespace Yello.Application.Memberships.LeaveSpace;

/// <summary>
/// The caller leaves the active Space: its own Membership is removed unless it holds ownership.
/// </summary>
/// <remarks>
/// <para>
/// <b>Decided by identity, not by Role.</b> "Any Account can remove its own" is not a row of the capability matrix, so the
/// route declares the read capability every Role holds, and the store's one statement can delete nothing but the caller's
/// own non-Owner row. This slice reads no Role and consults no acting authority.
/// </para>
/// <para>
/// <b>A Token may leave too.</b> Deleting the Membership cascades to every Token the Account holds for the Space, the
/// calling Token included, inside this request's transaction; the answer is still sent, and the Token's next request is the
/// no-credential <c>401</c>.
/// </para>
/// </remarks>
/// <param name="store">Removes the Membership.</param>
public sealed class LeaveSpaceHandler(IMembershipStore store)
{
    /// <summary>
    /// Leaves the Space.
    /// </summary>
    /// <param name="actorAccountId">The acting Account, from the pipeline's context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened.</returns>
    public Task<RemovalOutcome> HandleAsync(Guid actorAccountId, CancellationToken cancellationToken) =>
        store.LeaveAsync(actorAccountId, cancellationToken);
}
