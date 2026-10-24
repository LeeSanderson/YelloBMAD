namespace Yello.Domain.ApiTokens;

/// <summary>
/// Issues, finds and revokes API Tokens. FR-36; AD-6, NFR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>The only reader of a Token's hash.</b> A presented Token is hashed and looked up by that hash
/// through a unique index, so no plaintext is compared anywhere and there is nothing to time.
/// </para>
/// <para>
/// <b>Nothing is cached.</b> Every Token request reads its row afresh, so a Token whose row has gone -
/// revoked, or deleted by the cascade from its Membership - is refused on the very next request
/// (FR-2's standard, applied to Tokens; story 1.8, AC8).
/// </para>
/// </remarks>
public interface IApiTokenStore
{
    /// <summary>
    /// Finds the Token a presented bearer value identifies.
    /// </summary>
    /// <param name="presented">The bearer value exactly as presented.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Token's holder and bound Space, or <c>null</c> for a malformed, unknown or invalidated value.</returns>
    Task<ApiTokenHolder?> FindAsync(string presented, CancellationToken cancellationToken);

    /// <summary>
    /// Issues a Token bound to the Account's Membership in one Space, inside the caller's unit of work.
    /// </summary>
    /// <param name="accountId">The issuing Account.</param>
    /// <param name="spaceId">The Space to bind it to, in which the Account holds a Membership.</param>
    /// <param name="name">The Token's name, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Token, with the plaintext - the only time it exists outside the caller's hands.</returns>
    Task<IssuedApiToken> IssueAsync(Guid accountId, Guid spaceId, string name, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes one of the Account's Tokens. A Token of another Account, or none, is left alone.
    /// </summary>
    /// <param name="accountId">The Account revoking.</param>
    /// <param name="tokenId">The Token to revoke.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task that completes when the row, if it was the Account's, is gone.</returns>
    Task RevokeAsync(Guid accountId, Guid tokenId, CancellationToken cancellationToken);
}

/// <summary>
/// Who a valid Token acts for, and where - read off its row, never off the Token string.
/// </summary>
/// <param name="TokenId">The Token.</param>
/// <param name="AccountId">The Account that issued it.</param>
/// <param name="BoundSpaceId">The one Space it reaches.</param>
public sealed record ApiTokenHolder(Guid TokenId, Guid AccountId, Guid BoundSpaceId);

/// <summary>
/// A Token as issued, carrying its plaintext once.
/// </summary>
/// <remarks>
/// A class rather than a record so that its default <see cref="object.ToString"/> prints nothing but
/// the type name: a record would print the plaintext into any log that formatted it.
/// </remarks>
/// <param name="id">The Token's identity.</param>
/// <param name="name">Its name.</param>
/// <param name="createdAt">When it was issued.</param>
/// <param name="plaintext">The secret, shown once.</param>
public sealed class IssuedApiToken(Guid id, string name, DateTimeOffset createdAt, string plaintext)
{
    /// <summary>
    /// The Token's identity.
    /// </summary>
    public Guid Id => id;

    /// <summary>
    /// Its name.
    /// </summary>
    public string Name => name;

    /// <summary>
    /// When it was issued, in UTC.
    /// </summary>
    public DateTimeOffset CreatedAt => createdAt;

    /// <summary>
    /// The secret. Returned to the issuer in the issuance response, and nowhere else.
    /// </summary>
    public string Plaintext => plaintext;
}
