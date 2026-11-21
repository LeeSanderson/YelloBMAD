namespace Yello.Contracts.Projects;

/// <summary>
/// The body of a place: the Status this one goes after, and nothing else.
/// </summary>
/// <param name="AfterStatusId">Another Status of the Project's set, or <c>null</c> to place it first.</param>
public sealed record PlaceProjectStatusRequest(Guid? AfterStatusId);
