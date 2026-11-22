namespace Yello.Domain.Statuses;

/// <summary>
/// Plans one change of a Space's default Statuses - an add, a rename or a reorder - against every Project's delta, and
/// judges it by the set each Project would then hold.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing is planned per Project but the deletion of <c>Rename</c> operations.</b> A Project's set is derived from the
/// defaults, so a new default, a new name and a new order reach every Project through
/// <see cref="EffectiveStatusSet.Of(IEnumerable{StatusDefinition}, ProjectStatusDelta)"/>. A row written per Project would be
/// a copy of its set, wrong for every Project created afterwards.
/// </para>
/// <para>
/// <b>Every Project's after-set is judged by <see cref="EffectiveStatusSet.Check"/></b>, the check a Project's own write
/// makes, so the two levels can never disagree about whether a set holds a name twice.
/// </para>
/// <para>
/// <b>A Project's own name for a renamed default is a conflict only when it differs, exactly, from both the old and the
/// new Space name.</b> Equal to the old name, the Project's editor already shows the Status as not renamed; equal to the new
/// one, it already shows the name the Space is taking. Either way its operation is deleted so it follows the Space.
/// </para>
/// </remarks>
public static class SpaceDefaultStatusPlan
{
    /// <summary>
    /// Plans a new default, last in Space order or right after another default.
    /// </summary>
    /// <param name="defaults">The Space's defaults, read under the Space-level Status lock, in Space order.</param>
    /// <param name="deltas">Every Project's delta in the Space, keyed by Project.</param>
    /// <param name="added">The new default, with no owner; its position is planned here.</param>
    /// <param name="afterStatusId">The default it goes after, or <c>null</c> for last.</param>
    /// <returns>The change, or why it cannot be made.</returns>
    public static SpaceDefaultStatusChange Add(
        IReadOnlyList<StatusDefinition> defaults,
        IReadOnlyDictionary<Guid, ProjectStatusDelta> deltas,
        StatusDefinition added,
        Guid? afterStatusId)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(deltas);
        ArgumentNullException.ThrowIfNull(added);

        if (afterStatusId is { } anchor && !IsDefault(defaults, anchor))
        {
            return SpaceDefaultStatusChange.Refused(SpaceDefaultStatusVerdict.PositionInvalid);
        }

        if (defaults.Any(status => StatusName.Comparer.Equals(status.Name, added.Name)))
        {
            return SpaceDefaultStatusChange.Refused(SpaceDefaultStatusVerdict.NameTaken);
        }

        var order = Ordered(defaults).ToList();
        order = EffectiveStatusSet.Moved(order, added.Id, order.Count == 0 ? afterStatusId : afterStatusId ?? order[^1]);
        var after = Laid([.. defaults, added], order);
        var change = new SpaceDefaultStatusChange(SpaceDefaultStatusVerdict.Admitted)
        {
            Added = after.Single(status => status.Id == added.Id),
            Positions = Repositioned(defaults, after),
        };

