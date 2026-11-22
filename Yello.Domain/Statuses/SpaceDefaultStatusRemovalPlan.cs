namespace Yello.Domain.Statuses;

/// <summary>
/// A planned Space-level removal: whether it may be made, what a refusal reports, and what the commit writes.
/// </summary>
/// <param name="Verdict">Whether it may be made.</param>
public sealed record SpaceDefaultStatusRemovalPlan(SpaceDefaultStatusVerdict Verdict)
{
    /// <summary>
    /// The default being removed.
    /// </summary>
    public Guid StatusId { get; init; }

    /// <summary>
    /// On <see cref="SpaceDefaultStatusVerdict.DestinationRequired"/>: every Project holding the Status with Tasks in it. On
    /// <see cref="SpaceDefaultStatusVerdict.ProjectDestinationRequired"/>: every such Project that cannot take the Space-wide
    /// destination and was given none of its own.
    /// </summary>
    public IReadOnlyList<SpaceDefaultStatusOccupancy> Report { get; init; } = [];

    /// <summary>
    /// On <see cref="SpaceDefaultStatusVerdict.SetEmpty"/>: the Projects the removal would leave with no Status, or none when
    /// it is the Space's last default. On <see cref="SpaceDefaultStatusVerdict.DestinationInvalid"/>: the Projects whose own
    /// destination is outside their set after the removal, or none when the Space-wide destination is the invalid one.
    /// </summary>
    public IReadOnlyList<Guid> Projects { get; init; } = [];

    /// <summary>
    /// Where each occupied Project's Tasks go.
    /// </summary>
    public IReadOnlyList<SpaceStatusMigration> Migrations { get; init; } = [];

    /// <summary>
    /// The places that replace every place of a Project whose places the removed Status anchored.
    /// </summary>
    public IReadOnlyList<SpaceStatusReanchoring> Places { get; init; } = [];

    /// <summary>
    /// A refusal, which writes nothing.
    /// </summary>
    /// <param name="verdict">Why.</param>
    /// <returns>The plan.</returns>
    public static SpaceDefaultStatusRemovalPlan Refused(SpaceDefaultStatusVerdict verdict) => new(verdict);
}

/// <summary>
/// What a Space-level removal is judged against, every part read under the removal's locks.
/// </summary>
/// <param name="Defaults">The Space's defaults, in Space order.</param>
/// <param name="Deltas">Every Project's delta, keyed by Project; a Project that changed nothing is absent.</param>
/// <param name="ProjectIds">Every Project of the Space.</param>
/// <param name="Occupancy">How many Tasks hold the Status in each Project; a Project with none is absent.</param>
public sealed record SpaceStatusRemovalState(
    IReadOnlyList<StatusDefinition> Defaults,
    IReadOnlyDictionary<Guid, ProjectStatusDelta> Deltas,
    IReadOnlyCollection<Guid> ProjectIds,
    IReadOnlyDictionary<Guid, int> Occupancy);

/// <summary>
/// What the Admin asked for.
/// </summary>
/// <param name="StatusId">The default to remove.</param>
/// <param name="DestinationStatusId">The Space-wide destination, or <c>null</c> when none was chosen.</param>
/// <param name="Exceptions">A destination of its own for each Project that cannot take the Space-wide one.</param>
public sealed record SpaceStatusRemovalRequest(Guid StatusId, Guid? DestinationStatusId, IReadOnlyList<SpaceStatusDestination> Exceptions);

/// <summary>
/// One Project's own destination.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="DestinationStatusId">A Status of its set after the removal.</param>
public sealed record SpaceStatusDestination(Guid ProjectId, Guid DestinationStatusId);

/// <summary>
/// One Project holding the Status with Tasks in it.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="TaskCount">How many of its Tasks hold the Status.</param>
/// <param name="After">Its set after the removal, in Board order: what its destination picker offers.</param>
public sealed record SpaceDefaultStatusOccupancy(Guid ProjectId, int TaskCount, IReadOnlyList<EffectiveStatus> After);

/// <summary>
/// Where one Project's Tasks go.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="DestinationStatusId">The Status they go to.</param>
public sealed record SpaceStatusMigration(Guid ProjectId, Guid DestinationStatusId);

/// <summary>
/// The places that replace every place of one Project.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="Places">The new places, sequenced after the Project's last operation.</param>
public sealed record SpaceStatusReanchoring(Guid ProjectId, IReadOnlyList<StatusDeltaOperation> Places);
