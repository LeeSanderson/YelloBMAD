using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.ViewTask;

/// <summary>
/// Reads one Task of the active Space. FR-20; story 2.3.
/// </summary>
/// <remarks>
/// <b>No Space and no Role here</b> (AR-3). The unit of work is already scoped to the active Space, so a Task
/// id from another Space matches nothing - exactly like an id that never existed.
/// </remarks>
/// <param name="store">Reads the Task.</param>
public sealed class ViewTaskHandler(ITaskStore store)
{
    /// <summary>
    /// Reads the Task.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Task, or <c>null</c> when no Task in the active Space has that id.</returns>
    public Task<TaskItem?> HandleAsync(Guid taskId, CancellationToken cancellationToken) =>
        store.FindAsync(taskId, cancellationToken);
}
