using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.MoveTask;

/// <summary>
/// Moves a Task of the active Space to a Status and a position within it, chosen by neighbour. FR-29 ("Move and
/// reorder a Task by explicit control"); story 2.6, AC3, AC9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space, checked
/// <c>ManageTasks</c> and replayed an <c>Idempotency-Key</c> it had already seen. This slice reads no Role, counts nothing
/// and consults no bound - a move adds no Task.
/// </para>
/// <para>
/// <b>The Status is checked before anything is written</b> against the effective set of the Task's own Project (AD-17),
/// exactly as the edit checks it, so another Space's Status id is the same validation refusal as a random one.
/// </para>
/// <para>
/// <b>Where the Task lands is the store's to decide under its lock</b>: the request names a neighbour, never a key
/// (FR-35, AR-21), and the neighbour's current successor is read only once the Project's Task range is held.
/// </para>
/// </remarks>
/// <param name="store">Reads and moves the Task.</param>
/// <param name="statuses">Reads the Space's Status definitions.</param>
public sealed class MoveTaskHandler(ITaskStore store, IStatusDefinitionReader statuses)
{
    /// <summary>
    /// Moves the Task.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="statusId">The destination Status.</param>
    /// <param name="afterTaskId">The Task to land after, or <c>null</c> for the top of the column.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the Task as stored when it was moved.</returns>
    public async Task<MoveTaskResult> HandleAsync(Guid taskId, Guid statusId, Guid? afterTaskId, CancellationToken cancellationToken)
    {
        var task = await store.FindAsync(taskId, cancellationToken).ConfigureAwait(false);

        if (task is null)
        {
            return MoveTaskResult.NotFound;
        }

        // Until story 6.1 adds a Project's own delta, every Project's effective set is its Space's definitions.
        var effective = EffectiveStatusSet.Of(await statuses.ListAsync(cancellationToken).ConfigureAwait(false));

        if (!EffectiveStatusSet.Contains(effective, statusId))
        {
            return MoveTaskResult.StatusOutsideTheEffectiveSet;
        }

        var move = await store.MoveAsync(taskId, statusId, afterTaskId, cancellationToken).ConfigureAwait(false);

        if (move.Outcome != TaskMoveOutcome.Moved)
        {
            return move.Outcome == TaskMoveOutcome.NotFound ? MoveTaskResult.NotFound : MoveTaskResult.PositionUnavailable;
        }

        var labels = await store.LabelIdsOfAsync([taskId], cancellationToken).ConfigureAwait(false);

        return MoveTaskResult.Moved(move.Task!, labels.GetValueOrDefault(taskId, []));
    }
}

/// <summary>
/// The outcome of <see cref="MoveTaskHandler.HandleAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Task">The Task as stored, when <see cref="Outcome"/> is <see cref="MoveTaskOutcome.Moved"/>.</param>
/// <param name="LabelIds">The Labels the Task carries, by name then id; empty unless moved.</param>
public sealed record MoveTaskResult(MoveTaskOutcome Outcome, TaskItem? Task, IReadOnlyList<Guid> LabelIds)
{
    /// <summary>
    /// No Task in the active Space has the id.
    /// </summary>
    public static MoveTaskResult NotFound { get; } = new(MoveTaskOutcome.NotFound, null, []);

    /// <summary>
    /// The Status is not in the Task's Project's effective set; nothing was written.
    /// </summary>
    public static MoveTaskResult StatusOutsideTheEffectiveSet { get; } = new(MoveTaskOutcome.StatusOutsideTheEffectiveSet, null, []);

    /// <summary>
    /// The neighbour is not a Task of the destination column, or the gap has no room; nothing was written.
    /// </summary>
    public static MoveTaskResult PositionUnavailable { get; } = new(MoveTaskOutcome.PositionUnavailable, null, []);

    /// <summary>
    /// The move was applied, or the Task was already there.
    /// </summary>
    /// <param name="task">The Task as stored.</param>
    /// <param name="labelIds">The Labels it carries, by name then id.</param>
    /// <returns>The result.</returns>
    public static MoveTaskResult Moved(TaskItem task, IReadOnlyList<Guid> labelIds) => new(MoveTaskOutcome.Moved, task, labelIds);
}

/// <summary>
/// What a move came to.
/// </summary>
public enum MoveTaskOutcome
{
    /// <summary>
    /// The Task is where it was asked to go.
    /// </summary>
    Moved,

    /// <summary>
    /// No Task in the active Space has the id.
    /// </summary>
    NotFound,

    /// <summary>
    /// The Status is not in the Task's Project's effective set.
    /// </summary>
    StatusOutsideTheEffectiveSet,

    /// <summary>
    /// The neighbour is not one, or no key fits (story 2.6, open questions 3 and 10).
    /// </summary>
    PositionUnavailable,
}
