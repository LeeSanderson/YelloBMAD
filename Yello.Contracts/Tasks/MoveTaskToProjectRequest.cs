namespace Yello.Contracts.Tasks;

/// <summary>
/// A Task's move into another Project of its Space: the Project, and optionally the Status it takes there.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Space and no position.</b> The Space is the route's and never changes; the Task lands at the end of its column in
/// the destination, so a body naming a <c>positionKey</c>, an <c>afterTaskId</c> or a <c>spaceId</c> is refused.
/// </para>
/// <para>
/// <b>A <see cref="StatusId"/> left out or <c>null</c> keeps the Task's Status</b>, and is refused
/// <c>409 status-destination-required</c> when the destination's set lacks it. One that is named is applied, even when the
/// Task's own would have been kept.
/// </para>
/// </remarks>
/// <param name="ProjectId">The destination: another Project of the Task's Space.</param>
/// <param name="StatusId">A Status of the destination's effective set, or <c>null</c> to keep the Task's own.</param>
public sealed record MoveTaskToProjectRequest(Guid ProjectId, Guid? StatusId);
