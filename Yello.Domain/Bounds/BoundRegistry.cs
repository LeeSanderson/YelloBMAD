namespace Yello.Domain.Bounds;

/// <summary>
/// NFR-8's bounds, declared in one place: every limit the product enforces, its value, and the
/// creation operation that must consult it. AD-25 / AR-30.
/// </summary>
/// <remarks>
/// <para>
/// <b>All six, enumerated here rather than accumulated story by story</b> (AC8). A registry whose
/// completeness is incidental is one where a missing bound is invisible, and NFR-8 makes an
/// unenforced bound a defect rather than a relaxation.
/// <c>RefusalBoundaryGateTests.The_bound_registry_declares_all_six_bounds_with_their_values_and_operations</c>
/// holds the list against AC8's table.
/// </para>
/// <para>
/// <b>Enforced by the stories named, each of which consults this registry rather than declaring its
/// own limit:</b> concurrent active Sessions per Space by story 1.6, at Space resolution; Projects
/// per Space by 2.1; Tasks per Project by 2.2; Spaces per Account by 3.1; Memberships per Space by
/// 4.3; concurrent editors per Task by 7.3.
/// </para>
/// <para>
/// <b>Checked by the pipeline, never by a slice</b> (AR-3), and the refusal is raised inside the
/// transaction of the creation it refuses, with a machine-readable reason (AC7).
/// </para>
/// </remarks>
public static class BoundRegistry
{
    private const int ConcurrentActiveSessionsPerSpaceLimit = 50;
    private const int ProjectsPerSpaceLimit = 50;
    private const int TasksPerProjectLimit = 5_000;
    private const int SpacesPerAccountLimit = 50;
    private const int MembershipsPerSpaceLimit = 100;
    private const int ConcurrentEditorsPerTaskLimit = 10;

    /// <summary>
    /// Every bound, with its value and the creation operation that consults it.
    /// </summary>
    public static IReadOnlyList<BoundEntry> All { get; } =
    [
        new(Bound.ConcurrentActiveSessionsPerSpace, ConcurrentActiveSessionsPerSpaceLimit, BoundOperation.EstablishSessionInSpace),
        new(Bound.ProjectsPerSpace, ProjectsPerSpaceLimit, BoundOperation.CreateProject),
        new(Bound.TasksPerProject, TasksPerProjectLimit, BoundOperation.CreateTask),
        new(Bound.SpacesPerAccount, SpacesPerAccountLimit, BoundOperation.CreateSpace),
        new(Bound.MembershipsPerSpace, MembershipsPerSpaceLimit, BoundOperation.AcceptInvitation),
        new(Bound.ConcurrentEditorsPerTask, ConcurrentEditorsPerTaskLimit, BoundOperation.EstablishTaskSyncLease),
    ];

    /// <summary>
    /// One bound's entry.
    /// </summary>
    /// <param name="bound">The bound.</param>
    /// <returns>Its entry.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The bound is not declared.</exception>
    public static BoundEntry For(Bound bound)
    {
        foreach (var entry in All)
        {
            if (entry.Bound == bound)
            {
                return entry;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(bound), bound, "The bound is not declared in the registry.");
    }
}

/// <summary>
/// One NFR-8 bound.
/// </summary>
/// <param name="Bound">Which bound.</param>
/// <param name="Limit">Its value: the most the product admits.</param>
/// <param name="Operation">The creation operation that must consult it.</param>
public sealed record BoundEntry(Bound Bound, int Limit, BoundOperation Operation);

/// <summary>
/// NFR-8's bounds. The client renders a bound refusal from a resource keyed on the member's
/// <see cref="StableCode"/>.
/// </summary>
public enum Bound
{
    /// <summary>
    /// Concurrent active Sessions per Space.
    /// </summary>
    ConcurrentActiveSessionsPerSpace,

    /// <summary>
    /// Projects per Space.
    /// </summary>
    ProjectsPerSpace,

    /// <summary>
    /// Tasks per Project.
    /// </summary>
    TasksPerProject,

    /// <summary>
    /// Spaces per Account.
    /// </summary>
    SpacesPerAccount,

    /// <summary>
    /// Memberships per Space.
    /// </summary>
    MembershipsPerSpace,

    /// <summary>
    /// Concurrent editors per Task.
    /// </summary>
    ConcurrentEditorsPerTask,
}

/// <summary>
/// The creation operations NFR-8's bounds are consulted by.
/// </summary>
public enum BoundOperation
{
    /// <summary>
    /// Establishing a Session in a Space (FR-2, FR-9).
    /// </summary>
    EstablishSessionInSpace,

    /// <summary>
    /// Creating a Project (FR-17).
    /// </summary>
    CreateProject,

    /// <summary>
    /// Creating a Task (FR-19).
    /// </summary>
    CreateTask,

    /// <summary>
    /// Creating a Space (FR-5).
    /// </summary>
    CreateSpace,

    /// <summary>
    /// Accepting an Invitation (FR-11).
    /// </summary>
    AcceptInvitation,

    /// <summary>
    /// Establishing a sync lease on a Task (FR-31).
    /// </summary>
    EstablishTaskSyncLease,
}
