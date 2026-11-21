namespace Yello.Domain.Statuses;

/// <summary>
/// One Project's Status delta as read: the definitions it owns and its operations, in the delta's order.
/// </summary>
/// <param name="OwnedDefinitions">The Statuses the Project added, as <see cref="StatusDefinition"/> rows it owns.</param>
/// <param name="Operations">The operations, ascending by <see cref="StatusDeltaOperation.Sequence"/>.</param>
public sealed record ProjectStatusDelta(IReadOnlyList<StatusDefinition> OwnedDefinitions, IReadOnlyList<StatusDeltaOperation> Operations)
{
    /// <summary>
    /// The delta of a Project that has changed nothing: its set is its Space's defaults.
    /// </summary>
    public static ProjectStatusDelta Empty { get; } = new([], []);

    /// <summary>
    /// The sequence the next operation written under the Project's lock takes.
    /// </summary>
    public int NextSequence => Operations.Count == 0 ? 1 : Operations.Max(operation => operation.Sequence) + 1;

    /// <summary>
    /// This delta with <paramref name="added"/> owned and <paramref name="operations"/> applied as writes: each replaces
    /// the operation of the same Status and kind, and goes last.
    /// </summary>
    /// <param name="added">A definition the write adds, or <c>null</c>.</param>
    /// <param name="operations">The operations the write records, in order.</param>
    /// <returns>The delta as it would read after the write.</returns>
    public ProjectStatusDelta With(StatusDefinition? added, IReadOnlyList<StatusDeltaOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        var kept = Operations
            .Where(existing => !operations.Any(written => written.StatusId == existing.StatusId && written.Kind == existing.Kind))
            .Concat(operations)
            .OrderBy(operation => operation.Sequence)
            .ToList();

        return new ProjectStatusDelta(added is null ? OwnedDefinitions : [.. OwnedDefinitions, added], kept);
    }

    /// <summary>
    /// This delta with every operation of <paramref name="statusId"/> deleted, and its definition too when the Project owns
    /// it: what a removal deletes before it records anything.
    /// </summary>
    /// <param name="statusId">The Status being removed.</param>
    /// <returns>The delta without it.</returns>
    public ProjectStatusDelta Without(Guid statusId) => new(
        [.. OwnedDefinitions.Where(definition => definition.Id != statusId)],
        [.. Operations.Where(operation => operation.StatusId != statusId)]);

    /// <summary>
    /// This delta with the <see cref="StatusDeltaKind.Remove"/> of <paramref name="statusId"/> deleted: a removed Space
    /// default added back.
    /// </summary>
    /// <param name="statusId">The removed default.</param>
    /// <returns>The delta with the default restored.</returns>
    public ProjectStatusDelta WithoutRemovalOf(Guid statusId) => this with
    {
        Operations = [.. Operations.Where(operation => operation.StatusId != statusId || operation.Kind != StatusDeltaKind.Remove)],
    };

    /// <summary>
    /// Whether this Project's delta removes the Space default <paramref name="statusId"/>.
    /// </summary>
    /// <param name="statusId">The Status.</param>
    /// <returns><c>true</c> when a <see cref="StatusDeltaKind.Remove"/> names it.</returns>
    public bool Removes(Guid statusId) => Operations.Any(operation => operation.StatusId == statusId && operation.Kind == StatusDeltaKind.Remove);
}
