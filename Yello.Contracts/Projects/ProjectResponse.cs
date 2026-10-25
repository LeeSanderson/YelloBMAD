namespace Yello.Contracts.Projects;

/// <summary>
/// One Project, as the Space it belongs to lists it.
/// </summary>
/// <param name="Id">Its id.</param>
/// <param name="Name">Its name.</param>
/// <param name="CreatedAt">When it was created, in UTC.</param>
public sealed record ProjectResponse(Guid Id, string Name, DateTimeOffset CreatedAt);
