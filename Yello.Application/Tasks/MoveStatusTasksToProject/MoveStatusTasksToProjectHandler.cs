using Yello.Application.Spaces;
using Yello.Application.Statuses;
using Yello.Domain.Bounds;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.MoveStatusTasksToProject;

/// <summary>
/// Moves every Task of one Status of a Project into another Project of the same Space, in one statement, keeping the Status
/// where the destination holds it and otherwise placing them all in one destination Status the request names.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both Projects' locks, taken together and sorted</b> (<see cref="ITaskStore.LockProjectsAsync"/>), as the single move
/// takes them: two single takes in source-then-destination order would let two opposite moves each hold one lock and wait for
/// the other. Every decision rests on reads made after they are granted - the source, its set, the destination, its set.
/// </para>
/// <para>
/// <b>The selection is decided under the locks and never sent by the caller.</b> Every writer that puts a Task into a column
/// or takes one out holds its Project's lock, so the Tasks counted after the locks are exactly the ones the statement writes:
/// a Task created into the Status before the move was granted its locks is moved, one moved out is not. A list of ids from
/// the caller could not even be complete, since a column is read in pages.
/// </para>
/// <para>
/// <b>One of the two cross-Project slices that count a bound</b>, for the single move's reason: the destination is in the
/// body, so the pipeline cannot count it, and a pipeline lock on it before the sorted pair would reopen the deadlock above.
/// The destination is counted here, after both locks, against all the Tasks the move adds at once. The source is never
/// counted.
/// </para>
/// <para>
/// <b>One Status at a time.</b> A mapping table covering several Statuses in one request was rejected: at the Tasks-per-Project
/// bound it would open a transaction-size question that no stated requirement answers.
/// </para>
/// </remarks>
/// <param name="store">Reads, locks, counts and moves the Tasks.</param>
/// <param name="statuses">Reads the Space's Status definitions and each Project's delta.</param>
/// <param name="bounds">Counts the destination's Tasks under its lock.</param>
public sealed class MoveStatusTasksToProjectHandler(ITaskStore store, IStatusDefinitionReader statuses, IBoundCounter bounds)
{
    /// <summary>
    /// Decides the move under both Projects' locks and writes it; every outcome but a move writes nothing.
    /// </summary>
    /// <param name="sourceProjectId">The Project the Tasks are in, from the route.</param>
    /// <param name="sourceStatusId">The Status whose Tasks move, from the route.</param>
    /// <param name="destinationProjectId">The Project they move into.</param>
    /// <param name="statusId">The Status they take there, or <c>null</c> to keep their own.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, the Status the Tasks hold in the destination and how many moved.</returns>
    public async Task<MoveStatusTasksToProjectResult> HandleAsync(
        Guid sourceProjectId,
        Guid sourceStatusId,
        Guid destinationProjectId,
        Guid? statusId,
        CancellationToken cancellationToken)
    {
        // Plain reads first, so no lock is ever taken for a Project the caller cannot see.
        if (!await store.ProjectExistsAsync(sourceProjectId, cancellationToken).ConfigureAwait(false))
        {
            return MoveStatusTasksToProjectResult.NotFound;
        }

        if (destinationProjectId == sourceProjectId
            || !await store.ProjectExistsAsync(destinationProjectId, cancellationToken).ConfigureAwait(false))
        {
            return MoveStatusTasksToProjectResult.ProjectInvalid;
        }

        await store.LockProjectsAsync([sourceProjectId, destinationProjectId], cancellationToken).ConfigureAwait(false);

        return await DecideUnderTheLocksAsync(sourceProjectId, sourceStatusId, destinationProjectId, statusId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MoveStatusTasksToProjectResult> DecideUnderTheLocksAsync(
        Guid sourceProjectId,
        Guid sourceStatusId,
        Guid destinationProjectId,
        Guid? statusId,
        CancellationToken cancellationToken)
    {
        if (!await store.ProjectExistsAsync(sourceProjectId, cancellationToken).ConfigureAwait(false))
        {
            return MoveStatusTasksToProjectResult.NotFound;
        }

        var source = await statuses.EffectiveOfAsync(sourceProjectId, cancellationToken).ConfigureAwait(false);

        if (!EffectiveStatusSet.Contains(source, sourceStatusId))
        {
            return MoveStatusTasksToProjectResult.NotFound;
        }

        if (!await store.ProjectExistsAsync(destinationProjectId, cancellationToken).ConfigureAwait(false))
        {
            return MoveStatusTasksToProjectResult.ProjectInvalid;
        }

        var destination = await statuses.EffectiveOfAsync(destinationProjectId, cancellationToken).ConfigureAwait(false);
        var decision = CrossProjectStatus.Decide(destination, sourceStatusId, statusId);

        if (decision.Outcome == CrossProjectStatusOutcome.Invalid)
        {
            return MoveStatusTasksToProjectResult.StatusDestinationInvalid;
        }

        var selected = await store.CountInStatusAsync(sourceProjectId, sourceStatusId, cancellationToken).ConfigureAwait(false);

        // An empty Status needs no destination Status, as an empty Status's removal needs none.
        if (selected == 0)
        {
            return MoveStatusTasksToProjectResult.Moved(decision.Landing, 0);
        }

        return decision.Outcome == CrossProjectStatusOutcome.Lands
            ? await CountAndMoveAsync(sourceProjectId, sourceStatusId, destinationProjectId, decision.Landing, selected, cancellationToken).ConfigureAwait(false)
            : MoveStatusTasksToProjectResult.StatusDestinationRequired;
    }

    private async Task<MoveStatusTasksToProjectResult> CountAndMoveAsync(
        Guid sourceProjectId,
        Guid sourceStatusId,
        Guid destinationProjectId,
        Guid landing,
        int selected,
        CancellationToken cancellationToken)
    {
        var bound = BoundRegistry.For(Bound.TasksPerProject);
        var count = await bounds.CountAsync(bound.Bound, destinationProjectId, cancellationToken).ConfigureAwait(false);

        if (count + selected > bound.Limit)
        {
            return MoveStatusTasksToProjectResult.BoundReached(bound);
        }

        var written = await store.MoveStatusToProjectAsync(sourceProjectId, sourceStatusId, destinationProjectId, landing, cancellationToken).ConfigureAwait(false);

        // Under both locks the two cannot differ. If they ever do, failing rolls the unit of work back rather than answering a
        // count that was not written.
        if (written != selected)
        {
            throw new InvalidOperationException($"The bulk move counted {selected} Tasks under its locks and wrote {written}.");
        }

        return MoveStatusTasksToProjectResult.Moved(landing, written);
    }
}

/// <summary>
/// The outcome of <see cref="MoveStatusTasksToProjectHandler.HandleAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="StatusId">The Status the Tasks hold in the destination, when moved.</param>
/// <param name="MovedCount">How many Tasks were written; zero unless moved.</param>
/// <param name="Bound">The bound reached, when <see cref="Outcome"/> is <see cref="MoveStatusTasksToProjectOutcome.BoundReached"/>.</param>
public sealed record MoveStatusTasksToProjectResult(MoveStatusTasksToProjectOutcome Outcome, Guid StatusId, int MovedCount, BoundEntry? Bound)
{
    /// <summary>
    /// The source Project is not one of the active Space's, or its set does not hold the Status.
    /// </summary>
    public static MoveStatusTasksToProjectResult NotFound { get; } = new(MoveStatusTasksToProjectOutcome.NotFound, Guid.Empty, 0, null);

    /// <summary>
    /// The destination is not another Project of the active Space; nothing was written.
    /// </summary>
    public static MoveStatusTasksToProjectResult ProjectInvalid { get; } = new(MoveStatusTasksToProjectOutcome.ProjectInvalid, Guid.Empty, 0, null);

    /// <summary>
    /// The destination's set lacks the Status and the request named none; nothing was written.
    /// </summary>
    public static MoveStatusTasksToProjectResult StatusDestinationRequired { get; } = new(MoveStatusTasksToProjectOutcome.StatusDestinationRequired, Guid.Empty, 0, null);

    /// <summary>
    /// The Status the request named is not in the destination's set; nothing was written.
    /// </summary>
    public static MoveStatusTasksToProjectResult StatusDestinationInvalid { get; } = new(MoveStatusTasksToProjectOutcome.StatusDestinationInvalid, Guid.Empty, 0, null);

    /// <summary>
    /// The destination cannot take every selected Task within its bound; nothing was written.
    /// </summary>
    /// <param name="bound">The bound reached.</param>
    /// <returns>The result.</returns>
    public static MoveStatusTasksToProjectResult BoundReached(BoundEntry bound) => new(MoveStatusTasksToProjectOutcome.BoundReached, Guid.Empty, 0, bound);

    /// <summary>
    /// Every selected Task is in the destination.
    /// </summary>
    /// <param name="statusId">The Status they hold there.</param>
    /// <param name="movedCount">How many were written.</param>
    /// <returns>The result.</returns>
    public static MoveStatusTasksToProjectResult Moved(Guid statusId, int movedCount) => new(MoveStatusTasksToProjectOutcome.Moved, statusId, movedCount, null);
}

/// <summary>
/// How a bulk move into another Project ends.
/// </summary>
public enum MoveStatusTasksToProjectOutcome
{
    /// <summary>
    /// Every selected Task is in the destination; none, when the Status held none.
    /// </summary>
    Moved,

    /// <summary>
    /// The source Project is missing, another Space's or deleted meanwhile, or its set does not hold the Status.
    /// </summary>
    NotFound,

    /// <summary>
    /// The destination is missing, another Space's, deleted meanwhile, or the source itself.
    /// </summary>
    ProjectInvalid,

    /// <summary>
    /// No Status was named and the destination's set lacks the source Status.
    /// </summary>
    StatusDestinationRequired,

    /// <summary>
    /// The Status named is not in the destination's set.
    /// </summary>
    StatusDestinationInvalid,

    /// <summary>
    /// The destination cannot take every selected Task.
    /// </summary>
    BoundReached,
}
