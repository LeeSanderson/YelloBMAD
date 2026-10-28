namespace Yello.Domain.Tasks;

/// <summary>
/// Creates, reads, edits and deletes Tasks, and the Labels applied to them, in the Space the current unit of work
/// is scoped to. FR-19, FR-20, FR-22, FR-23, FR-29; stories 2.2, 2.3, 2.4, 2.5 and 2.6.
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

    /// <summary>
    /// Moves one Task of the resolved Space to <paramref name="statusId"/>, immediately after
    /// <paramref name="afterTaskId"/> - or to the top of that column when it is <c>null</c> - and saves it (story 2.6, AC3,
    /// AC9). Only the moved Task's Status and position key are written; no sibling's key changes (AD-15). Never changes
    /// the Task's Project.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="statusId">The destination Status, already checked against the Task's Project's effective set.</param>
    /// <param name="afterTaskId">The Task to land after, or <c>null</c> for the top of the column.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the move came to.</returns>
    Task<TaskMove> MoveAsync(Guid taskId, Guid statusId, Guid? afterTaskId, CancellationToken cancellationToken);
}

/// <summary>
/// What a Task move came to (story 2.6). No "the neighbour is in another Space": that would tell a caller an id exists
/// elsewhere (AD-3, I-13).
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Task">The Task as stored, when <see cref="Outcome"/> is <see cref="TaskMoveOutcome.Moved"/>.</param>
public sealed record TaskMove(TaskMoveOutcome Outcome, TaskItem? Task)
{
    /// <summary>
    /// No Task in the resolved Space has the id.
    /// </summary>
    public static TaskMove NotFound { get; } = new(TaskMoveOutcome.NotFound, null);

    /// <summary>
    /// The position asked for is not one: the neighbour is not a Task of the destination column of the same Project, or
    /// no key fits in the gap. Nothing was written.
    /// </summary>
    public static TaskMove PositionUnavailable { get; } = new(TaskMoveOutcome.PositionUnavailable, null);

    /// <summary>
    /// The Task is where it was asked to go.
    /// </summary>
    /// <param name="task">The Task as stored.</param>
    /// <returns>The outcome.</returns>
    public static TaskMove Moved(TaskItem task) => new(TaskMoveOutcome.Moved, task);
}

/// <summary>
/// What a Task move came to.
/// </summary>
public enum TaskMoveOutcome
{
    /// <summary>
    /// The Task is in the destination Status at the position asked for - written, or already there and left alone.
    /// </summary>
    Moved,

    /// <summary>
    /// No Task in the resolved Space has the id, whether it never existed, was deleted, or is another Space's.
    /// </summary>
    NotFound,

    /// <summary>
    /// The neighbour is absent, another Space's, another Project's or column's, deleted, or the moved Task itself; or no
    /// position key fits between it and its successor (story 2.6, open questions 3 and 10).
    /// </summary>
    PositionUnavailable,
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
