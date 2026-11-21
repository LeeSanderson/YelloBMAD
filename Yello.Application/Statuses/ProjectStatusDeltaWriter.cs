using Yello.Domain;
using Yello.Domain.Boards;
using Yello.Domain.Statuses;

namespace Yello.Application.Statuses;

/// <summary>
/// The one way a Project's Status delta is written: under the Project's lock, against the set read after the lock was
/// granted, checked as the set it would leave, and answered with the set as the read then returns it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Checked after the lock, never before.</b> Two adds of one name both pass a check made before it; under it the second
/// reads the first's Status and is refused. The check is the effective set's own - an index cannot see a renamed default's
/// name, which lives in the delta.
/// </para>
/// <para>
/// <b>A refusal writes nothing</b>: the check runs before the store is called, and the endpoint's error result rolls the
/// unit of work back if an index refuses what the check admitted.
/// </para>
/// </remarks>
/// <param name="store">Takes the lock and records the write.</param>
/// <param name="statuses">Reads the Space's defaults and the Project's delta.</param>
/// <param name="boards">Counts the Project's Tasks per Status.</param>
/// <param name="identifiers">Produces the ids of what the write adds.</param>
/// <param name="clock">Stamps the write.</param>
public sealed class ProjectStatusDeltaWriter(
    IProjectStatusStore store,
    IStatusDefinitionReader statuses,
    IBoardReader boards,
    IIdentifierGenerator identifiers,
    TimeProvider clock)
{
    /// <summary>
    /// Plans and records one write.
    /// </summary>
    /// <param name="spaceId">The active Space, from the pipeline's context.</param>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="plan">
    /// Plans the write on the draft, or answers why it cannot be made; <see cref="ProjectStatusWriteOutcome.Written"/> means
    /// planned.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The outcome, and the Project's set when it was written.</returns>
    public Task<ProjectStatusWriteResult> WriteAsync(
        Guid spaceId,
        Guid projectId,
        Func<ProjectStatusDeltaDraft, ProjectStatusWriteOutcome> plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return WriteUnderLockAsync(spaceId, projectId, (draft, _) => Task.FromResult(plan(draft)), cancellationToken);
    }

    /// <summary>
    /// Plans and records one write whose plan reads more under the lock, such as a Status's occupants.
    /// </summary>
    /// <param name="spaceId">The active Space, from the pipeline's context.</param>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="plan">As for the synchronous overload.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The outcome, and the Project's set when it was written.</returns>
    public Task<ProjectStatusWriteResult> WriteAsync(
        Guid spaceId,
        Guid projectId,
        Func<ProjectStatusDeltaDraft, CancellationToken, Task<ProjectStatusWriteOutcome>> plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return WriteUnderLockAsync(spaceId, projectId, plan, cancellationToken);
    }

    private async Task<ProjectStatusWriteResult> WriteUnderLockAsync(
        Guid spaceId,
        Guid projectId,
        Func<ProjectStatusDeltaDraft, CancellationToken, Task<ProjectStatusWriteOutcome>> plan,
        CancellationToken cancellationToken)
    {
        if (!await store.LockAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return ProjectStatusWriteResult.Refused(ProjectStatusWriteOutcome.NotFound);
        }

        var defaults = await statuses.ListAsync(cancellationToken).ConfigureAwait(false);
        var delta = await statuses.DeltaOfAsync(projectId, cancellationToken).ConfigureAwait(false);
        var draft = new ProjectStatusDeltaDraft(
            spaceId,
            projectId,
            EffectiveStatusSet.Of(defaults, delta),
            delta,
            identifiers,
            clock.GetUtcNow(),
            (statusId, token) => boards.CountTasksAsync(projectId, statusId, token));

        var planned = await plan(draft, cancellationToken).ConfigureAwait(false);

        if (planned != ProjectStatusWriteOutcome.Written)
        {
            return ProjectStatusWriteResult.Refused(planned);
        }

        var verdict = EffectiveStatusSet.Check(EffectiveStatusSet.Of(defaults, draft.After()), draft.NamedStatusId);

        if (verdict != StatusDeltaVerdict.Admitted)
        {
            return ProjectStatusWriteResult.Refused(verdict == StatusDeltaVerdict.NameTaken ? ProjectStatusWriteOutcome.NameTaken : ProjectStatusWriteOutcome.SetEmpty);
        }

        if (!await RecordAsync(projectId, draft, cancellationToken).ConfigureAwait(false))
        {
            return ProjectStatusWriteResult.Refused(ProjectStatusWriteOutcome.NameTaken);
        }

        return new ProjectStatusWriteResult(ProjectStatusWriteOutcome.Written, await ReadAsync(projectId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Every key to a definition is <c>NO ACTION</c>, so the order is fixed: the Tasks leave the removed Status first, then
    /// the operations naming it go (its own, and the places anchored on it, which the re-anchored places replace), and an
    /// owned definition is deleted last, when nothing refers to it.
    /// </summary>
    private async Task<bool> RecordAsync(Guid projectId, ProjectStatusDeltaDraft draft, CancellationToken cancellationToken)
    {
        if (draft.RemovedStatusId is { } removed)
        {
            if (draft.DestinationStatusId is { } destination)
            {
                await store.MigrateTasksAsync(projectId, removed, destination, cancellationToken).ConfigureAwait(false);
            }

            await store.DeleteOperationsAsync(projectId, removed, kind: null, cancellationToken).ConfigureAwait(false);
        }

        if (draft.RestoredStatusId is { } restored)
        {
            await store.DeleteOperationsAsync(projectId, restored, StatusDeltaKind.Remove, cancellationToken).ConfigureAwait(false);
        }

        if (!await store.WriteAsync(draft.Added, draft.Operations, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        if (draft is { RemovedStatusId: { } owned, RemovesOwnedDefinition: true })
        {
            await store.DeleteOwnedDefinitionAsync(projectId, owned, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Reads one Project's set and its Task counts, as the read and every write answer it.
    /// </summary>
    /// <param name="projectId">The Project, already known to be the active Space's.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The set.</returns>
    internal async Task<ProjectStatusSet> ReadAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var effective = await statuses.EffectiveOfAsync(projectId, cancellationToken).ConfigureAwait(false);
        var counts = await boards.CountTasksByStatusAsync(projectId, cancellationToken).ConfigureAwait(false);

        return new ProjectStatusSet(effective, counts);
    }
}
