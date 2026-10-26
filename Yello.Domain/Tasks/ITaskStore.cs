namespace Yello.Domain.Tasks;

/// <summary>
/// Creates, reads, edits and deletes Tasks, and the Labels applied to them, in the Space the current unit of work
/// is scoped to. FR-19, FR-20, FR-22, FR-23; stories 2.2, 2.3, 2.4 and 2.5.
/// </summary>
/// <remarks>
/// <b>Never asked about another Space.</b> Every call runs inside the request's Space-scoped unit of work,
/// where both isolation layers are already narrowed, so a Project or Task in another Space is invisible here
/// exactly as one that never existed is. Creation takes the resolved Space only to stamp it on the row.
/// </remarks>
public interface ITaskStore
{
    /// <summary>
    /// Creates a Task at the bottom of its Status's column in a Project of the resolved Space.
    /// </summary>
    /// <param name="spaceId">The resolved Space, from the pipeline's context and never from the request.</param>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The Status, the first of the Project's effective set.</param>
    /// <param name="title">The title, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The created Task, or <c>null</c> when no Project with that id is visible.</returns>
    Task<TaskItem?> CreateAsync(Guid spaceId, Guid projectId, Guid statusId, string title, CancellationToken cancellationToken);

    /// <summary>
    /// Reads one Task of the resolved Space.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Task, or <c>null</c> when no Task with that id is visible.</returns>
    Task<TaskItem?> FindAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>
    /// Applies an edit to one Task of the resolved Space and saves it. A Status that differs from the Task's
    /// own places it after the last key of its new column (story 2.3, open question 3 (a)); the same Status
    /// writes no key.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="edit">What changes, already validated.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Task as stored, or <c>null</c> when no Task with that id is visible.</returns>
    Task<TaskItem?> EditAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the Labels applied to one Task of the resolved Space with exactly <paramref name="labelIds"/>, and
    /// saves it. Every Label is read under a lock first, so one deleted, foreign or unknown refuses the whole set
    /// before anything is written (story 2.4, AC2, AC10).
    /// </summary>
    /// <param name="taskId">The Task, already known to be visible.</param>
    /// <param name="labelIds">The whole new set, without repeats; empty clears it.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>true</c> when applied; <c>false</c> when any of the Labels is not visible in the Space.</returns>
    Task<bool> ReplaceLabelsAsync(Guid taskId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken);

    /// <summary>
    /// The Labels applied to each of <paramref name="taskIds"/>, each list by Label name and then id (story 2.4,
    /// open question 12). One read for the whole set, never one per Task.
    /// </summary>
    /// <param name="taskIds">The Tasks.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Label ids per Task id; a Task carrying none is absent.</returns>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes one Task of the resolved Space, with the Labels applied to it, immediately and irreversibly (story 2.5,
    /// AC3, AC7). Its Labels stay; no other Task's position key changes.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the deletion came to.</returns>
    Task<TaskDeletion> DeleteAsync(Guid taskId, CancellationToken cancellationToken);
}

/// <summary>
/// What a Task deletion came to. No "already deleted": that would tell a caller an id once existed (AD-3).
/// </summary>
public enum TaskDeletion
{
    /// <summary>
    /// The Task and its applications were deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// No Task in the resolved Space has the id, whether it never existed, was deleted, or is another Space's.
    /// </summary>
    NotFound,
}
