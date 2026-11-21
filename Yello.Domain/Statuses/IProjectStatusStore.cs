namespace Yello.Domain.Statuses;

/// <summary>
/// Writes one Project's Status delta inside the request's Space-scoped unit of work.
/// </summary>
/// <remarks>
/// <b>Every write runs under the Project's lock</b>, the one each placer of the Project's Tasks takes: the effective set's
/// name uniqueness cannot be an index (a renamed default's name lives in the delta), so two writes are serialised and the
/// second reads the first's result before it is checked. A removal's Task migration, its operation deletes and inserts,
/// and an owned definition's delete all share the one unit of work, so they commit together or not at all.
/// </remarks>
public interface IProjectStatusStore
{
    /// <summary>
    /// Takes the Project's lock, if the Project is one of the active Space's.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>true</c> when the Project exists and its lock is held until the unit of work ends.</returns>
    Task<bool> LockAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Records a write: <paramref name="added"/>, when there is one, then each operation, replacing the Project's operation
    /// of the same Status and kind.
    /// </summary>
    /// <param name="added">A definition the Project adds, or <c>null</c>.</param>
    /// <param name="operations">The operations.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>false</c> when a name index refused the write; nothing is left pending.</returns>
    Task<bool> WriteAsync(StatusDefinition? added, IReadOnlyList<StatusDeltaOperation> operations, CancellationToken cancellationToken);

    /// <summary>
    /// Moves every Task of the Project that holds <paramref name="fromStatusId"/> to the end of
    /// <paramref name="toStatusId"/>'s column, in the order they sat in, in one statement.
    /// </summary>
    /// <param name="projectId">The Project, whose lock is held.</param>
    /// <param name="fromStatusId">The Status being removed.</param>
    /// <param name="toStatusId">The destination, a Status of the set the removal leaves.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    Task MigrateTasksAsync(Guid projectId, Guid fromStatusId, Guid toStatusId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the Project's operations of <paramref name="statusId"/>: of one kind, or of every kind.
    /// </summary>
    /// <param name="projectId">The Project, whose lock is held.</param>
    /// <param name="statusId">The Status.</param>
    /// <param name="kind">The kind, or <c>null</c> for every kind.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    Task DeleteOperationsAsync(Guid projectId, Guid statusId, StatusDeltaKind? kind, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a definition the Project owns. Its Tasks and every operation naming it must already be gone.
    /// </summary>
    /// <param name="projectId">The Project, whose lock is held.</param>
    /// <param name="statusId">The Status it added.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    Task DeleteOwnedDefinitionAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken);
}
