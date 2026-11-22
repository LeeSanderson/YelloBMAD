namespace Yello.Contracts.Tasks;

/// <summary>
/// The move of every Task of one Status into another Project of the Space: the Project, and optionally the one Status they
/// all take there. The source Project and Status are the route's.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Space, no position and no list of Tasks.</b> The Tasks moved are every Task holding the Status when the move is
/// granted its locks, so a body naming <c>taskIds</c>, a <c>positionKey</c>, an <c>afterTaskId</c> or a <c>spaceId</c> is
/// refused.
/// </para>
/// <para>
/// <b>A <see cref="StatusId"/> left out or <c>null</c> keeps the Status</b>, and is refused
/// <c>409 status-destination-required</c> when the destination's set lacks it and there are Tasks to move. One that is named
/// is applied, even when the Status would have been kept.
/// </para>
/// </remarks>
/// <param name="ProjectId">The destination: another Project of the Space.</param>
/// <param name="StatusId">A Status of the destination's effective set, or <c>null</c> to keep the source Status.</param>
public sealed record MoveStatusTasksToProjectRequest(Guid ProjectId, Guid? StatusId);
