namespace Yello.Contracts.Invitations;

/// <summary>
/// The Invitation endpoints' paths, stated once for the client that calls them and the Host that serves them. FR-10;
/// story 4.1.
/// </summary>
/// <remarks>
/// <b><c>{spaceId}</c> first after the version</b> (AR-9), so the pipeline resolves the route because of its shape,
/// checks <c>IssueAndRevokeInvitations</c>, and records a boundary refusal. Served on both surfaces (FR-35).
/// </remarks>
public static class InvitationRoutes
{
    /// <summary>
    /// <c>POST</c> an <see cref="IssueInvitationRequest"/> here: <c>204</c> with no body and no <c>Location</c>, whether or
    /// not the address has an Account (AD-23).
    /// </summary>
    public static string Issue => "/api/v1/spaces/{spaceId:guid}/invitations";

    /// <summary>
    /// The Space's Invitations path, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string InvitationsOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/invitations";
}
