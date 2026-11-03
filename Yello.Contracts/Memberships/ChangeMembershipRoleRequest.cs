namespace Yello.Contracts.Memberships;

/// <summary>
/// The body of a Role change. Story 4.4, AC4, Q9 (a).
/// </summary>
/// <param name="Role">The Role asked for, as its stable code: <c>admin</c>, <c>member</c> or <c>viewer</c>. <c>owner</c>
/// is refused <c>400 role-not-assignable</c> - ownership moves only by an Ownership Offer.</param>
public sealed record ChangeMembershipRoleRequest(string? Role);
