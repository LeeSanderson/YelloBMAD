namespace Yello.Contracts.Invitations;

/// <summary>
/// The Invitation endpoints' paths, stated once for the client that calls them and the Host that serves them. FR-10,
/// FR-12; stories 4.1 (issue) and 4.2 (list and revoke).
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

    /// <summary>
    /// <c>GET</c> here: <c>200</c> with the Space's actionable pending Invitations as <see cref="InvitationResponse"/> rows,
    /// newest first and unpaged (story 4.2, AC1). The same path as <see cref="Issue"/>.
    /// </summary>
    public static string List => Issue;

    /// <summary>
    /// The route parameter naming one Invitation.
    /// </summary>
    public static string InvitationIdParameter => "invitationId";

    /// <summary>
    /// <c>DELETE</c> here to revoke a pending Invitation: <c>204</c> with no body; <c>409 invitation-not-pending</c> when it
    /// is accepted, revoked or expired; the boundary's <c>404</c> when the id names nothing in this Space (story 4.2, AC2,
    /// AC4, open question 1 (a)). The row is kept, set <c>Revoked</c> (AD-27).
    /// </summary>
    public static string Invitation => "/api/v1/spaces/{spaceId:guid}/invitations/{invitationId:guid}";

    /// <summary>
    /// One Invitation's path, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="invitationId">The Invitation.</param>
    /// <returns>The path.</returns>
    public static string InvitationOf(Guid spaceId, Guid invitationId) => $"/api/v1/spaces/{spaceId:D}/invitations/{invitationId:D}";

    /// <summary>
    /// <c>GET</c> here with the token in <see cref="TokenHeader"/>: the Invitation view (story 4.3, AC1, AC2; Lee's Q4 (a)).
    /// <c>200</c> with an <see cref="InvitationOfferResponse"/>, or <c>404 invitation-not-valid</c> for a revoked, accepted,
    /// expired or unrecognised token. A safe read that writes nothing, served the same whatever credential is presented.
    /// </summary>
    /// <remarks>
    /// <b>No <c>{spaceId}</c></b> (story 4.3, C7): the link holder does not know the Space until the view answers, and the
    /// page URL must not carry it. The token never travels in an API path, so no request line any layer logs carries it.
    /// </remarks>
    public static string Offer => "/api/v1/invitations/offer";

    /// <summary>
    /// <c>POST</c> an <see cref="AcceptInvitationRequest"/> here, with a Session, the anti-forgery token and an optional
    /// <c>Idempotency-Key</c>: the accept (story 4.3, AC4; Lee's Q4 (a), Q7 (a)). <c>200</c> with an
    /// <see cref="AcceptedInvitationResponse"/>; <c>401</c> without a Session (a bearer Token included);
    /// <c>404 invitation-not-valid</c>; <c>403 invitation-for-another-account</c>; <c>409 already-a-member</c>; or
    /// <c>409 bound-reached</c> at 100 Memberships.
    /// </summary>
    public static string Acceptance => "/api/v1/invitations/acceptance";

    /// <summary>
    /// The request header the view's token travels in (Lee's Q4 (a)), never a URL.
    /// </summary>
    public static string TokenHeader => "Yello-Invitation-Token";
}
