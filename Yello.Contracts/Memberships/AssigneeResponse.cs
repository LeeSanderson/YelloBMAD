namespace Yello.Contracts.Memberships;

/// <summary>
/// One Assignee candidate: a Membership of the Space and its display name, labelled by the client with
/// <see cref="MembershipLabels"/>.
/// </summary>
/// <param name="MembershipId">The Membership an assignment names.</param>
/// <param name="DisplayName">The Account's display name, as written.</param>
public sealed record AssigneeResponse(Guid MembershipId, string DisplayName);
