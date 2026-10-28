namespace Yello.Contracts.Tasks;

/// <summary>
/// A Task move: the Status it goes to and the Task it lands after. FR-29; story 2.6, open question 2 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>A position by neighbour, never a key.</b> The server places the Task between <see cref="AfterTaskId"/> and that
/// Task's successor as it finds them under its lock, so two moves after one neighbour both land and converge (AC5). A
/// body carrying a <c>positionKey</c> member is refused, so a client can never believe it wrote one (AD-15, AR-21).
/// </para>
/// <para>
/// <b>Both members are required.</b> <c>"afterTaskId": null</c> is the top of the column; a body leaving it out is
/// refused, because where the Task goes is the person's choice and has no default.
/// </para>
/// <para>
/// <b>Session only</b> (FR-35): a position within a column is readable over the API and never writable, so a Token on
/// the move route is answered as no credential. A Status change over the edit still appends to the new column.
/// </para>
/// </remarks>
/// <param name="StatusId">The destination Status; one of the Task's Project's effective set.</param>
/// <param name="AfterTaskId">
/// The Task to land immediately after, in the destination column of the same Project, or <c>null</c> for the top.
/// </param>
public sealed record MoveTaskRequest(Guid StatusId, Guid? AfterTaskId);
