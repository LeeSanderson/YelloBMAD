namespace Yello.Domain.Statuses;

/// <summary>
/// The locks, reads and writes a Space-level removal needs beyond the other Space-level changes'.
/// </summary>
/// <remarks>
/// <para>
/// <b>Locks, in this order only:</b> the Status-defaults lock Exclusive (<see cref="ISpaceDefaultStatusStore.LockAsync"/>),
/// then the Space's Project list (<see cref="LockProjectsAsync"/>), then the Tasks lock of every Project whose set holds the
/// Status (<see cref="LockProjectTasksAsync"/>), in one sorted order. A Task writer takes only its own Project's lock, so
/// without the last a Task could enter the Status after the occupancy count; without the second a Project created after the
/// list was read would escape the plan.
/// </para>
/// <para>
/// <b>Every read the plan judges is stable once the locks are held</b>: the defaults and every delta change only under the
/// Status-defaults lock, the Project list only under the Project-list lock, and the occupancy is counted after the last
/// Tasks lock is granted.
/// </para>
/// </remarks>
public interface ISpaceDefaultStatusRemovalStore
{
    /// <summary>
    /// Takes the active Space's Project-list lock, which Project creation and deletion take, and reads the Space's Projects.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>Every Project of the Space, its id to its name.</returns>
    Task<IReadOnlyDictionary<Guid, string>> LockProjectsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Takes the Tasks lock of each Project, in one fixed order whatever order they are given in.
    /// </summary>
    /// <param name="projectIds">The Projects, all of the active Space.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    Task LockProjectTasksAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>
    /// Records an admitted removal, in key order: every Project's Tasks migrated, the re-anchored places written in place of
    /// the old ones, every operation naming the Status as its Status or its anchor deleted, and the default's row deleted
    /// last.
    /// </summary>
    /// <param name="plan">The plan, as <see cref="SpaceDefaultStatusRemoval.Plan"/> admitted it.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(SpaceDefaultStatusRemovalPlan plan, CancellationToken cancellationToken);
}
