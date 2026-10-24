namespace Yello.Contracts.ApiTokens;

/// <summary>
/// The body of an issuance request.
/// </summary>
/// <param name="Name">The Token's name: required, trimmed, 1-100 characters.</param>
public sealed record IssueApiTokenRequest(string? Name);
