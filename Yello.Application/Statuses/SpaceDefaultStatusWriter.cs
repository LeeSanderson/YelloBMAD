using Yello.Domain.Statuses;

namespace Yello.Application.Statuses;

/// <summary>
/// The one way the Space's default Statuses are changed: under the Space-level Status lock, planned against the defaults
/// and every Project's delta read after the lock was granted, and answered with the defaults as the read then returns them.
/// </summary>
/// <remarks>
/// <b>A refusal writes nothing</b>: the plan is judged before the store is called, and the endpoint's error result rolls
/// the unit of work back if the default-name index refuses what the plan admitted. A rename's conflict report and a name
/// clash's Project list are read under the same lock, so they name exactly the Projects the plan judged.
/// </remarks>
/// <param name="store">Takes the lock, reads every Project's delta and records the change.</param>
/// <param name="statuses">Reads the Space's defaults.</param>
public sealed class SpaceDefaultStatusWriter(ISpaceDefaultStatusStore store, IStatusDefinitionReader statuses)
{
    /// <summary>
    /// Plans and records one change.
    /// </summary>
    /// <param name="plan">Plans the change from the defaults and every Project's delta.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The outcome, and the defaults when the change was written.</returns>
    public Task<SpaceStatusWriteResult> WriteAsync(
        Func<IReadOnlyList<StatusDefinition>, IReadOnlyDictionary<Guid, ProjectStatusDelta>, SpaceDefaultStatusChange> plan,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return WriteUnderLockAsync(plan, cancellationToken);
    }

    private async Task<SpaceStatusWriteResult> WriteUnderLockAsync(
        Func<IReadOnlyList<StatusDefinition>, IReadOnlyDictionary<Guid, ProjectStatusDelta>, SpaceDefaultStatusChange> plan,
        CancellationToken cancellationToken)
    {
        await store.LockAsync(cancellationToken).ConfigureAwait(false);

        var defaults = await statuses.ListAsync(cancellationToken).ConfigureAwait(false);
        var deltas = await store.DeltasAsync(cancellationToken).ConfigureAwait(false);
        var change = plan(defaults, deltas);

        switch (change.Verdict)
        {
            case SpaceDefaultStatusVerdict.Unchanged:
                return SpaceStatusWriteResult.Written(defaults);

            case SpaceDefaultStatusVerdict.Admitted:
                return await store.WriteAsync(change, cancellationToken).ConfigureAwait(false)
                    ? SpaceStatusWriteResult.Written(await statuses.ListAsync(cancellationToken).ConfigureAwait(false))
                    : SpaceStatusWriteResult.Refused(SpaceStatusWriteOutcome.NameTaken);

            case SpaceDefaultStatusVerdict.RenameConflict:
                return SpaceStatusWriteResult.Refused(SpaceStatusWriteOutcome.RenameConflict) with
                {
                    Conflicts = await ReportAsync(change, cancellationToken).ConfigureAwait(false),
                };

            case SpaceDefaultStatusVerdict.ProjectNameTaken:
                return SpaceStatusWriteResult.Refused(SpaceStatusWriteOutcome.NameTaken) with
                {
                    Projects = await NamedAsync(change.ClashingProjects, cancellationToken).ConfigureAwait(false),
                };

            default:
                return SpaceStatusWriteResult.Refused(Outcome(change.Verdict));
        }
    }

    private static SpaceStatusWriteOutcome Outcome(SpaceDefaultStatusVerdict verdict) => verdict switch
    {
        SpaceDefaultStatusVerdict.NameTaken => SpaceStatusWriteOutcome.NameTaken,
        SpaceDefaultStatusVerdict.PositionInvalid => SpaceStatusWriteOutcome.PositionInvalid,
        SpaceDefaultStatusVerdict.SetEmpty => SpaceStatusWriteOutcome.SetEmpty,
        _ => SpaceStatusWriteOutcome.NotFound,
    };

    private async Task<IReadOnlyList<SpaceStatusConflictReport>> ReportAsync(SpaceDefaultStatusChange change, CancellationToken cancellationToken)
    {
        var statusId = change.RenamedStatusId ?? Guid.Empty;
        var names = await store.ProjectNamesAsync([.. change.Conflicts.Select(conflict => conflict.ProjectId)], cancellationToken).ConfigureAwait(false);
        var counts = await store.CountTasksByProjectAsync(statusId, cancellationToken).ConfigureAwait(false);

        return
        [
            .. change.Conflicts
                .Select(conflict => new SpaceStatusConflictReport(
                    conflict.ProjectId,
                    names.GetValueOrDefault(conflict.ProjectId) ?? string.Empty,
                    conflict.Name,
                    counts.GetValueOrDefault(conflict.ProjectId)))
                .OrderBy(conflict => conflict.ProjectName, StringComparer.Ordinal)
                .ThenBy(conflict => conflict.ProjectId),
        ];
    }

