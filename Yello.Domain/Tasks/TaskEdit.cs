namespace Yello.Domain.Tasks;

/// <summary>
/// What one edit of a Task changes: any of its title, Status and due date, each already validated. FR-20;
/// story 2.3, open question 1 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>An attribute the request did not name is left alone.</b> <see cref="Title"/> and <see cref="StatusId"/>
/// are <c>null</c> when absent - neither may be cleared - and the due date, which may be cleared, says whether
/// it changes with <see cref="ChangesDueDate"/>, so "absent" and "set to none" stay distinct.
/// </para>
/// <para>
/// <b>No description, Project, Space or position key</b>: the description is FR-31's (Epic 7), the Project is
/// FR-41's own move, the Space never changes, and the position key is the server's to place (AD-15).
/// </para>
/// </remarks>
/// <param name="Title">The new title, already normalised, or <c>null</c> to leave it.</param>
/// <param name="StatusId">The new Status, already checked against the effective set, or <c>null</c> to leave it.</param>
/// <param name="ChangesDueDate">Whether the due date changes.</param>
/// <param name="DueDate">The new due date in UTC, or <c>null</c> for none; read only when <see cref="ChangesDueDate"/>.</param>
public sealed record TaskEdit(string? Title, Guid? StatusId, bool ChangesDueDate, DateTimeOffset? DueDate);
