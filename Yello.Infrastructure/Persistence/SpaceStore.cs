using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Writes a new Space's rows on the creation's unit of work, renames the active Space, and deletes it. Stories 3.1 and
/// 3.3; FR-5, FR-6, FR-7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Creation saves its own effect</b>, as every store does, so the Account-scoped idempotency record the pipeline
/// writes next is the only change its save flushes.
/// </para>
/// <para>
/// <b>The rename and the deletion filter on the resolved Space's id, and that <c>WHERE</c> is the only thing scoping
/// them.</b> <c>Space</c> carries no row-level security policy and no query filter (open question 5 (a), recorded in the
/// AD-24 amendment). The id is <see cref="ActiveSpaceContext.SpaceId"/>, which the pipeline resolved from the route and
/// verified against a Membership - never a value from the body. These are the only bulk writes of a <c>Space</c> row in
/// the product, and two gates hold that (the AD-24 amendment of 2026-10-31).
/// </para>
/// <para>
/// <b>The deletion is one statement, run under the <c>SpaceId</c> key alone</b> (story 3.3, AC10). The database's
/// cascades do the rest - <c>Membership</c> (and through it <c>ApiToken</c>, so every Token bound to the Space goes in
/// the same transaction: AD-6, FR-36), <c>Project</c> (and through it <c>Task</c> and <c>TaskLabel</c>),
/// <c>StatusDefinition</c>, <c>Label</c>, <c>SpacePresence</c> and <c>IdempotencyRecord</c>. <c>Task → StatusDefinition</c>
/// and <c>TaskLabel → Label</c> are <c>NO ACTION</c>, which SQL Server checks at the end of the statement, after the
/// cascades have emptied both sides; the populated-Space slice case proves it. Child rows are never deleted by hand.
/// <c>AccessRefusal</c> carries no foreign key, so the refusals naming the Space outlive it (NFR-7).
/// </para>
/// <para>
/// <b>The deletion takes the Space's Projects-per-Space lock first</b> (open question 4 (a)), as a Project creation's bound
/// count takes it, so a creation that has counted finishes - and is deleted with the Space - before the deletion runs,
/// rather than meeting the foreign key afterwards; a creation queued behind the deletion meets the foreign key and is
/// answered the boundary's <c>404</c> by the pipeline.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class SpaceStore(YelloDbContext dbContext) : ISpaceStore
{
    /// <inheritdoc />
    public Task AddAsync(ProvisionedSpace provisioned, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provisioned);

        dbContext.Spaces.Add(provisioned.Space);
        dbContext.Memberships.Add(provisioned.OwnerMembership);
        dbContext.StatusDefinitions.AddRange(provisioned.Statuses);

        return dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> RenameAsync(ActiveSpaceContext context, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Split for S4457: the argument check above runs eagerly.
        return RenameCoreAsync(context.SpaceId, name, cancellationToken);
    }

    /// <inheritdoc />
    public Task<SpaceDeletion> DeleteAsync(ActiveSpaceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return DeleteCoreAsync(context.SpaceId, cancellationToken);
    }

    private async Task<bool> RenameCoreAsync(Guid spaceId, string name, CancellationToken cancellationToken)
    {
        var renamed = await dbContext.Spaces
            .Where(space => space.Id == spaceId)
            .ExecuteUpdateAsync(set => set.SetProperty(space => space.Name, name), cancellationToken)
            .ConfigureAwait(false);

        return renamed != 0;
    }

    private async Task<SpaceDeletion> DeleteCoreAsync(Guid spaceId, CancellationToken cancellationToken)
    {
        await ApplicationLocks.TakeProjectsPerSpaceAsync(dbContext, cancellationToken).ConfigureAwait(false);

        var deleted = await dbContext.Spaces
            .Where(space => space.Id == spaceId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        return deleted == 0 ? SpaceDeletion.NotFound : SpaceDeletion.Deleted;
    }
}
