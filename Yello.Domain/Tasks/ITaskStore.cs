namespace Yello.Domain.Tasks;

/// <summary>
/// Creates Tasks in the Space the current unit of work is scoped to. FR-19; story 2.2.
/// </summary>
/// <remarks>
/// <b>Never asked about another Space.</b> Every call runs inside the request's Space-scoped unit of work,
/// where both isolation layers are already narrowed, so a Project in another Space is invisible here
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
}
