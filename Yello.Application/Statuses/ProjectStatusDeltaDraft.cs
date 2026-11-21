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

    private readonly ProjectStatusDelta _delta;

    private readonly Func<Guid, CancellationToken, Task<int>> _occupants;

    private int _nextSequence;

    internal ProjectStatusDeltaDraft(
        Guid spaceId,
        Guid projectId,
        IReadOnlyList<EffectiveStatus> before,
        ProjectStatusDelta delta,
        IIdentifierGenerator identifiers,
        DateTimeOffset now,
        Func<Guid, CancellationToken, Task<int>> occupants)
    {
        _spaceId = spaceId;
        _projectId = projectId;
        _delta = delta;
        _occupants = occupants;
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
    /// The Status the write takes out of the set, if any.
    /// </summary>
    public Guid? RemovedStatusId { get; private set; }

    /// <summary>
    /// Whether the removed Status is one the Project added, so its definition is deleted with it.
    /// </summary>
    public bool RemovesOwnedDefinition { get; private set; }

    /// <summary>
    /// Where the removed Status's Tasks go, if anywhere.
    /// </summary>
    public Guid? DestinationStatusId { get; private set; }

    /// <summary>
    /// The removed Space default the write adds back, if any.
    /// </summary>
    public Guid? RestoredStatusId { get; private set; }

    /// <summary>
    /// Whether the set before the write holds <paramref name="statusId"/>.
    /// </summary>
    /// <param name="statusId">The Status.</param>
    /// <returns><c>true</c> when it does.</returns>
    public bool Holds(Guid statusId) => EffectiveStatusSet.Contains(Before, statusId);

    /// <summary>
    /// Whether this Project's delta removes the Space default <paramref name="statusId"/>.
    /// </summary>
    /// <param name="statusId">The Status.</param>
    /// <returns><c>true</c> when it does.</returns>
    public bool HasRemoved(Guid statusId) => _delta.Removes(statusId);

    /// <summary>
    /// How many of the Project's Tasks hold <paramref name="statusId"/>, counted under the Project's lock so a Task that
    /// arrived after the caller read its counts is seen.
    /// </summary>
    /// <param name="statusId">The Status.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The count.</returns>
    public Task<int> OccupantsAsync(Guid statusId, CancellationToken cancellationToken) => _occupants(statusId, cancellationToken);

    /// <summary>
    /// Plans a Status's removal: every other place re-anchored so the rest keep their order (see
    /// <see cref="EffectiveStatusSet.Reanchored"/>), then a <see cref="StatusDeltaKind.Remove"/> for a Space default, or the
    /// deletion of the definition for a Status the Project added.
    /// </summary>
    /// <param name="statusId">A Status the set holds.</param>
    /// <param name="destinationStatusId">Where its Tasks go, or <c>null</c> when it has none.</param>
    public void Remove(Guid statusId, Guid? destinationStatusId)
    {
        RemovedStatusId = statusId;
        DestinationStatusId = destinationStatusId;
        RemovesOwnedDefinition = Before.Single(status => status.Id == statusId).Source == StatusSource.AddedHere;
        _placed.Remove(statusId);

        foreach (var (placedId, anchor) in EffectiveStatusSet.Reanchored(Before, _placed, statusId))
        {
            Record(placedId, StatusDeltaKind.Place, name: null, anchor);
        }

        if (!RemovesOwnedDefinition)
        {
            Record(statusId, StatusDeltaKind.Remove, name: null, afterStatusId: null);
        }
    }

    /// <summary>
    /// Plans a removed Space default's return: its <see cref="StatusDeltaKind.Remove"/> is deleted, and nothing else, so it
    /// is back at its Space slot under its Space name.
    /// </summary>
    /// <param name="statusId">A default this Project's delta removes.</param>
    public void Restore(Guid statusId)
    {
        RestoredStatusId = statusId;
        NamedStatusId = statusId;
    }

    /// <summary>
    /// The delta as it would read after the write.
    /// </summary>
    /// <returns>The delta.</returns>
    internal ProjectStatusDelta After()
    {
        var delta = _delta;

        if (RemovedStatusId is { } removed)
        {
            delta = delta.Without(removed);
        }

        if (RestoredStatusId is { } restored)
        {
            delta = delta.WithoutRemovalOf(restored);
        }

        return delta.With(Added, _operations);
    }

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
