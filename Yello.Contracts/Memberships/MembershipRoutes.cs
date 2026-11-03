namespace Yello.Contracts.Memberships;

/// <summary>
/// The Membership endpoints' paths, stated once for the client that calls them and the Host that serves them. FR-13,
/// UX-DR14; story 4.4, AC1, AC4, Q9 (a).
/// </summary>
/// <remarks>
/// <b><c>{spaceId}</c> first after the version</b> (AR-9), so the pipeline resolves the route because of its shape, checks
/// <c>ChangeMembershipRole</c> (Owner Allowed, Admin Qualified; Lee's Q4 (a)) and records a boundary refusal. Served on both
/// surfaces (AD-4, FR-35).
/// </remarks>
public static class MembershipRoutes
{
    /// <summary>
    /// <c>GET</c> here, with an optional <see cref="AfterParameter"/>: <c>200</c> with a <see cref="MembershipPageResponse"/>
    /// - 50 Memberships a page in join order, the true total, and the next page's cursor (Q8 (a)).
    /// </summary>
    public static string List => "/api/v1/spaces/{spaceId:guid}/memberships";

    /// <summary>
    /// The query parameter carrying the opaque cursor a previous page answered.
    /// </summary>
    public static string AfterParameter => "after";

    /// <summary>
    /// The route parameter naming one Membership.
    /// </summary>
    public static string MembershipIdParameter => "membershipId";

    /// <summary>
    /// <c>PATCH</c> a <see cref="ChangeMembershipRoleRequest"/> here: <c>200</c> with the updated
    /// <see cref="MembershipRoleChangeResponse"/> (no address); <c>400 role-not-assignable</c> for <c>owner</c> or an unknown code;
    /// <c>403 insufficient-role</c> beyond the caller's authority; <c>409 owner-role-fixed</c> for the Owner's own row; the
    /// boundary's <c>404</c> for an id that names nothing in this Space (AC4, Q10 (a), Q11 (a)).
    /// </summary>
    public static string Membership => "/api/v1/spaces/{spaceId:guid}/memberships/{membershipId:guid}";

    /// <summary>
    /// The Space's Memberships path - the first page - as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string MembershipsOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/memberships";

    /// <summary>
    /// The Space's Memberships path, as a client calls it, with the cursor when one is given.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="after">The cursor the previous page answered, or <c>null</c> for the first page.</param>
    /// <returns>The path.</returns>
    public static string MembershipsOf(Guid spaceId, string? after) =>
        after is null
            ? MembershipsOf(spaceId)
            : $"{MembershipsOf(spaceId)}?{AfterParameter}={Uri.EscapeDataString(after)}";

    /// <summary>
    /// One Membership's path, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="membershipId">The Membership.</param>
    /// <returns>The path.</returns>
    public static string MembershipOf(Guid spaceId, Guid membershipId) => $"/api/v1/spaces/{spaceId:D}/memberships/{membershipId:D}";
}