    private async Task<IReadOnlyList<SpaceStatusProject>> NamedAsync(IReadOnlyList<Guid> projectIds, CancellationToken cancellationToken)
    {
        var names = await store.ProjectNamesAsync(projectIds, cancellationToken).ConfigureAwait(false);

        return
        [
            .. projectIds
                .Select(id => new SpaceStatusProject(id, names.GetValueOrDefault(id) ?? string.Empty))
                .OrderBy(project => project.Name, StringComparer.Ordinal)
                .ThenBy(project => project.Id),
        ];
    }
}

/// <summary>
/// The outcome of a change of the Space's defaults.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Defaults">The Space's defaults after the change, when it was written.</param>
public sealed record SpaceStatusWriteResult(SpaceStatusWriteOutcome Outcome, IReadOnlyList<StatusDefinition>? Defaults)
{
    /// <summary>
    /// On <see cref="SpaceStatusWriteOutcome.RenameConflict"/>: every Project that gave the default a name of its own.
    /// </summary>
    public IReadOnlyList<SpaceStatusConflictReport> Conflicts { get; init; } = [];

    /// <summary>
    /// On <see cref="SpaceStatusWriteOutcome.NameTaken"/>: the Projects whose set would hold the name twice, or none when the
    /// name is another default's.
    /// </summary>
    public IReadOnlyList<SpaceStatusProject> Projects { get; init; } = [];

    /// <summary>
    /// On a removal's <see cref="SpaceStatusWriteOutcome.DestinationRequired"/> or
    /// <see cref="SpaceStatusWriteOutcome.ProjectDestinationRequired"/>: the Projects it names, each with its Task count and
    /// its own set after the removal.
    /// </summary>
    public IReadOnlyList<SpaceStatusOccupancyReport> Removal { get; init; } = [];

    /// <summary>
    /// A written change.
    /// </summary>
    /// <param name="defaults">The defaults as the read now returns them.</param>
    /// <returns>The result.</returns>
    public static SpaceStatusWriteResult Written(IReadOnlyList<StatusDefinition> defaults) => new(SpaceStatusWriteOutcome.Written, defaults);

    /// <summary>
    /// A refusal; nothing was written.
    /// </summary>
    /// <param name="outcome">Why.</param>
    /// <returns>The result.</returns>
    public static SpaceStatusWriteResult Refused(SpaceStatusWriteOutcome outcome) => new(outcome, null);
}

/// <summary>
/// One Project in a rename's conflict report.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="ProjectName">Its name.</param>
/// <param name="Name">Its own name for the Status.</param>
/// <param name="TaskCount">How many of its Tasks hold the Status.</param>
public sealed record SpaceStatusConflictReport(Guid ProjectId, string ProjectName, string Name, int TaskCount);

/// <summary>
/// One Project a name clash names.
/// </summary>
/// <param name="Id">The Project.</param>
/// <param name="Name">Its name.</param>
public sealed record SpaceStatusProject(Guid Id, string Name);

/// <summary>
/// One Project in a removal's report.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="ProjectName">Its name.</param>
/// <param name="TaskCount">How many of its Tasks hold the Status being removed.</param>
/// <param name="Destinations">Its own set after the removal, in Board order, by its own names.</param>
public sealed record SpaceStatusOccupancyReport(Guid ProjectId, string ProjectName, int TaskCount, IReadOnlyList<StatusChoice> Destinations);

/// <summary>
/// One Status a Project's destination picker offers.
/// </summary>
/// <param name="Id">The Status.</param>
/// <param name="Name">The Project's own name for it.</param>
public sealed record StatusChoice(Guid Id, string Name);

/// <summary>
/// What a change of the Space's defaults came to.
/// </summary>
public enum SpaceStatusWriteOutcome
{
    /// <summary>
    /// The change was recorded, or there was nothing to change.
    /// </summary>
    Written,

    /// <summary>
    /// The Status in the path is not one of the Space's defaults.
    /// </summary>
    NotFound,

    /// <summary>
    /// The name is another default's, or would be held twice in some Project's set.
    /// </summary>
    NameTaken,

    /// <summary>
    /// Some Projects gave the default a name of their own, and the request carries no decision about them.
    /// </summary>
    RenameConflict,

    /// <summary>
    /// The default to place after is not another default of the Space.
    /// </summary>
    PositionInvalid,

    /// <summary>
    /// The change would leave the Space with no default, or some Project with no Status.
    /// </summary>
    SetEmpty,

    /// <summary>
    /// A removal moves Tasks and names no destination.
    /// </summary>
    DestinationRequired,

    /// <summary>
    /// A removal's Space-wide destination is not another default, or a Project's own destination is outside its set.
    /// </summary>
    DestinationInvalid,

    /// <summary>
    /// A Project that cannot take a removal's Space-wide destination was given none of its own.
    /// </summary>
    ProjectDestinationRequired,
}
