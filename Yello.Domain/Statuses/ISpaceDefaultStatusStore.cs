namespace Yello.Domain.Statuses;

/// <summary>
/// Changes the active Space's default Statuses inside the request's Space-scoped unit of work, and reads what such a change
/// must be judged against.
/// </summary>
/// <remarks>
/// <b>Every change runs under the Space-level Status lock, held exclusively</b>, which every Project's own delta write holds
/// shared before its Project's lock. A Space change alters a name in every Project's set at once, while a Project checks a
/// name only against its own set: without one lock between them both pass their checks and commit a set holding one name
/// twice. Every Project's delta is read after the lock is granted, so the check sees every Project write committed before.
/// </remarks>
public interface ISpaceDefaultStatusStore
{
    /// <summary>
    /// Takes the active Space's Status-defaults lock exclusively, until the unit of work ends.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    Task LockAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Every Project's delta in the active Space, keyed by Project. A Project that has changed nothing is absent.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The deltas.</returns>
    Task<IReadOnlyDictionary<Guid, ProjectStatusDelta>> DeltasAsync(CancellationToken cancellationToken);

    /// <summary>
    /// How many Tasks hold <paramref name="statusId"/> in each Project of the active Space; a Project with none is absent.
    /// </summary>
    /// <param name="statusId">The Status.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The counts, keyed by Project.</returns>
    Task<IReadOnlyDictionary<Guid, int>> CountTasksByProjectAsync(Guid statusId, CancellationToken cancellationToken);

    /// <summary>
    /// The names of the active Space's Projects among <paramref name="projectIds"/>.
    /// </summary>
    /// <param name="projectIds">The Projects.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The names, keyed by Project.</returns>
    Task<IReadOnlyDictionary<Guid, string>> ProjectNamesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>
    /// Records an admitted change: the named <c>Rename</c> operations deleted, then the default's new name, the new positions
    /// and the new default, all in the one unit of work.
    /// </summary>
    /// <param name="change">The change, as <see cref="SpaceDefaultStatusPlan"/> admitted it.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>false</c> when the default-name index refused the write; nothing is left pending.</returns>
    Task<bool> WriteAsync(SpaceDefaultStatusChange change, CancellationToken cancellationToken);
}
