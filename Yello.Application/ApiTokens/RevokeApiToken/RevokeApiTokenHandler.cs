using Yello.Application.Spaces;
using Yello.Domain.ApiTokens;

namespace Yello.Application.ApiTokens.RevokeApiToken;

/// <summary>
/// Revokes one of the acting Account's API Tokens. FR-36, FR-2's standard; story 1.8, open question 3.
/// </summary>
/// <remarks>
/// The same answer whether or not the id was the Account's: an id belonging to another Account is
/// indistinguishable from an unknown one and is left alone. The row is deleted, so the next request
/// with that Token finds nothing and is refused (AC8).
/// </remarks>
/// <param name="store">Deletes the row, if it is the Account's.</param>
public sealed class RevokeApiTokenHandler(IApiTokenStore store)
{
    /// <summary>
    /// Revokes the Token.
    /// </summary>
    /// <param name="context">The Account-scoped context the pipeline established.</param>
    /// <param name="tokenId">The Token to revoke.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task that completes when the Token, if it was the Account's, is gone.</returns>
    public Task HandleAsync(AccountScopedContext context, Guid tokenId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return store.RevokeAsync(context.AccountId, tokenId, cancellationToken);
    }
}
