namespace Yello.Domain.ApiTokens;

/// <summary>
/// An API Token: a credential bound, at issue and for good, to one Account's Membership in one
/// Space. FR-36 / CAP-36; AD-6, NFR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Only a hash of the secret is held.</b> <see cref="SecretHash"/> is the SHA-256 of the
/// plaintext the issuer was shown once; nothing that can be presented as the Token is stored.
/// </para>
/// <para>
/// <b>No Role, no expiry, no last-used instant.</b> Capability is the issuing Account's current
/// Membership Role, read on each request (AD-1), so a Role stored here would be one that outlives
/// the request that read it. The Token ends when its row goes: revoked by its holder, or deleted by
/// the cascade from its Membership when the Membership, its Space or its Account is removed.
/// </para>
/// <para>
/// <b><see cref="BoundSpaceId"/>, not <c>SpaceId</c>.</b> The table is outside the Space isolation
/// policy on purpose: the authentication lookup runs before any Space is known. Every Space-scoped
/// gate recognises a Space-scoped table by the <c>SpaceId</c> column, and this is not one.
/// </para>
/// </remarks>
public sealed class ApiToken
{
    /// <summary>
    /// The Token's identity, produced application-side.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Account that issued the Token and on whose behalf it acts.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// The one Space the Token reaches. Set at issue and never changed (AC1).
    /// </summary>
    public required Guid BoundSpaceId { get; init; }

    /// <summary>
    /// The holder's label for the Token, 1-100 characters.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The SHA-256 of the plaintext, 32 bytes.
    /// </summary>
    public required byte[] SecretHash { get; init; }

    /// <summary>
    /// When the Token was issued, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
