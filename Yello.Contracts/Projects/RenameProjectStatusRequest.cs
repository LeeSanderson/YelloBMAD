namespace Yello.Contracts.Projects;

/// <summary>
/// The body of a rename of one Status in a Project: its new name there, and nothing else.
/// </summary>
/// <param name="Name">The name, 1-100 characters once trimmed, unique in the Project's set ignoring case.</param>
public sealed record RenameProjectStatusRequest(string? Name);
