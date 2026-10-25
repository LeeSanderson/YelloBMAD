namespace Yello.Contracts.Projects;

/// <summary>
/// The body of a Project rename: its new name, and nothing else - never a Space (AC7).
/// </summary>
/// <param name="Name">The new name, 1-100 characters once trimmed.</param>
public sealed record RenameProjectRequest(string? Name);
