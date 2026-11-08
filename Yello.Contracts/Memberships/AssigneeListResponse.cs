namespace Yello.Contracts.Memberships;

/// <summary>
/// A Space's Assignee candidates, unpaged, in join order.
/// </summary>
/// <param name="Assignees">Every Membership of the Space.</param>
public sealed record AssigneeListResponse(IReadOnlyList<AssigneeResponse> Assignees);