        return Judged(change, after, deltas, deleted: [], named: added.Id);
    }

    /// <summary>
    /// Plans a default's new name, and what becomes of every Project's own name for it.
    /// </summary>
    /// <param name="defaults">The Space's defaults, read under the Space-level Status lock, in Space order.</param>
    /// <param name="deltas">Every Project's delta in the Space, keyed by Project.</param>
    /// <param name="statusId">The default being renamed.</param>
    /// <param name="name">The new name, already normalised.</param>
    /// <param name="replaceProjectNames">
    /// The one decision for every Project that conflicts: <c>true</c> deletes their names, <c>false</c> keeps them, and
    /// <c>null</c> - no decision yet - refuses the rename with the conflicts when there are any.
    /// </param>
    /// <returns>The change, or why it cannot be made.</returns>
    public static SpaceDefaultStatusChange Rename(
        IReadOnlyList<StatusDefinition> defaults,
        IReadOnlyDictionary<Guid, ProjectStatusDelta> deltas,
        Guid statusId,
        string name,
        bool? replaceProjectNames)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(deltas);

        if (defaults.FirstOrDefault(status => status.Id == statusId) is not { } renamed)
        {
            return SpaceDefaultStatusChange.Refused(SpaceDefaultStatusVerdict.NotFound);
        }

        if (string.Equals(renamed.Name, name, StringComparison.Ordinal))
        {
            return new SpaceDefaultStatusChange(SpaceDefaultStatusVerdict.Unchanged);
        }

        if (defaults.Any(status => status.Id != statusId && StatusName.Comparer.Equals(status.Name, name)))
        {
            return SpaceDefaultStatusChange.Refused(SpaceDefaultStatusVerdict.NameTaken);
        }

        var (deleted, conflicts) = OwnNames(deltas, statusId, renamed.Name, name, replaceProjectNames);

        if (conflicts.Count > 0 && replaceProjectNames is null)
        {
            return SpaceDefaultStatusChange.Refused(SpaceDefaultStatusVerdict.RenameConflict) with { RenamedStatusId = statusId, Conflicts = conflicts };
        }

        var after = defaults.Select(status => status.Id == statusId ? Copy(status, name, status.Position) : status).ToList();
        var change = new SpaceDefaultStatusChange(SpaceDefaultStatusVerdict.Admitted)
        {
            RenamedStatusId = statusId,
            Name = name,
            RenameOperationsDeletedIn = deleted,
            Conflicts = conflicts,
        };

        return Judged(change, after, deltas, deleted, named: statusId);
    }

    /// <summary>
    /// Plans a default's new place in Space order, first or right after another default.
    /// </summary>
    /// <param name="defaults">The Space's defaults, read under the Space-level Status lock, in Space order.</param>
    /// <param name="statusId">The default being moved.</param>
    /// <param name="afterStatusId">Another default it goes after, or <c>null</c> for first.</param>
    /// <returns>The change, or why it cannot be made.</returns>
    public static SpaceDefaultStatusChange Place(IReadOnlyList<StatusDefinition> defaults, Guid statusId, Guid? afterStatusId)
    {
        ArgumentNullException.ThrowIfNull(defaults);

        if (!IsDefault(defaults, statusId))
        {
            return SpaceDefaultStatusChange.Refused(SpaceDefaultStatusVerdict.NotFound);
        }

        if (afterStatusId is { } anchor && (anchor == statusId || !IsDefault(defaults, anchor)))
        {
            return SpaceDefaultStatusChange.Refused(SpaceDefaultStatusVerdict.PositionInvalid);
        }

        var after = Laid(defaults, EffectiveStatusSet.Moved(Ordered(defaults), statusId, afterStatusId));
        var change = new SpaceDefaultStatusChange(SpaceDefaultStatusVerdict.Admitted) { Positions = Repositioned(defaults, after) };

        return Judged(change, after, new Dictionary<Guid, ProjectStatusDelta>(), deleted: [], named: null);
    }

    /// <summary>
    /// Whether a default set may be left as it is: a Space always keeps at least one default, so a new Project always has
    /// a Status to start from.
    /// </summary>
    /// <param name="defaultsAfter">The defaults as a change would leave them.</param>
    /// <returns><see cref="SpaceDefaultStatusVerdict.SetEmpty"/> for an empty set, otherwise <see cref="SpaceDefaultStatusVerdict.Admitted"/>.</returns>
    public static SpaceDefaultStatusVerdict CheckDefaults(IReadOnlyCollection<StatusDefinition> defaultsAfter)
    {
        ArgumentNullException.ThrowIfNull(defaultsAfter);

        return defaultsAfter.Count == 0 ? SpaceDefaultStatusVerdict.SetEmpty : SpaceDefaultStatusVerdict.Admitted;
    }

    private static (IReadOnlyList<Guid> Deleted, IReadOnlyList<SpaceDefaultStatusConflict> Conflicts) OwnNames(
        IReadOnlyDictionary<Guid, ProjectStatusDelta> deltas,
        Guid statusId,
        string oldName,
        string newName,
        bool? replaceProjectNames)
    {
        var deleted = new List<Guid>();
        var conflicts = new List<SpaceDefaultStatusConflict>();

        foreach (var (projectId, delta) in deltas)
        {
            if (delta.Removes(statusId)
                || delta.Operations.FirstOrDefault(operation => operation.StatusId == statusId && operation.Kind == StatusDeltaKind.Rename) is not { Name: { } own })
            {
                continue;
            }

            if (string.Equals(own, oldName, StringComparison.Ordinal) || string.Equals(own, newName, StringComparison.Ordinal))
            {
                deleted.Add(projectId);

                continue;
            }

            conflicts.Add(new SpaceDefaultStatusConflict(projectId, own));

            if (replaceProjectNames == true)
            {
                deleted.Add(projectId);
            }
        }

        return (deleted, conflicts);
    }

    private static SpaceDefaultStatusChange Judged(
        SpaceDefaultStatusChange change,
        IReadOnlyList<StatusDefinition> after,
        IReadOnlyDictionary<Guid, ProjectStatusDelta> deltas,
        IReadOnlyList<Guid> deleted,
        Guid? named)
    {
        if (CheckDefaults(after) == SpaceDefaultStatusVerdict.SetEmpty)
        {
            return SpaceDefaultStatusChange.Refused(SpaceDefaultStatusVerdict.SetEmpty);
        }

        if (named is not { } statusId)
        {
            return change;
        }

        var clashing = deltas
            .Where(pair => EffectiveStatusSet.Check(EffectiveStatusSet.Of(after, AfterDeletes(pair.Value, statusId, deleted.Contains(pair.Key))), statusId) == StatusDeltaVerdict.NameTaken)
            .Select(pair => pair.Key)
            .ToList();

        return clashing.Count == 0
            ? change
            : SpaceDefaultStatusChange.Refused(SpaceDefaultStatusVerdict.ProjectNameTaken) with { ClashingProjects = clashing };
    }

    private static ProjectStatusDelta AfterDeletes(ProjectStatusDelta delta, Guid statusId, bool deletesRename) =>
        deletesRename
            ? delta with { Operations = [.. delta.Operations.Where(operation => operation.StatusId != statusId || operation.Kind != StatusDeltaKind.Rename)] }
            : delta;

    private static bool IsDefault(IReadOnlyList<StatusDefinition> defaults, Guid statusId) =>
        defaults.Any(status => status.Id == statusId);

    private static IEnumerable<Guid> Ordered(IReadOnlyList<StatusDefinition> defaults) =>
        defaults.OrderBy(status => status.Position).Select(status => status.Id);

    /// <summary>
    /// The definitions laid out in <paramref name="order"/>, positions 0 to n-1, so no two share a position afterwards.
    /// </summary>
    private static List<StatusDefinition> Laid(IReadOnlyList<StatusDefinition> definitions, IReadOnlyList<Guid> order) =>
        [.. order.Select((id, position) => Copy(definitions.Single(status => status.Id == id), position))];

    private static Dictionary<Guid, int> Repositioned(IReadOnlyList<StatusDefinition> before, IReadOnlyList<StatusDefinition> after) =>
        after
            .Where(status => before.FirstOrDefault(old => old.Id == status.Id) is { } old && old.Position != status.Position)
            .ToDictionary(status => status.Id, status => status.Position);

    private static StatusDefinition Copy(StatusDefinition status, int position) => Copy(status, status.Name, position);

    private static StatusDefinition Copy(StatusDefinition status, string name, int position) => new()
    {
        Id = status.Id,
        SpaceId = status.SpaceId,
        OwnerProjectId = status.OwnerProjectId,
        Name = name,
        Position = position,
        CreatedAt = status.CreatedAt,
    };
}

