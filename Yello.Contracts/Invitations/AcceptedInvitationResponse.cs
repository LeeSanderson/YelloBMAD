namespace Yello.Contracts.Invitations;

/// <summary>
/// The accept's <c>200</c>: the Space the caller has just joined. Story 4.3, open question 10 (a).
/// </summary>
/// <remarks>
/// The caller is now a Member of it, so the id is theirs to know; the client lands on the Space's home with it.
/// </remarks>
/// <param name="SpaceId">The joined Space.</param>
public sealed record AcceptedInvitationResponse(Guid SpaceId);
