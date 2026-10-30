namespace Yello.Contracts.Spaces;

/// <summary>
/// The body of a Space rename: its new name, and nothing else - the Space is the route's (AD-2). Story 3.1.
/// </summary>
/// <param name="Name">The new name, 1-200 characters once trimmed.</param>
public sealed record RenameSpaceRequest(string? Name);
