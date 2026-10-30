using Yello.Domain;
using Yello.Domain.Spaces;

namespace Yello.Application.Spaces.CreateSpace;

/// <summary>
/// Creates a Space: the caller becomes its one Owner, and it carries the default Statuses and no Projects. FR-5;
/// story 3.1.
/// </summary>
/// <remarks>
/// <b>Nothing cross-cutting is re-implemented here</b> (AR-3). The pipeline has opened the creation's unit of
/// work under both keys, replayed a key it had already answered and consulted the Spaces-per-Account bound
/// before this runs; it commits after. This provisions through <see cref="NewSpace"/>, exactly as registration
/// does, and saves.
/// </remarks>
/// <param name="spaces">Writes the rows on the pipeline's unit of work.</param>
/// <param name="identifiers">Draws the Membership's and the Statuses' ids.</param>
/// <param name="clock">One instant for every row.</param>
public sealed class CreateSpaceHandler(ISpaceStore spaces, IIdentifierGenerator identifiers, TimeProvider clock)
{
    /// <summary>
    /// Provisions and saves the new Space.
    /// </summary>
    /// <param name="context">The creation's context: the Account, and the server-generated Space id.</param>
    /// <param name="name">The Space's name, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The new Space's identity.</returns>
    public Task<SpaceIdentity> HandleAsync(SpaceCreationContext context, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var provisioned = NewSpace.Provision(context.SpaceId, context.AccountId, name, clock.GetUtcNow(), identifiers);

        // Split for S4457: the argument check above runs eagerly. The identity is the context's id and the name
        // given, so this slice never reads the policy-free Space type (Space_is_read_only_where_named).
        return SaveAsync(provisioned, new SpaceIdentity(context.SpaceId, name), cancellationToken);
    }

    private async Task<SpaceIdentity> SaveAsync(ProvisionedSpace provisioned, SpaceIdentity identity, CancellationToken cancellationToken)
    {
        await spaces.AddAsync(provisioned, cancellationToken).ConfigureAwait(false);

        return identity;
    }
}
