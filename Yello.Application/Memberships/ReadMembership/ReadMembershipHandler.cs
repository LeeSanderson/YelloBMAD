namespace Yello.Application.Memberships.ReadMembership;

/// <summary>
/// Reads one Membership of the active Space with whether its Account is in the Space now: what the removal confirm states
/// when it opens. UX-DR36, AD-23, AD-27.
/// </summary>
/// <remarks>
/// <b>Under the list's capability</b>, because the row carries the address the list does, and read fresh rather than taken
/// from the list, because the confirm says "now". Presence is read in this Space only; nothing about any other Space is
/// read.
/// </remarks>
/// <param name="store">Reads the Membership.</param>
public sealed class ReadMembershipHandler(IMembershipStore store)
{
    /// <summary>
    /// Reads the Membership.
    /// </summary>
    /// <param name="callerAccountId">The acting Account, whose own row is marked.</param>
    /// <param name="membershipId">The Membership, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The row, or <c>null</c> when no Membership in this Space has the id.</returns>
    public Task<ListedMembership?> HandleAsync(Guid callerAccountId, Guid membershipId, CancellationToken cancellationToken) =>
        store.ReadAsync(callerAccountId, membershipId, cancellationToken);
}
