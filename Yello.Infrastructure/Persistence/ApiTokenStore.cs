using Microsoft.EntityFrameworkCore;
using Yello.Domain;
using Yello.Domain.ApiTokens;
using Yello.Infrastructure.ApiTokens;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Issues, finds and revokes API Tokens - the only reader of <c>ApiToken.SecretHash</c>. AD-6, NFR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>No session context is set here</b>, for the reason <see cref="AccountSessionStore"/> gives for
/// <c>Account</c>: <c>ApiToken</c> carries no policy, and the authentication lookup happens before
/// any Space is known. Issue runs inside the request's Space-scoped unit of work, which the
/// composite foreign key to <c>Membership</c> checks at insert; foreign-key validation is not
/// subject to row-level security.
/// </para>
/// <para>
/// <b>One indexed read per Token request, and no cache.</b> A Token that has been revoked, or whose
/// Membership, Space or Account has gone, has no row - so it is refused on the very next request with
/// no code here knowing why (story 1.8, AC8).
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the Token's identity.</param>
/// <param name="clock">Stamps the issue instant.</param>
internal sealed class ApiTokenStore(YelloDbContext dbContext, IIdentifierGenerator identifiers, TimeProvider clock)
    : IApiTokenStore
{
    /// <inheritdoc />
    public async Task<ApiTokenHolder?> FindAsync(string presented, CancellationToken cancellationToken)
    {
        if (!ApiTokenSecret.IsWellFormed(presented))
        {
            return null;
        }

        var hash = ApiTokenSecret.Hash(presented);

        return await dbContext.Set<ApiToken>()
            .AsNoTracking()
            .Where(token => token.SecretHash == hash)
            .Select(token => new ApiTokenHolder(token.Id, token.AccountId, token.BoundSpaceId))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IssuedApiToken> IssueAsync(
        Guid accountId,
        Guid spaceId,
        string name,
        CancellationToken cancellationToken)
    {
        var plaintext = ApiTokenSecret.Generate();

        var token = new ApiToken
        {
            Id = identifiers.Next(),
            AccountId = accountId,
            BoundSpaceId = spaceId,
            Name = name,
            SecretHash = ApiTokenSecret.Hash(plaintext),
            CreatedAt = clock.GetUtcNow(),
        };

        dbContext.Set<ApiToken>().Add(token);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new IssuedApiToken(token.Id, token.Name, token.CreatedAt, plaintext);
    }

    /// <inheritdoc />
    public async Task RevokeAsync(Guid accountId, Guid tokenId, CancellationToken cancellationToken) =>
        await dbContext.Set<ApiToken>()
            .Where(token => token.Id == tokenId && token.AccountId == accountId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
}
