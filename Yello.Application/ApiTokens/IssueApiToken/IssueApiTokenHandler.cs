using Yello.Application.Spaces;
using Yello.Domain.ApiTokens;

namespace Yello.Application.ApiTokens.IssueApiToken;

/// <summary>
/// Issues an API Token bound to the active Space, for the acting Account. FR-36 / CAP-36; AD-6;
/// story 1.8, AC1 and AC4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline verified the
/// Membership, checked <c>IssueApiTokenForOneself</c> against the matrix - every Role holds it - and
/// opened the Space-scoped unit of work the row is written in. This slice reads the Account and the
/// Space off that context, and never its Role.
/// </para>
/// <para>
/// <b>Bound for good.</b> The Space is the one the request was resolved to; nothing afterwards can
/// change it (AC1).
/// </para>
/// </remarks>
/// <param name="store">Writes the Token.</param>
/// <param name="names">Reads the active Space's name for the response.</param>
public sealed class IssueApiTokenHandler(IApiTokenStore store, IActiveSpaceNameReader names)
{
    /// <summary>
    /// Issues the Token.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="name">The Token's name, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// The Token with its plaintext, and the Space it is bound to; or <c>null</c> when the Space was deleted after the
    /// pipeline resolved it (story 3.3) - answered as the boundary's <c>404</c>, and the Token rolled back with it.
    /// </returns>
    public Task<IssuedApiTokenInSpace?> HandleAsync(
        ActiveSpaceContext context,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Split for S4457.
        return HandleCoreAsync(context.AccountId, context.SpaceId, name, cancellationToken);
    }

    private async Task<IssuedApiTokenInSpace?> HandleCoreAsync(
        Guid accountId,
        Guid spaceId,
        string name,
        CancellationToken cancellationToken)
    {
        var issued = await store.IssueAsync(accountId, spaceId, name, cancellationToken).ConfigureAwait(false);
        var spaceName = await names.ReadAsync(cancellationToken).ConfigureAwait(false);

        return spaceName is null ? null : new IssuedApiTokenInSpace(issued, new SpaceIdentity(spaceId, spaceName));
    }
}

/// <summary>
/// An issued Token and the Space it is bound to.
/// </summary>
/// <remarks>A class, not a record, so the plaintext it carries is never printed by a default <see cref="object.ToString"/>.</remarks>
/// <param name="token">The Token, with its plaintext.</param>
/// <param name="space">The Space, as identity only.</param>
public sealed class IssuedApiTokenInSpace(IssuedApiToken token, SpaceIdentity space)
{
    /// <summary>
    /// The Token, with its plaintext.
    /// </summary>
    public IssuedApiToken Token => token;

    /// <summary>
    /// The Space it is bound to.
    /// </summary>
    public SpaceIdentity Space => space;
}
