namespace Yello.Domain.Statuses;

/// <summary>
/// A Project's effective Status set, derived at the point of use and never stored. AD-16 / AR-23.
/// </summary>
/// <remarks>
/// <para>
/// <b>The one place the derivation lives.</b> The Space's defaults in Space order, then the Project's delta applied in the
/// delta's order. Every reader of a Project's Statuses - the Board, its column page, the List, Task creation, edit and
/// move, and the Project's own Status read - goes through <see cref="Of(IEnumerable{StatusDefinition}, ProjectStatusDelta)"/>.
/// </para>
/// <para>
/// <b>Ordered by <c>Position</c>, stably.</b> Two definitions sharing a position keep the order they were read in, which
/// <c>IStatusDefinitionReader</c> fixes as <c>Position</c> then <c>Id</c> in the database's own <c>uniqueidentifier</c>
/// order - .NET orders a <see cref="Guid"/> differently, so re-sorting by <c>Id</c> here would make the tie disagree with
/// every SQL reader.
/// </para>
/// <para>
/// <b>A place whose anchor is not in the set is ignored</b>, and the Status keeps its slot: deterministic, and it needs
/// nothing from whoever removed the anchor.
/// </para>
/// </remarks>
public static class EffectiveStatusSet
{
    /// <summary>
    /// The effective set of a Project that has changed nothing: its Space's defaults.
    /// </summary>
    /// <param name="spaceDefaults">The Space's default Status definitions, in the reader's order.</param>
    /// <returns>The effective set, in Board order.</returns>
    public static IReadOnlyList<EffectiveStatus> Of(IEnumerable<StatusDefinition> spaceDefaults) =>
        Of(spaceDefaults, ProjectStatusDelta.Empty);

    /// <summary>
    /// The effective set of a Project whose Space defines <paramref name="spaceDefaults"/> and whose delta is
    /// <paramref name="delta"/>.
    /// </summary>
    /// <param name="spaceDefaults">The Space's default Status definitions, in the reader's order.</param>
    /// <param name="delta">The Project's delta.</param>
    /// <returns>The effective set, in Board order.</returns>
    public static IReadOnlyList<EffectiveStatus> Of(IEnumerable<StatusDefinition> spaceDefaults, ProjectStatusDelta delta)
    {
        ArgumentNullException.ThrowIfNull(spaceDefaults);
        ArgumentNullException.ThrowIfNull(delta);

        var set = spaceDefaults
            .OrderBy(status => status.Position)
            .Select(status => new EffectiveStatus(status.Id, status.Name, StatusSource.SpaceDefault, DefaultName: null))
            .ToList();

        foreach (var operation in delta.Operations.OrderBy(operation => operation.Sequence))
        {
            Apply(set, delta.OwnedDefinitions, operation);
        }

        return set;
    }

    /// <summary>
    /// Whether <paramref name="statusId"/> is one of an effective set - the one membership test every slice
    /// that sets a Status makes (AD-17: no Task holds a Status outside its Project's effective set).
    /// </summary>
    /// <param name="effective">An effective set, as <see cref="Of(IEnumerable{StatusDefinition}, ProjectStatusDelta)"/> derived it.</param>
    /// <param name="statusId">The Status a request asks for.</param>
    /// <returns><c>true</c> when the set holds it.</returns>
    public static bool Contains(IReadOnlyList<EffectiveStatus> effective, Guid statusId)
    {
        ArgumentNullException.ThrowIfNull(effective);

        return effective.Any(status => status.Id == statusId);
    }

    /// <summary>
    /// Whether the effective set a delta write would leave is one a Project may hold: never empty, and the name the write
    /// sets is no other Status's, compared case-insensitively.
    /// </summary>
    /// <param name="after">The effective set as it would be after the write.</param>
    /// <param name="namedStatusId">The Status whose name the write sets, or <c>null</c> when it sets none.</param>
    /// <returns>The verdict.</returns>
    public static StatusDeltaVerdict Check(IReadOnlyList<EffectiveStatus> after, Guid? namedStatusId)
    {
        ArgumentNullException.ThrowIfNull(after);

        if (after.Count == 0)
        {
            return StatusDeltaVerdict.SetEmpty;
        }

        if (namedStatusId is not { } id || after.FirstOrDefault(status => status.Id == id) is not { } named)
        {
            return StatusDeltaVerdict.Admitted;
        }

        return after.Any(status => status.Id != id && StatusName.Comparer.Equals(status.Name, named.Name))
            ? StatusDeltaVerdict.NameTaken
            : StatusDeltaVerdict.Admitted;
    }

    private static void Apply(List<EffectiveStatus> set, IReadOnlyList<StatusDefinition> owned, StatusDeltaOperation operation)
    {
        var index = set.FindIndex(status => status.Id == operation.StatusId);

        if (operation.Kind == StatusDeltaKind.Add)
        {
            if (index < 0 && owned.FirstOrDefault(status => status.Id == operation.StatusId) is { } definition)
            {
                set.Add(new EffectiveStatus(definition.Id, definition.Name, StatusSource.AddedHere, DefaultName: null));
            }

            return;
        }

        // A rename or a place of a Status the set does not hold changes nothing.
        if (index < 0)
        {
            return;
        }

        if (operation.Kind == StatusDeltaKind.Rename)
        {
            set[index] = operation.Name is { } name ? Renamed(set[index], name) : set[index];

            return;
        }

        Place(set, index, operation.AfterStatusId);
    }

    private static EffectiveStatus Renamed(EffectiveStatus status, string name)
    {
        if (status.Source != StatusSource.SpaceDefault)
        {
            return status with { Name = name };
        }

        var defaultName = status.DefaultName ?? status.Name;

        return status with { Name = name, DefaultName = string.Equals(name, defaultName, StringComparison.Ordinal) ? null : defaultName };
    }

    private static void Place(List<EffectiveStatus> set, int index, Guid? afterStatusId)
    {
        var status = set[index];

        if (afterStatusId is not { } anchor)
        {
            set.RemoveAt(index);
            set.Insert(0, status);

            return;
        }

        if (anchor == status.Id || !set.Exists(other => other.Id == anchor))
        {
            return;
        }

        set.RemoveAt(index);
        set.Insert(set.FindIndex(other => other.Id == anchor) + 1, status);
    }
}

/// <summary>
/// Whether a Status delta write may be made.
/// </summary>
public enum StatusDeltaVerdict
{
    /// <summary>
    /// The write leaves a set a Project may hold.
    /// </summary>
    Admitted,

    /// <summary>
    /// The name the write sets is another Status's in the set.
    /// </summary>
    NameTaken,

    /// <summary>
    /// The write would leave the Project with no Status.
    /// </summary>
    SetEmpty,
}
