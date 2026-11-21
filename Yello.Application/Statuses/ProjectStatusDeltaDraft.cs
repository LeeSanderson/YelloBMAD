using Yello.Domain;
using Yello.Domain.Statuses;

namespace Yello.Application.Statuses;

/// <summary>
/// One delta write being planned under the Project's lock: the set as it stood when the lock was granted, and the
/// definition and operations the write will record.
/// </summary>
public sealed class ProjectStatusDeltaDraft
{
    private readonly Guid _spaceId;

    private readonly Guid _projectId;

    private readonly int _ownedCount;

    private readonly IIdentifierGenerator _identifiers;

    private readonly DateTimeOffset _now;

    private readonly List<StatusDeltaOperation> _operations = [];

    private readonly HashSet<Guid> _placed;

    private int _nextSequence;

    internal ProjectStatusDeltaDraft(
        Guid spaceId,
        Guid projectId,
        IReadOnlyList<EffectiveStatus> before,
        ProjectStatusDelta delta,
        IIdentifierGenerator identifiers,
        DateTimeOffset now)
    {
        _spaceId = spaceId;
        _projectId = projectId;
        _ownedCount = delta.OwnedDefinitions.Count;
        _placed = delta.Operations.Where(operation => operation.Kind == StatusDeltaKind.Place).Select(operation => operation.StatusId).ToHashSet();
        _nextSequence = delta.NextSequence;
        _identifiers = identifiers;
        _now = now;
        Before = before;
    }

    /// <summary>
    /// The Project's effective set before the write, read under its lock.
    /// </summary>
    public IReadOnlyList<EffectiveStatus> Before { get; }

    /// <summary>
    /// The definition the write adds, if any.
    /// </summary>
    public StatusDefinition? Added { get; private set; }

    /// <summary>
    /// The operations the write records, in order.
    /// </summary>
    public IReadOnlyList<StatusDeltaOperation> Operations => _operations;

    /// <summary>
    /// The Status whose name the write sets, if any.
    /// </summary>
    public Guid? NamedStatusId { get; private set; }

    /// <summary>
    /// Whether the set before the write holds <paramref name="statusId"/>.
    /// </summary>
    /// <param name="statusId">The Status.</param>
    /// <returns><c>true</c> when it does.</returns>
    public bool Holds(Guid statusId) => EffectiveStatusSet.Contains(Before, statusId);

    /// <summary>
    /// Plans a Status this Project owns, appended to its set.
    /// </summary>
    /// <param name="name">The name, already normalised.</param>
    /// <returns>The new Status's id.</returns>
    public Guid Add(string name)
    {
        var id = _identifiers.Next();

        Added = new StatusDefinition
        {
            Id = id,
            SpaceId = _spaceId,
            OwnerProjectId = _projectId,
            Name = name,
            Position = _ownedCount,
            CreatedAt = _now,
        };

        Record(id, StatusDeltaKind.Add, name: null, afterStatusId: null);
        NamedStatusId = id;

        return id;
    }

    /// <summary>
    /// Plans a name for a Status in this Project.
    /// </summary>
    /// <param name="statusId">The Status.</param>
    /// <param name="name">The name, already normalised.</param>
    public void Rename(Guid statusId, string name)
    {
        Record(statusId, StatusDeltaKind.Rename, name, afterStatusId: null);
        NamedStatusId = statusId;
    }

    /// <summary>
    /// Plans a Status's place after another, or first, re-anchoring every Status the Project already placed (see
    /// <see cref="EffectiveStatusSet.Placements"/>).
    /// </summary>
    /// <param name="statusId">The Status.</param>
    /// <param name="afterStatusId">The Status it goes after, or <c>null</c> for first.</param>
    public void Place(Guid statusId, Guid? afterStatusId)
    {
        var current = Before;

        if (Added is { } added && !Holds(added.Id))
        {
            current = [.. Before, new EffectiveStatus(added.Id, added.Name, StatusSource.AddedHere, DefaultName: null)];
        }

        foreach (var (placedId, anchor) in EffectiveStatusSet.Placements(current, _placed, statusId, afterStatusId))
        {
            Record(placedId, StatusDeltaKind.Place, name: null, anchor);
        }
    }

    private void Record(Guid statusId, StatusDeltaKind kind, string? name, Guid? afterStatusId) =>
        _operations.Add(new StatusDeltaOperation
        {
            Id = _identifiers.Next(),
            SpaceId = _spaceId,
            ProjectId = _projectId,
            StatusId = statusId,
            Kind = kind,
            Name = name,
            AfterStatusId = afterStatusId,
            Sequence = _nextSequence++,
            CreatedAt = _now,
        });
}
