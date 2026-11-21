namespace Yello.Contracts.Spaces;

/// <summary>
/// One Project a refusal names, identity only: a Project of the request's Space.
/// </summary>
/// <param name="Id">The Project.</param>
/// <param name="Name">Its name.</param>
public sealed record ProjectIdentityResponse(Guid Id, string Name);
