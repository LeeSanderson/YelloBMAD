using Microsoft.EntityFrameworkCore;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// The transaction-owned application locks that serialise one Space's Project creations and deletions, and one Project's
/// key placers - and nothing in any other Space. Story 2.9, AC17, open question 14 (a); <c>deferred-work.md:471</c>,
/// <c>:476-477</c>, <c>:541</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not key-range locks.</b> Both used to be <c>UPDLOCK, HOLDLOCK</c> range scans: the Projects-per-Space count over
/// <c>dbo.[Project]</c>, and "the Project's Task range" over the <c>(SpaceId, ProjectId, …)</c> index that every Task key
/// placer took. Row-level security narrows the rows a scan returns but not the keys it locks, and a serializable scan
/// locks the first key after its range too - which, at the edge of a Space's keys, is another Space's. Measured
/// (<c>BoardPagingTests</c> PG-16, PG-17): a Task creation in Space B queued on <c>LCK_M_RS_U</c> behind Space A's held
/// 5,000-Task Project deletion, and a Project creation in B behind A's Project count - cross-tenant waits, and timing
/// signals. An application lock named for the Space or the Project serialises exactly the writers that share it.
/// </para>
/// <para>
/// <b>Reads under them are plain statements.</b> Under <c>READ_COMMITTED_SNAPSHOT</c> a statement reads the last state
/// committed before it began, so a count or a last key read after the lock is granted sees every write of the holder that
/// released it. Every writer that a count or a key placement must see takes the same lock first.
/// </para>
/// <para>
/// <b>The names are ids, lower case</b> (<c>Yello/ProjectsPerSpace/{spaceId:D}</c>,
/// <c>Yello/ProjectTasks/{spaceId:D}/{projectId:D}</c>): resource names compare binary, so every taker spells them the same
/// way. Released when the request's transaction ends.
/// </para>
/// <para>
/// <b>Both carry the request's Space.</b> An application lock is a name, not a row: the security policy does not narrow it.
/// The Tasks-per-Project count takes the Project's lock from the route before anything has checked that the Project is the
/// caller's, so a name of the Project alone would let a request in Space B that names Space A's Project queue behind A's
/// writers - a cross-tenant wait, and a timing signal that the id exists (story 2.9 code review). Named for the request's
/// Space as well, B's request takes a lock of B's own and answers <c>404</c> at once. A Project's writers all run in its
/// Space, so they still meet on one name.
/// </para>
/// </remarks>
internal static class ApplicationLocks
{
    /// <summary>
    /// The lock one Space's Project creations (the Projects-per-Space count) and Project deletions take.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The resource name.</returns>
    internal static string ProjectsPerSpace(Guid spaceId) => $"Yello/ProjectsPerSpace/{spaceId:D}";

    /// <summary>
    /// The lock every key placer of one Project's Tasks takes first - a creation (the Tasks-per-Project count), a Status
    /// edit, a move, a Task deletion - and a Project deletion before it removes them. Named for the requesting Space too.
    /// </summary>
    /// <param name="spaceId">The request's Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <returns>The resource name.</returns>
    internal static string ProjectTasks(Guid spaceId, Guid projectId) => $"Yello/ProjectTasks/{spaceId:D}/{projectId:D}";

    /// <summary>
    /// Takes the request's Space's Projects-per-Space lock.
    /// </summary>
    /// <param name="dbContext">The request's unit of work, scoped to a Space and inside its transaction.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    internal static Task TakeProjectsPerSpaceAsync(YelloDbContext dbContext, CancellationToken cancellationToken)
    {
        var spaceId = dbContext.ScopedSpaceId
            ?? throw new InvalidOperationException("The Projects-per-Space lock is taken only inside a Space-scoped unit of work.");

        return TakeAsync(dbContext, ProjectsPerSpace(spaceId), cancellationToken);
    }

    /// <summary>
    /// Takes one Project's Tasks lock, named for the request's Space and the Project.
    /// </summary>
    /// <param name="dbContext">The request's unit of work, scoped to a Space and inside its transaction.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    internal static Task TakeProjectTasksAsync(YelloDbContext dbContext, Guid projectId, CancellationToken cancellationToken)
    {
        var spaceId = dbContext.ScopedSpaceId
            ?? throw new InvalidOperationException("A Project's Tasks lock is taken only inside a Space-scoped unit of work.");

        return TakeAsync(dbContext, ProjectTasks(spaceId, projectId), cancellationToken);
    }

    /// <summary>
    /// Takes a lock exclusively, owned by the current transaction - waiting while another transaction holds it. Throws when
    /// it cannot be granted (a deadlock victim), so the request fails rather than going on unserialised.
    /// </summary>
    private static Task TakeAsync(YelloDbContext dbContext, string resource, CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlAsync(
            $"""
            DECLARE @granted int;
            EXEC @granted = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction';
            IF @granted < 0 THROW 51000, 'An application lock was not granted.', 1;
            """,
            cancellationToken);
}
