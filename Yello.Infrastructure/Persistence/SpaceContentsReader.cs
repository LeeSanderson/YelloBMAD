using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Counts the active Space's Projects, Tasks and other Members, live, inside the request's Space-scoped unit of work - for
/// the delete confirm to state. Story 3.3, AC2, open question 2 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Projects and Tasks need no <c>Where</c> on <c>SpaceId</c></b>: both isolation layers are narrowed to the resolved
/// Space (see <see cref="StatusDefinitionReader"/>).
/// </para>
/// <para>
/// <b>Memberships are filtered on the resolved Space all the same.</b> <c>Membership</c>'s policy admits a row by its Space
/// <i>or</i> its Account (<c>fn_MembershipVisibilityPredicate</c>), so the Space is named here rather than left to which
/// key happens to be set; the caller's own Membership is the one not counted.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class SpaceContentsReader(YelloDbContext dbContext) : ISpaceContentsReader
{
    /// <inheritdoc />
    public Task<SpaceContents> CountAsync(ActiveSpaceContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return CountCoreAsync(context.SpaceId, context.AccountId, cancellationToken);
    }

    private async Task<SpaceContents> CountCoreAsync(Guid spaceId, Guid accountId, CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects.CountAsync(cancellationToken).ConfigureAwait(false);
        var tasks = await dbContext.Tasks.CountAsync(cancellationToken).ConfigureAwait(false);
        var others = await dbContext.Memberships
            .CountAsync(membership => membership.SpaceId == spaceId && membership.AccountId != accountId, cancellationToken)
            .ConfigureAwait(false);

        return new SpaceContents(projects, tasks, others);
    }
}
