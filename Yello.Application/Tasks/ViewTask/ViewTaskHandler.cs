using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.ViewTask;

/// <summary>
/// Reads one Task of the active Space, with the Labels it carries. FR-20; stories 2.3 and 2.4.
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
    public async Task<ViewedTask?> HandleAsync(Guid taskId, CancellationToken cancellationToken)
    {
        if (await store.FindAsync(taskId, cancellationToken).ConfigureAwait(false) is not { } task)
        {
            return null;
        }

        var labels = await store.LabelIdsOfAsync([taskId], cancellationToken).ConfigureAwait(false);

        return new ViewedTask(task, labels.GetValueOrDefault(taskId, []));
    }
}

/// <summary>
/// One Task as read, with its Labels.
/// </summary>
/// <param name="Task">The Task.</param>
/// <param name="LabelIds">The Labels it carries, by name then id (story 2.4, open question 12).</param>
public sealed record ViewedTask(TaskItem Task, IReadOnlyList<Guid> LabelIds);
