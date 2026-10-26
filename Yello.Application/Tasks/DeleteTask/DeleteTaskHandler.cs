using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.DeleteTask;

/// <summary>
/// Deletes one Task of the active Space, with the Labels applied to it, immediately and irreversibly. FR-23's request
/// path; story 2.5, AC1, AC3, AC7.
/// </summary>
/// <remarks>
/// <para>
/// <b>No trash and no restore</b> (PRD §6.2): the row is gone when this answers. The Labels stay; only their applications
/// to this Task go, by the database's cascade.
/// </para>
/// <para>
/// <b>No Role, no Space</b> (AR-3): the pipeline resolved the Space and checked <c>ManageTasks</c>. Another Space's Task
/// id matches nothing here, exactly like an id that never existed.
/// </para>
/// </remarks>
/// <param name="store">Deletes the Task.</param>
public sealed class DeleteTaskHandler(ITaskStore store)
{
    /// <summary>
    /// Deletes the Task.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the deletion came to.</returns>
    public Task<TaskDeletion> HandleAsync(Guid taskId, CancellationToken cancellationToken) =>
        store.DeleteAsync(taskId, cancellationToken);
}
