using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Reads the name of the Space this request's unit of work was resolved to. Story 1.7, AC5.
/// </summary>
/// <remarks>
/// The Space comes from <see cref="SpaceScope.Resolved"/> - the context
/// <see cref="MembershipActiveSpaceResolver"/> verified on this request - never from a parameter, so
/// this cannot be pointed at a Space the caller has not proved Membership in. It runs inside the
/// pipeline's unit of work, on the connection already scoped to that Space.
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="spaceScope">The request's Space scope, holding the resolved context.</param>
internal sealed class ActiveSpaceNameReader(YelloDbContext dbContext, SpaceScope spaceScope) : IActiveSpaceNameReader
{
    /// <inheritdoc />
    public async Task<string> ReadAsync(CancellationToken cancellationToken)
    {
        var resolved = spaceScope.Resolved
            ?? throw new InvalidOperationException(
                "No active Space has been resolved on this request, so there is no Space whose name may be read.");

        return await dbContext.Spaces
            .AsNoTracking()
            .Where(space => space.Id == resolved.SpaceId)
            .Select(space => space.Name)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
