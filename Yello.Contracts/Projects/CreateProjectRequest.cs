namespace Yello.Contracts.Projects;

/// <summary>
/// The body of a Project creation: its name, and nothing else. The Space is the route's, fixed at
/// creation (AC7).
/// </summary>
/// <param name="Name">The name, 1-100 characters once trimmed.</param>
public sealed record CreateProjectRequest(string? Name);
