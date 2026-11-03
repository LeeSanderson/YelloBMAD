namespace Yello.Contracts.Memberships;

/// <summary>
/// The row a Role change answers: the Membership list's row without the email address. Story 4.4, AC4; code review
/// 2026-11-03, decided by Lee (option (b)).
/// </summary>
/// <remarks>
/// <b>Exactly four members</b>, held by a contract-shape gate. <b>No address</b>: the pipeline records a keyed change's
/// answer whole in its idempotency record, and addresses are visible in the Membership list and nowhere else (AD-23,
/// Lee's Q4 (a)), so no idempotency record may hold one. The client merges this into the row it already shows, keeping
/// that row's address.
/// </remarks>
/// <param name="Id">The Membership's id.</param>
/// <param name="DisplayName">The Account's display name, read live for a current Member (Lee's Q5 (a)).</param>
/// <param name="Role">The Role, as its stable code: <c>owner</c>, <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="IsYou">Whether the row is the caller's own Membership.</param>
public sealed record MembershipRoleChangeResponse(Guid Id, string DisplayName, string Role, bool IsYou);
