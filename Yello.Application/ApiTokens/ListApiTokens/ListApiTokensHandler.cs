using Yello.Application.Spaces;

namespace Yello.Application.ApiTokens.ListApiTokens;

/// <summary>
/// Lists the acting Account's API Tokens. FR-36; AD-24 / AR-29; story 1.8, AC7.
/// </summary>
/// <remarks>
/// The pipeline established the Account-scoped context from the Session before this ran; a Token
/// never reaches here (AC9). Nothing per-Space but identity is returned.
/// </remarks>
/// <param name="directory">Reads the Account's Tokens under its Account-scoped unit of work.</param>
public sealed class ListApiTokensHandler(IAccountApiTokenDirectory directory)
{
    /// <summary>
    /// Lists the Account's Tokens.
    /// </summary>
    /// <param name="context">The Account-scoped context the pipeline established.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Tokens, oldest first.</returns>
    public Task<IReadOnlyList<ApiTokenListing>> HandleAsync(
        AccountScopedContext context,
        CancellationToken cancellationToken) =>
        directory.ListApiTokensAsync(context, cancellationToken);
}
