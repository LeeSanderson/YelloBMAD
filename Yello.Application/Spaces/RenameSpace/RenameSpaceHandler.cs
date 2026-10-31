namespace Yello.Application.Spaces.RenameSpace;

/// <summary>
/// Renames the active Space. FR-6; story 3.1.
/// </summary>
/// <remarks>
/// <b>The pipeline has already decided who may</b> (AR-3): the route declares <c>RenameSpace</c>, which the matrix
/// grants Owner and Admin, and a non-member was refused at the boundary. This writes the resolved Space and no
/// other - the request names no Space but the route's.
/// </remarks>
/// <param name="spaces">Writes the name.</param>
public sealed class RenameSpaceHandler(ISpaceStore spaces)
{
    /// <summary>
    /// Renames the resolved Space.
    /// </summary>
    /// <param name="context">The context the pipeline resolved.</param>
    /// <param name="name">The new name, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// The Space's identity under its new name, or <c>null</c> when no row was renamed because the Space was deleted
    /// after it was resolved (story 3.3, <c>deferred-work.md:666</c>) - answered as the boundary's <c>404</c>.
    /// </returns>
    public Task<SpaceIdentity?> HandleAsync(ActiveSpaceContext context, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Split for S4457: the argument check above runs eagerly.
        return RenameAsync(context, name, cancellationToken);
    }

    private async Task<SpaceIdentity?> RenameAsync(ActiveSpaceContext context, string name, CancellationToken cancellationToken)
    {
        var renamed = await spaces.RenameAsync(context, name, cancellationToken).ConfigureAwait(false);

        return renamed ? new SpaceIdentity(context.SpaceId, name) : null;
    }
}