/// <summary>
/// A planned change of a Space's defaults: whether it may be made, and what it writes.
/// </summary>
/// <param name="Verdict">Whether it may be made.</param>
public sealed record SpaceDefaultStatusChange(SpaceDefaultStatusVerdict Verdict)
{
    /// <summary>
    /// The new default to insert, at its planned position, if the change adds one.
    /// </summary>
    public StatusDefinition? Added { get; init; }

    /// <summary>
    /// The default whose name the change sets, if any.
    /// </summary>
    public Guid? RenamedStatusId { get; init; }

    /// <summary>
    /// The name the change sets.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The existing defaults whose position changes, and their new positions.
    /// </summary>
    public IReadOnlyDictionary<Guid, int> Positions { get; init; } = new Dictionary<Guid, int>();

    /// <summary>
    /// The Projects whose <c>Rename</c> operation of the renamed default is deleted.
    /// </summary>
    public IReadOnlyList<Guid> RenameOperationsDeletedIn { get; init; } = [];

    /// <summary>
    /// Every Project that gave the renamed default a name of its own, as the rename found them.
    /// </summary>
    public IReadOnlyList<SpaceDefaultStatusConflict> Conflicts { get; init; } = [];

    /// <summary>
    /// On <see cref="SpaceDefaultStatusVerdict.ProjectNameTaken"/>: the Projects whose set would hold the name twice.
    /// </summary>
    public IReadOnlyList<Guid> ClashingProjects { get; init; } = [];

    /// <summary>
    /// A refusal, which writes nothing.
    /// </summary>
    /// <param name="verdict">Why.</param>
    /// <returns>The change.</returns>
    public static SpaceDefaultStatusChange Refused(SpaceDefaultStatusVerdict verdict) => new(verdict);
}

/// <summary>
/// One Project that gave a renamed default a name of its own.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="Name">Its own name for the Status.</param>
public sealed record SpaceDefaultStatusConflict(Guid ProjectId, string Name);

/// <summary>
/// Whether a change of a Space's defaults may be made.
/// </summary>
public enum SpaceDefaultStatusVerdict
{
    /// <summary>
    /// The change may be written.
    /// </summary>
    Admitted,

    /// <summary>
    /// The change changes nothing: a rename to the name the default already has.
    /// </summary>
    Unchanged,

    /// <summary>
    /// The Status in the path is not one of the Space's defaults.
    /// </summary>
    NotFound,

    /// <summary>
    /// The name is another default's, compared case-insensitively.
    /// </summary>
    NameTaken,

    /// <summary>
    /// The name would be held twice in some Project's set; see <see cref="SpaceDefaultStatusChange.ClashingProjects"/>.
    /// </summary>
    ProjectNameTaken,

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
