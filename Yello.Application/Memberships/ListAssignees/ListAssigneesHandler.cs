namespace Yello.Application.Memberships.ListAssignees;

/// <summary>
/// Lists the active Space's Assignee candidates: every Membership, as its id and its display name.
/// </summary>
/// <remarks>
/// <para>
/// <b>Open to every Role</b>: assigners need it for the picker, and every Role needs it to name the Assignees on the cards
/// and in the List View's filter. So it carries no address and no Role; the client labels the names itself, and a
/// collision between two names is found over the whole list, never a page of it.
/// </para>
/// <para>
/// <b>Unpaged</b>: a Space holds at most 100 Memberships, so the whole list is one bounded read.
/// </para>
/// </remarks>
/// <param name="store">Reads the Memberships.</param>
public sealed class ListAssigneesHandler(IMembershipStore store)
{
    /// <summary>
    /// Reads every candidate at once, so a name collision is judged over the whole Space.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>Every Membership of the Space, in join order.</returns>
    public Task<IReadOnlyList<AssigneeCandidate>> HandleAsync(CancellationToken cancellationToken) =>
        store.ListAssigneesAsync(cancellationToken);
}
