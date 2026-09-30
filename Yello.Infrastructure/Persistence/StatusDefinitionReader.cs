using Microsoft.EntityFrameworkCore;
using Yello.Domain.Statuses;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Reads the Status definitions visible to the current unit of work.
/// </summary>
/// <remarks>
/// <b>No <c>Where</c> on <c>SpaceId</c>, and that is the point.</b> Both isolation layers - the
/// row-level security policy and the global query filter - are already narrowed to the request's
/// Space, so this query states what it wants rather than where it may look. A filter written here
/// as well would be a third layer that hides whether the other two work.
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class StatusDefinitionReader(YelloDbContext dbContext) : IStatusDefinitionReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.StatusDefinitions
            .AsNoTracking()
            .OrderBy(status => status.Position)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
