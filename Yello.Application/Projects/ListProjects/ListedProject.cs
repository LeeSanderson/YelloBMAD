using Yello.Domain.Projects;

namespace Yello.Application.Projects.ListProjects;

/// <summary>
/// A Project with its true Task count (AD-29), as the listing and a rename state it. Story 2.5, open question 2 (a).
/// </summary>
/// <param name="Project">The Project.</param>
/// <param name="TaskCount">How many Tasks it holds, in every Status.</param>
public sealed record ListedProject(Project Project, int TaskCount);
