using Yello.Domain.Spaces;

namespace Yello.Application.Spaces;

/// <summary>
/// Writes a Space's own row: a new Space's rows on the creation's unit of work, a rename of the active Space, and the
/// deletion of the active Space. Stories 3.1 and 3.3; FR-5, FR-6, FR-7.
/// </summary>
/// <remarks>
/// <b><c>Space</c> carries no row-level security policy</b> (story 3.1, open question 5 (a), recorded in the AD-24
/// amendment), so the rename's and the deletion's <c>WHERE Id = </c> the resolved Space is the only thing scoping
/// those writes. <c>RefusalBoundaryGateTests.The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space</c> and
/// <c>The_only_bulk_writes_of_a_Space_are_the_rename_and_the_deletion_in_SpaceStore</c> hold it (the AD-24 amendment of
/// 2026-10-31).
/// </remarks>
public interface ISpaceStore
{
    /// <summary>
    /// Adds a new Space's rows and saves them, on the creation's unit of work.
    /// </summary>
    /// <param name="provisioned">The Space, its Owner Membership and its default Statuses.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task.</returns>
    Task AddAsync(ProvisionedSpace provisioned, CancellationToken cancellationToken);

    /// <summary>
    /// Renames the active Space - the one the pipeline resolved, never one named by the request body.
    /// </summary>
    /// <param name="context">The resolved context.</param>
    /// <param name="name">The new name, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>true</c> when the row was renamed; <c>false</c> when no row matched, because the Space was deleted after the
    /// pipeline resolved it (story 3.3, <c>deferred-work.md:666</c>).
    /// </returns>
    Task<bool> RenameAsync(ActiveSpaceContext context, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the active Space - the one the pipeline resolved - in one statement, the database's cascades taking every
    /// Membership, API Token, Project, Task, Label, Status, presence row and idempotency record with it. Story 3.3.
    /// </summary>
    /// <param name="context">The resolved context.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What the deletion came to.</returns>
    Task<SpaceDeletion> DeleteAsync(ActiveSpaceContext context, CancellationToken cancellationToken);
}
