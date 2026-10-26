namespace Yello.Domain.Tasks;

/// <summary>
/// What one edit of a Task changes: any of its title, Status, due date and Labels, each already validated. FR-20;
/// story 2.3, open question 1 (a); story 2.4, open question 1 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>An attribute the request did not name is left alone.</b> <see cref="Title"/> and <see cref="StatusId"/>
/// are <c>null</c> when absent - neither may be cleared - and the due date, which may be cleared, says whether
/// it changes with <see cref="ChangesDueDate"/>, so "absent" and "set to none" stay distinct. The Labels say
/// whether they change with <see cref="ChangesLabels"/> for the same reason: an empty set clears them.
/// </para>
/// <para>
/// <b>The Labels are the whole set</b> (story 2.4, open question 1 (a)): the Task carries exactly
/// <see cref="LabelIds"/> afterwards. Which of them exist in the Space is the store's to decide, under its lock,
/// because a Label can be deleted between this record being built and the edit being applied (AC10).
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
/// <param name="ChangesLabels">Whether the Task's Labels change.</param>
/// <param name="LabelIds">
/// The Task's whole new set of Labels, without repeats; read only when <see cref="ChangesLabels"/>.
/// </param>
public sealed record TaskEdit(
    string? Title,
    Guid? StatusId,
    bool ChangesDueDate,
    DateTimeOffset? DueDate,
    bool ChangesLabels = false,
    IReadOnlyList<Guid>? LabelIds = null);
