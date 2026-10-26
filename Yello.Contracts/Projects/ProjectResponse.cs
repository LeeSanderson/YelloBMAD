namespace Yello.Contracts.Projects;

/// <summary>
/// One Project, as the Space it belongs to lists it.
/// </summary>
/// <param name="Id">Its id.</param>
/// <param name="Name">Its name.</param>
/// <param name="CreatedAt">When it was created, in UTC.</param>
/// <param name="TaskCount">
/// How many Tasks it holds, in every Status - a true total (AD-29), appended by story 2.5 (open question 2 (a), AD-19)
/// so the deletion confirm can name it. A new Project holds none.
/// </param>
public sealed record ProjectResponse(Guid Id, string Name, DateTimeOffset CreatedAt, int TaskCount = 0);
