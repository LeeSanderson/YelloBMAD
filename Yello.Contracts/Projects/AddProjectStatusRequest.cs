namespace Yello.Contracts.Projects;

/// <summary>
/// The body of an add to a Project's Statuses: its name, and optionally the Status it goes right after. Never a Space or
/// a Project - both are the route's.
/// </summary>
/// <param name="Name">The name, 1-100 characters once trimmed, unique in the Project's set ignoring case.</param>
/// <param name="AfterStatusId">The Status of the set it goes after, or <c>null</c> to append it.</param>
public sealed record AddProjectStatusRequest(string? Name, Guid? AfterStatusId);
