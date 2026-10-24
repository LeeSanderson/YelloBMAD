using Yello.Application.Spaces;

namespace Yello.Application.ApiTokens;

/// <summary>
/// Lists an Account's API Tokens for Account settings - AD-24's second Account-scoped surface.
/// FR-36; AD-24 / AR-29.
/// </summary>
/// <remarks>
/// Implemented beside the Space switcher's read, in the same Account-scoped unit of work, so the
/// <c>AccountId</c> session key keeps a single setter. Each Token's Space is named through a join
/// from the Account's own Membership, as identity only.
/// </remarks>
public interface IAccountApiTokenDirectory
{
    /// <summary>
    /// Lists the Account's Tokens.
    /// </summary>
    /// <param name="context">The Account-scoped context the pipeline established.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>Every Token the Account holds, oldest first. Empty is an answer.</returns>
    Task<IReadOnlyList<ApiTokenListing>> ListApiTokensAsync(
        AccountScopedContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// One Token as Account settings lists it: no Role, no hash, no secret (story 1.8, AC7).
/// </summary>
/// <param name="Id">The Token.</param>
/// <param name="Name">Its name.</param>
/// <param name="Space">The Space it is bound to, as identity only.</param>
/// <param name="CreatedAt">When it was issued.</param>
public sealed record ApiTokenListing(Guid Id, string Name, SpaceIdentity Space, DateTimeOffset CreatedAt);
