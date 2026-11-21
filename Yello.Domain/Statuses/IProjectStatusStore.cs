namespace Yello.Domain.Statuses;

/// <summary>
/// Writes one Project's Status delta inside the request's Space-scoped unit of work.
/// </summary>
/// <remarks>
/// <b>Every write runs under the Project's lock</b>, the one each placer of the Project's Tasks takes: the effective set's
/// name uniqueness cannot be an index (a renamed default's name lives in the delta), so two writes are serialised and the
/// second reads the first's result before it is checked.
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
}
