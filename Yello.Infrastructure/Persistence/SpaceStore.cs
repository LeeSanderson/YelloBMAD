using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Writes a new Space's rows on the creation's unit of work, and renames the active Space. Story 3.1; FR-5, FR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Creation saves its own effect</b>, as every store does, so the Account-scoped idempotency record the pipeline
/// writes next is the only change its save flushes.
/// </para>
/// <para>
/// <b>The rename filters on the resolved Space's id, and that <c>WHERE</c> is the only thing scoping it.</b>
/// <c>Space</c> carries no row-level security policy and no query filter (open question 5 (a), recorded in the
/// AD-24 amendment). The id is <see cref="ActiveSpaceContext.SpaceId"/>, which the pipeline resolved from the
/// route and verified against a Membership - never a value from the body. This is the one update of a
/// <c>Space</c> row in the product, and a gate holds that.
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
    public Task RenameAsync(ActiveSpaceContext context, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var spaceId = context.SpaceId;

        return dbContext.Spaces
            .Where(space => space.Id == spaceId)
            .ExecuteUpdateAsync(set => set.SetProperty(space => space.Name, name), cancellationToken);
    }
}
