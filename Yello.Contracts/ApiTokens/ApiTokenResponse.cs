using Yello.Contracts.Spaces;

namespace Yello.Contracts.ApiTokens;

/// <summary>
/// One of the Account's Tokens, as Account settings lists it. No Role, no hash, no secret (AC7).
/// </summary>
/// <param name="Id">The Token.</param>
/// <param name="Name">Its name.</param>
/// <param name="Space">The Space it is bound to, as identity only.</param>
/// <param name="CreatedAt">When it was issued.</param>
public sealed record ApiTokenResponse(Guid Id, string Name, SpaceIdentityResponse Space, DateTimeOffset CreatedAt);
