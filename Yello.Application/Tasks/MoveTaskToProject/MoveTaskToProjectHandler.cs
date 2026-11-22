using Yello.Application.Spaces;
using Yello.Application.Statuses;
using Yello.Domain.Bounds;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.MoveTaskToProject;

/// <summary>
/// Moves a Task of the active Space into another Project of the same Space, keeping its Status where the destination holds
/// it and otherwise placing it in a destination Status the request names.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both Projects' locks, taken together and sorted</b> (<see cref="ITaskStore.LockProjectsAsync"/>): taken one by one in
/// source-then-destination order, a move from A to B and one from B to A would each hold one lock and wait for the other.
/// Every decision is made after they are granted, on reads made after them: the Task re-read - gone or no longer in the
/// source whose lock is held is not found, as the Status edit answers it - the destination re-read, since a Project deletion
/// holds its lock until it commits, and the destination's effective set.
/// </para>
/// <para>
/// <b>One of the two cross-Project slices that count a bound.</b> The destination is in the body, so the pipeline, which counts the Project a
/// route names before the endpoint, cannot count it; and a pipeline lock on the destination taken before this slice's sorted
/// pair would reopen the deadlock above. So the destination's Tasks-per-Project count is taken here, after both locks, with
/// the pipeline's own counter and limit. The source is never counted: a move out never exceeds it.
/// </para>
/// </remarks>
/// <param name="store">Reads, locks and moves the Task.</param>
/// <param name="statuses">Reads the Space's Status definitions and the destination's delta.</param>
/// <param name="bounds">Counts the destination's Tasks under its lock.</param>
public sealed class MoveTaskToProjectHandler(ITaskStore store, IStatusDefinitionReader statuses, IBoundCounter bounds)
{
    /// <summary>
    /// Decides the move under both Projects' locks and writes it; every outcome but a move writes nothing.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="destinationProjectId">The Project it moves into.</param>
    /// <param name="statusId">The Status it takes there, or <c>null</c> to keep its own.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the Task as stored when it moved.</returns>
    public async Task<MoveTaskToProjectResult> HandleAsync(
        Guid taskId,
        Guid destinationProjectId,
        Guid? statusId,
        CancellationToken cancellationToken)
    {
        // Plain reads first, so no lock is ever taken for an id the caller cannot see.
        var task = await store.FindAsync(taskId, cancellationToken).ConfigureAwait(false);

        if (task is null)
        {
            return MoveTaskToProjectResult.NotFound;
        }

        var sourceProjectId = task.ProjectId;

        if (destinationProjectId == sourceProjectId
            || !await store.ProjectExistsAsync(destinationProjectId, cancellationToken).ConfigureAwait(false))
        {
            return MoveTaskToProjectResult.ProjectInvalid;
        }

        await store.LockProjectsAsync([sourceProjectId, destinationProjectId], cancellationToken).ConfigureAwait(false);

        return await DecideUnderTheLocksAsync(taskId, sourceProjectId, destinationProjectId, statusId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MoveTaskToProjectResult> DecideUnderTheLocksAsync(
        Guid taskId,
        Guid sourceProjectId,
        Guid destinationProjectId,
        Guid? statusId,
        CancellationToken cancellationToken)
    {
        var task = await store.FindAsync(taskId, cancellationToken).ConfigureAwait(false);

        if (task is null || task.ProjectId != sourceProjectId)
        {
            return MoveTaskToProjectResult.NotFound;
        }

        if (!await store.ProjectExistsAsync(destinationProjectId, cancellationToken).ConfigureAwait(false))
        {
            return MoveTaskToProjectResult.ProjectInvalid;
        }

        var effective = await statuses.EffectiveOfAsync(destinationProjectId, cancellationToken).ConfigureAwait(false);
        var decision = CrossProjectStatus.Decide(effective, task.StatusId, statusId);

        return decision.Outcome switch
        {
            CrossProjectStatusOutcome.Lands => await CountAndMoveAsync(taskId, sourceProjectId, destinationProjectId, decision.Landing, cancellationToken).ConfigureAwait(false),
            CrossProjectStatusOutcome.Invalid => MoveTaskToProjectResult.StatusDestinationInvalid,
            _ => MoveTaskToProjectResult.StatusDestinationRequired,
        };
    }

    private async Task<MoveTaskToProjectResult> CountAndMoveAsync(
        Guid taskId,
        Guid sourceProjectId,
        Guid destinationProjectId,
        Guid landing,
        CancellationToken cancellationToken)
    {
        var bound = BoundRegistry.For(Bound.TasksPerProject);
        var count = await bounds.CountAsync(bound.Bound, destinationProjectId, cancellationToken).ConfigureAwait(false);

        if (count >= bound.Limit)
        {
            return MoveTaskToProjectResult.BoundReached(bound);
        }

        var moved = await store.MoveToProjectAsync(taskId, sourceProjectId, destinationProjectId, landing, cancellationToken).ConfigureAwait(false);

        if (moved is null)
        {
            return MoveTaskToProjectResult.NotFound;
        }

        var labels = await store.LabelIdsOfAsync([taskId], cancellationToken).ConfigureAwait(false);

        return MoveTaskToProjectResult.Moved(moved, labels.GetValueOrDefault(taskId, []));
    }
}

/// <summary>
/// The outcome of <see cref="MoveTaskToProjectHandler.HandleAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Task">The Task as stored, when it moved.</param>
/// <param name="LabelIds">The Labels the Task carries, by name then id; empty unless moved.</param>
/// <param name="Bound">The bound reached, when <see cref="Outcome"/> is <see cref="MoveTaskToProjectOutcome.BoundReached"/>.</param>
public sealed record MoveTaskToProjectResult(MoveTaskToProjectOutcome Outcome, TaskItem? Task, IReadOnlyList<Guid> LabelIds, BoundEntry? Bound)
{
    /// <summary>
    /// No Task in the active Space has the id, or it left the Project whose lock was taken for it.
    /// </summary>
    public static MoveTaskToProjectResult NotFound { get; } = new(MoveTaskToProjectOutcome.NotFound, null, [], null);

    /// <summary>
    /// The destination is not another Project of the active Space; nothing was written.
    /// </summary>
    public static MoveTaskToProjectResult ProjectInvalid { get; } = new(MoveTaskToProjectOutcome.ProjectInvalid, null, [], null);

    /// <summary>
    /// The destination's set lacks the Task's Status and the request named none; nothing was written.
    /// </summary>
    public static MoveTaskToProjectResult StatusDestinationRequired { get; } = new(MoveTaskToProjectOutcome.StatusDestinationRequired, null, [], null);

    /// <summary>
    /// The Status the request named is not in the destination's set; nothing was written.
    /// </summary>
    public static MoveTaskToProjectResult StatusDestinationInvalid { get; } = new(MoveTaskToProjectOutcome.StatusDestinationInvalid, null, [], null);

    /// <summary>
    /// The destination already holds as many Tasks as its bound admits; nothing was written.
    /// </summary>
    /// <param name="bound">The bound reached.</param>
    /// <returns>The result.</returns>
    public static MoveTaskToProjectResult BoundReached(BoundEntry bound) => new(MoveTaskToProjectOutcome.BoundReached, null, [], bound);

    /// <summary>
    /// The Task is in the destination.
    /// </summary>
    /// <param name="task">The Task as stored.</param>
    /// <param name="labelIds">The Labels it carries, by name then id.</param>
    /// <returns>The result.</returns>
    public static MoveTaskToProjectResult Moved(TaskItem task, IReadOnlyList<Guid> labelIds) => new(MoveTaskToProjectOutcome.Moved, task, labelIds, null);
}

/// <summary>
/// How a move into another Project ends.
/// </summary>
public enum MoveTaskToProjectOutcome
{
    /// <summary>
    /// The Task is in the destination.
    /// </summary>
    Moved,

    /// <summary>
    /// No Task in the active Space has the id, or it is no longer in the Project it was read in.
    /// </summary>
    NotFound,

    /// <summary>
    /// The destination is missing, another Space's, deleted meanwhile, or the Task's own Project.
    /// </summary>
    ProjectInvalid,

    /// <summary>
    /// No Status was named and the destination's set lacks the Task's.
    /// </summary>
    StatusDestinationRequired,

    /// <summary>
    /// The Status named is not in the destination's set.
    /// </summary>
    StatusDestinationInvalid,

    /// <summary>
    /// The destination is full.
    /// </summary>
    BoundReached,
}
