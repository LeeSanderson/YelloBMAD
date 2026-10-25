namespace Yello.Domain.Tasks;

/// <summary>
/// Creates, reads and edits Tasks in the Space the current unit of work is scoped to. FR-19, FR-20; stories
/// 2.2 and 2.3.
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
}
