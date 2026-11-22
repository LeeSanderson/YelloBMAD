using Yello.Domain;
using Yello.Domain.Statuses;

namespace Yello.Application.Statuses.RemoveSpaceStatus;

/// <summary>
/// Takes one of the Space's defaults out of the Space and every Project of it, moving every Task that holds it, in one
/// transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own writer, not <see cref="SpaceDefaultStatusWriter"/>'s</b>: the other Space-level changes are judged against the
/// defaults and the deltas alone, while a removal also needs the Project list and the Task counts, read under locks those
/// changes never take.
/// </para>
/// <para>
/// <b>Locks before reads, in one order</b> (see <see cref="ISpaceDefaultStatusRemovalStore"/>): the Status-defaults lock,
/// the Project list, then the Tasks lock of every Project whose set holds the Status. The holders are known from the
/// defaults and deltas, which cannot change once the first lock is held, and the Tasks are counted only after the last
/// lock is granted, so a Task edited into the Status just before is counted and moved, and one edited just after is refused.
/// </para>
/// <para>
/// <b>A refusal writes nothing</b>, and every report names only Projects of this Space, read under its isolation.
/// </para>
/// </remarks>
/// <param name="store">Takes the Status-defaults lock and reads every delta and the Task counts.</param>
/// <param name="removals">Takes the Project list and Tasks locks and records the removal.</param>
/// <param name="statuses">Reads the Space's defaults.</param>
/// <param name="identifiers">Produces the ids of the re-anchored places.</param>
/// <param name="clock">Stamps them.</param>
public sealed class RemoveSpaceStatusHandler(
    ISpaceDefaultStatusStore store,
    ISpaceDefaultStatusRemovalStore removals,
    IStatusDefinitionReader statuses,
    IIdentifierGenerator identifiers,
    TimeProvider clock)
{
    /// <summary>
    /// Removes the default.
    /// </summary>
    /// <param name="request">The default, the Space-wide destination and each Project's own destination.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the defaults when the Status was removed.</returns>
    public Task<SpaceStatusWriteResult> HandleAsync(SpaceStatusRemovalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return RemoveUnderLocksAsync(request, cancellationToken);
    }

    private async Task<SpaceStatusWriteResult> RemoveUnderLocksAsync(SpaceStatusRemovalRequest request, CancellationToken cancellationToken)
    {
        await store.LockAsync(cancellationToken).ConfigureAwait(false);

        var projects = await removals.LockProjectsAsync(cancellationToken).ConfigureAwait(false);
        var defaults = await statuses.ListAsync(cancellationToken).ConfigureAwait(false);
        var deltas = await store.DeltasAsync(cancellationToken).ConfigureAwait(false);

        await removals.LockProjectTasksAsync(SpaceDefaultStatusRemoval.Holders(defaults, deltas, [.. projects.Keys], request.StatusId), cancellationToken).ConfigureAwait(false);

        var occupancy = await store.CountTasksByProjectAsync(request.StatusId, cancellationToken).ConfigureAwait(false);
        var plan = SpaceDefaultStatusRemoval.Plan(
            new SpaceStatusRemovalState(defaults, deltas, [.. projects.Keys], occupancy),
            request,
            identifiers,
            clock.GetUtcNow());

        switch (plan.Verdict)
        {
            case SpaceDefaultStatusVerdict.Admitted:
                await removals.RemoveAsync(plan, cancellationToken).ConfigureAwait(false);

                return SpaceStatusWriteResult.Written(await statuses.ListAsync(cancellationToken).ConfigureAwait(false));

            case SpaceDefaultStatusVerdict.DestinationRequired or SpaceDefaultStatusVerdict.ProjectDestinationRequired:
                return SpaceStatusWriteResult.Refused(Outcome(plan.Verdict)) with { Removal = Reported(plan.Report, projects) };

            default:
                return SpaceStatusWriteResult.Refused(Outcome(plan.Verdict)) with { Projects = Named(plan.Projects, projects) };
        }
    }

    private static SpaceStatusWriteOutcome Outcome(SpaceDefaultStatusVerdict verdict) => verdict switch
    {
        SpaceDefaultStatusVerdict.SetEmpty => SpaceStatusWriteOutcome.SetEmpty,
        SpaceDefaultStatusVerdict.DestinationRequired => SpaceStatusWriteOutcome.DestinationRequired,
        SpaceDefaultStatusVerdict.DestinationInvalid => SpaceStatusWriteOutcome.DestinationInvalid,
        SpaceDefaultStatusVerdict.ProjectDestinationRequired => SpaceStatusWriteOutcome.ProjectDestinationRequired,
        _ => SpaceStatusWriteOutcome.NotFound,
    };

    private static List<SpaceStatusOccupancyReport> Reported(IReadOnlyList<SpaceDefaultStatusOccupancy> report, IReadOnlyDictionary<Guid, string> projects) =>
    [
        .. report
            .Select(entry => new SpaceStatusOccupancyReport(
                entry.ProjectId,
                projects.GetValueOrDefault(entry.ProjectId) ?? string.Empty,
                entry.TaskCount,
                [.. entry.After.Select(status => new StatusChoice(status.Id, status.Name))]))
            .OrderBy(entry => entry.ProjectName, StringComparer.Ordinal)
            .ThenBy(entry => entry.ProjectId),
    ];

    private static List<SpaceStatusProject> Named(IReadOnlyList<Guid> projectIds, IReadOnlyDictionary<Guid, string> projects) =>
    [
        .. projectIds
            .Select(id => new SpaceStatusProject(id, projects.GetValueOrDefault(id) ?? string.Empty))
            .OrderBy(project => project.Name, StringComparer.Ordinal)
            .ThenBy(project => project.Id),
    ];
}
