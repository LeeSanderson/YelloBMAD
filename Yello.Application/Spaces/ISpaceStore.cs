using Yello.Domain.Spaces;

namespace Yello.Application.Spaces;

/// <summary>
/// Writes a Space's own row: a new Space's rows on the creation's unit of work, and a rename of the active Space.
/// Story 3.1; FR-5, FR-6.
/// </summary>
/// <remarks>
/// <b><c>Space</c> carries no row-level security policy</b> (story 3.1, open question 5 (a), recorded in the AD-24
/// amendment), so the rename's <c>WHERE Id = </c> the resolved Space is the only thing scoping that write.
/// <c>RefusalBoundaryGateTests.The_only_update_of_a_Space_is_the_rename_of_the_resolved_Space</c> holds it.
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
    /// <returns>A task.</returns>
    Task RenameAsync(ActiveSpaceContext context, string name, CancellationToken cancellationToken);
}
