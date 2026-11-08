namespace Yello.Contracts.Tasks;

/// <summary>
/// An assignment: the Membership the Task is given to, or <c>null</c> to unassign it.
/// </summary>
/// <remarks>
/// <b>A Membership of the Task's own Space, by id</b> - never an Account or an address. A body that is not an object, or
/// that does not name <c>membershipId</c> at all, is refused, so an unassignment is always stated explicitly.
/// </remarks>
/// <param name="MembershipId">The Membership, one of the Space's Assignee candidates, or <c>null</c>.</param>
public sealed record AssignTaskRequest(Guid? MembershipId);
