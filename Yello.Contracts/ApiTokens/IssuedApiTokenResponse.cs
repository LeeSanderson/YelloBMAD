using Yello.Contracts.Spaces;

namespace Yello.Contracts.ApiTokens;

/// <summary>
/// The issuance response - the one response that ever carries a Token's plaintext (AC4, AC10).
/// </summary>
/// <remarks>
/// A class rather than a record so that a formatted instance never prints the secret.
/// </remarks>
/// <param name="id">The Token.</param>
/// <param name="name">Its name.</param>
/// <param name="space">The Space it is bound to, as identity only.</param>
/// <param name="createdAt">When it was issued.</param>
/// <param name="token">The plaintext. Shown once; never retrievable afterwards.</param>
public sealed class IssuedApiTokenResponse(
    Guid id,
    string name,
    SpaceIdentityResponse space,
    DateTimeOffset createdAt,
    string token)
{
    /// <summary>
    /// The Token.
    /// </summary>
    public Guid Id => id;

    /// <summary>
    /// Its name.
    /// </summary>
    public string Name => name;

    /// <summary>
    /// The Space it is bound to.
    /// </summary>
    public SpaceIdentityResponse Space => space;

    /// <summary>
    /// When it was issued.
    /// </summary>
    public DateTimeOffset CreatedAt => createdAt;

    /// <summary>
    /// The plaintext.
    /// </summary>
    public string Token => token;
}
