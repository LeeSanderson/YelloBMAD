namespace Yello.Contracts.Memberships;

/// <summary>
/// The path of a Space's Assignee candidates: every Membership a Task of the Space may be assigned to.
/// </summary>
/// <remarks>
/// <b>Every Role reads it</b> - the picker, the cards' avatars and the List View's Assignee filter all name people from it -
/// so it carries a display name and a Membership id and nothing else: no address, no Role, no Account.
/// </remarks>
public static class AssigneeRoutes
{
    /// <summary>
    /// <c>GET</c> here: <c>200</c> with an <see cref="AssigneeListResponse"/> - every Membership of the Space, unpaged (a
    /// Space holds at most 100), in join order.
    /// </summary>
    public static string List => "/api/v1/spaces/{spaceId:guid}/assignees";

    /// <summary>
    /// A Space's candidates path, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string ListOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/assignees";
}
