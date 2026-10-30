namespace Yello.Contracts.Spaces;

/// <summary>
/// The body of a Space creation: its name, and nothing else - never an id, which the server generates (AD-2).
/// Story 3.1.
/// </summary>
/// <param name="Name">The name, 1-200 characters once trimmed.</param>
public sealed record CreateSpaceRequest(string? Name);
